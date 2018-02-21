using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Datastore.V1;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sudokumb;
using WebApp.Models;
using Newtonsoft.Json;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;

namespace WebApp.Services
{
    public class SolverOptions
    {
        public string ProjectId { get; set; }
        public string SubscriptionId { get; set; } = "sudokumb";
        public string TopicId { get; set; } = "sudokumb";
    }
    /// <summary>
    /// Reads sudoku puzzles from Pub/Sub and solves them.
    /// </summary>
    public class Solver : IHostedService
    {
        readonly PublisherServiceApiClient publisherApi_;
        readonly PublisherClient publisherClient_;
        readonly SubscriberClient subscriberClient_;

        readonly SolveStateStore solveStateStore_;
        readonly ILogger<Solver> logger_;
        readonly IOptions<SolverOptions> options_;

        readonly IsDumb isDumb_;

        class Message
        {
            public string SolveRequestId { get; set; }
            public GameBoard Board { get; set; }
        }

        public Solver(IOptions<SolverOptions> options,
            SolveStateStore solveStateStore,
            IsDumb isDumb,
            ILogger<Solver> logger)
        {
            logger_ = logger;
            options_ = options;
            isDumb_ = isDumb;
            solveStateStore_ = solveStateStore;
            publisherApi_ = PublisherServiceApiClient.Create();
            var subscriberApi = SubscriberServiceApiClient.Create();
            publisherClient_ = PublisherClient.Create(MyTopic,
                new [] { publisherApi_});
            subscriberClient_ = SubscriberClient.Create(MySubscription,
                new [] {subscriberApi});

            // Create the Topic and Subscription.
            try
            {
                publisherApi_.CreateTopic(MyTopic);
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
                 var opts = options_.Value;
                 return new TopicName(opts.ProjectId, opts.TopicId);
             }
        }

        public SubscriptionName MySubscription
        {
             get
             {
                 var opts = options_.Value;
                 return new SubscriptionName(opts.ProjectId,
                    opts.SubscriptionId);
             }
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
            Message message;
            try
            {
                message = JsonConvert.DeserializeObject<Message>(text);
            }
            catch (Exception e)
            {
                logger_.LogError(e, "Bad message in subscription {0}\n{1}",
                    MySubscription, text);
                return SubscriberClient.Reply.Ack;
            }
            var moves = new Stack<GameBoard>();
            moves.Push(message.Board);
            bool isDumb = await isDumb_.IsDumbAsync();
            while (moves.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return SubscriberClient.Reply.Nack;
                }
                GameBoard board = moves.Pop();
                if (!board.HasEmptyCell())
                {
                    // Solved!
                    await solveStateStore_.SetAsync(message.SolveRequestId,
                        board);
                    return SubscriberClient.Reply.Ack;
                }
                // Enumerate the next possible board states.
                foreach (var move in board.FillNextEmptyCell())
                {
                    if (isDumb)
                    {
                        await Publish(message.SolveRequestId, move);
                    }
                    else
                    {
                        moves.Push(move);
                    }
                }
            }
            return SubscriberClient.Reply.Ack;
        }

        public async Task<string> StartSolving(GameBoard gameBoard)
        {
            // Create a new request and publish it to pubsub.
            string solveRequestId = Guid.NewGuid().ToString();
            await Publish(solveRequestId, gameBoard);
            return solveRequestId;
        }

        /// <summary>
        /// Publishes a new game board to Pub/Sub to be solved.
        /// </summary>
        /// <param name="solveRequestId">The solve request id.</param>
        /// <param name="gameBoard">The gameboard to be solved.</param>
        /// <returns>A Task that completes when the message has been published
        /// to Pub/Sub.</returns>
        async Task Publish(string solveRequestId, GameBoard gameBoard)
        {
            // Create a new request and publish it to pubsub.
            var message = new Message()
            {
                SolveRequestId = solveRequestId,
                Board = gameBoard
            };
            await publisherApi_.PublishAsync(MyTopic, new []
            {
                new PubsubMessage()
                {
                    Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(
                        message))
                }
            });
        }

        public Task<SolveState> GetProgress(string solveRequestId) =>
            solveStateStore_.GetAsync(solveRequestId);

        Task IHostedService.StartAsync(CancellationToken cancellationToken) =>
            subscriberClient_.StartAsync(
                (message, token) => ProcessOneMessage(message, token));

        Task IHostedService.StopAsync(CancellationToken cancellationToken) =>
            subscriberClient_.StopAsync(cancellationToken);
    }
}