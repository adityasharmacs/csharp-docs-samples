using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Sudokumb
{
    public class PubsubGameBoardQueueOptions
    {
        /// <summary>
        /// The Google Cloud project id.
        /// </summary>
        public string ProjectId { get; set; }
        /// <summary>
        /// The Pub/sub subscription from which solve messages are read.
        /// </summary>
        public string SubscriptionId { get; set; } = "sudokumb3";
        /// <summary>
        /// The Pub/sub topic where solve messages are written.
        /// </summary>
        public string TopicId { get; set; } = "sudokumb3";
    }

    public static class PubsubGameBoardQueueExtensions
    {
        public static IServiceCollection AddPubsubGameBoardQueue(
            this IServiceCollection services)
        {
            services.AddSingleton<PubsubGameBoardQueue>();
            services.AddSingleton<IGameBoardQueue, PubsubGameBoardQueue>(
                provider => provider.GetService<PubsubGameBoardQueue>()
            );
            services.AddSingleton<IHostedService, PubsubGameBoardQueue>(
                provider => provider.GetService<PubsubGameBoardQueue>()
            );
            return services;
        }
    }

    public class PubsubGameBoardQueueImpl
    {
        readonly PublisherServiceApiClient _publisherApi;
        readonly PublisherClient _publisherClient;
        readonly SubscriberClient _subscriberClient;
        readonly ILogger<PubsubGameBoardQueueImpl> _logger;
        private readonly SolveStateStore _solveStateStore;
        readonly IOptions<PubsubGameBoardQueueOptions> _options;
        readonly Solver _solver;
        const int MAX_STACKS = -1;

        public PubsubGameBoardQueueImpl(
            IOptions<PubsubGameBoardQueueOptions> options,
            ILogger<PubsubGameBoardQueueImpl> logger,
            SolveStateStore solveStateStore,
            Solver solver)
        {
            _logger = logger;
            _solveStateStore = solveStateStore;
            _options = options;
            _solver = solver;
            _publisherApi = PublisherServiceApiClient.Create();
            var subscriberApi = SubscriberServiceApiClient.Create();
            _publisherClient = PublisherClient.Create(MyTopic,
                new [] { _publisherApi});
            _subscriberClient = SubscriberClient.Create(MySubscription,
                new [] {subscriberApi}, new SubscriberClient.Settings()
                {
                    StreamAckDeadline = TimeSpan.FromMinutes(1)
                });

            // Create the Topic and Subscription.
            try
            {
                _publisherApi.CreateTopic(MyTopic);
                _logger.LogInformation("Created {0}.", MyTopic.ToString());
            }
            catch (RpcException e)
            when (e.Status.StatusCode == StatusCode.AlreadyExists)
            {
                // Already exists.  That's fine.
            }

            try
            {
                subscriberApi.CreateSubscription(MySubscription, MyTopic,
                    pushConfig: null, ackDeadlineSeconds: 10);
                _logger.LogInformation("Created {0}.",
                    MySubscription.ToString());
            }
            catch (RpcException e)
            when (e.Status.StatusCode == StatusCode.AlreadyExists)
            {
                // Already exists.  That's fine.
            }
        }

        public TopicName MyTopic
        {
             get
             {
                 var opts = _options.Value;
                 return new TopicName(opts.ProjectId, opts.TopicId);
             }
        }

        public SubscriptionName MySubscription
        {
             get
             {
                 var opts = _options.Value;
                 return new SubscriptionName(opts.ProjectId,
                    opts.SubscriptionId);
             }
        }

        public Func<GameBoardMessage, CancellationToken,
            Task<bool>> GameBoardMessageHandler { get; set; }

        public async Task<bool> Publish(string solveRequestId,
            IEnumerable<GameBoard> gameBoards, int gameSearchTreeDepth,
            CancellationToken cancellationToken)
        {
            var messages = gameBoards.Select(board => new GameBoardMessage()
            {
                SolveRequestId = solveRequestId,
                Boards = new [] { board },
                Stacks = 1,
            });
            var pubsubMessages = messages.Select(message => new PubsubMessage()
            {
                Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(
                    message))
            });
            await _publisherApi.PublishAsync(MyTopic, pubsubMessages,
                CallSettings.FromCancellationToken(cancellationToken));
            return false;
        }

        /// <summary>
        /// Solve one sudoku puzzle.
        /// </summary>
        /// <param name="pubsubMessage">The message as it arrived from Pub/Sub.
        /// </param>
        /// <returns>Ack or Nack</returns>
        async Task<SubscriberClient.Reply> ProcessOneMessage(
            PubsubMessage pubsubMessage, CancellationToken cancellationToken)
        {
            // Unpack the pubsub message.
            string text = pubsubMessage.Data.ToString(Encoding.UTF8);
            GameBoardMessage message;
            try
            {
                message = JsonConvert.DeserializeObject<GameBoardMessage>(text);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Bad message in subscription {0}\n{1}",
                    MySubscription, text);
                return SubscriberClient.Reply.Ack;
            }
            if (message.Boards == null || message.Boards.Length == 0 ||
                string.IsNullOrEmpty(message.SolveRequestId))
            {
                _logger.LogError("Bad message in subscription {0}\n{1}",
                    MySubscription, text);
                return SubscriberClient.Reply.Ack;
            }
            // Examine the board.
            IEnumerable<GameBoard> nextMoves;
            _solveStateStore.IncreaseExaminedBoardCount(
                message.SolveRequestId, 1);
            if (_solver.ExamineGameBoard(message.Boards.Last(), out nextMoves))
            {
                await _solveStateStore.SetAsync(message.SolveRequestId,
                    message.Boards.Last(), cancellationToken);
                return SubscriberClient.Reply.Ack;
            }
            int stacks = nextMoves.Count();
            List<Task> tasks = new List<Task>();
            if (stacks * message.Stacks > MAX_STACKS) 
            {
                // Too many stacks.  Don't fork again.            
                List<GameBoard> stack =
                    new List<GameBoard>(message.Boards.SkipLast(1));
                stack.AddRange(nextMoves);
                message.Boards = stack.ToArray();
                // Republish the message with the new stack.
                string newText = JsonConvert.SerializeObject(message);
                tasks.Add(_publisherClient.PublishAsync(new PubsubMessage()
                {
                    Data = ByteString.CopyFromUtf8(newText)
                }));
            }
            else
            {
                // Fork this one stack into multiple stacks.
                message.Stacks *= stacks;
                foreach (GameBoard move in nextMoves)
                {
                    message.Boards[message.Boards.Length -1] = move;
                    // Republish the message with the new stack.
                    string newText = JsonConvert.SerializeObject(message);
                    tasks.Add(_publisherClient.PublishAsync(new PubsubMessage()
                    {
                        Data = ByteString.CopyFromUtf8(newText)
                    }));
                }
            }
            foreach (Task task in tasks) await task;
            return SubscriberClient.Reply.Ack;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(async() =>
            {
                // This potentially hammers the CPU, so wait until everything
                // else starts up.
                await Task.Delay(TimeSpan.FromSeconds(10));
                await _subscriberClient.StartAsync(
                    (message, token) => ProcessOneMessage(message, token));
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) =>
            _subscriberClient.StopAsync(cancellationToken);
    }

    public class PubsubGameBoardQueue : PubsubGameBoardQueueImpl, IGameBoardQueue, IHostedService
    {
        public PubsubGameBoardQueue(
            IOptions<PubsubGameBoardQueueOptions> options,
            ILogger<PubsubGameBoardQueueImpl> logger,
            SolveStateStore solveStateStore, Solver solver)
            : base(options, logger, solveStateStore, solver)
        {
        }
    }

    public class GameBoardMessage
    {
        public string SolveRequestId { get; set; }
        public GameBoard[] Boards { get; set; }
        public int Stacks { get; set; }
    }
}


