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
        public string SubscriptionId { get; set; } = "sudokumb";
        /// <summary>
        /// The Pub/sub topic where solve messages are written.
        /// </summary>
        public string TopicId { get; set; } = "sudokumb";
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
        readonly IOptions<PubsubGameBoardQueueOptions> _options;
        readonly Solver _solver;


        public PubsubGameBoardQueueImpl(
            IOptions<PubsubGameBoardQueueOptions> options,
            ILogger<PubsubGameBoardQueueImpl> logger,
            Solver solver)
        {
            _logger = logger;
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
                Board = board,
                GameSearchTreeDepth = gameSearchTreeDepth + 1
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
            await _solver.ExamineGameBoard(message.SolveRequestId, message.Board,
                message.GameSearchTreeDepth, cancellationToken);
            return cancellationToken.IsCancellationRequested ?
                SubscriberClient.Reply.Nack : SubscriberClient.Reply.Ack;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(async() =>
            {
                // This potentially hammers the CPU, so wait until everthing
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
        public PubsubGameBoardQueue(IOptions<PubsubGameBoardQueueOptions> options,
             ILogger<PubsubGameBoardQueueImpl> logger, Solver solver)
             : base(options, logger, solver)
        {
            solver.Queue = this;
        }
    }

    public class GameBoardMessage
    {
        public string SolveRequestId { get; set; }
        public GameBoard Board { get; set; }
        public int GameSearchTreeDepth { get; set; }
    }
}

