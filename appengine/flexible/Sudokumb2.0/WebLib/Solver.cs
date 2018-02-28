using System;
using System.Linq;
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
using Newtonsoft.Json;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;

namespace Sudokumb
{
    public class SolverOptions
    {
        /// <summary>
        /// The Google Cloud project id.
        /// </summary>
        public string ProjectId { get; set; }
        /// <summary>
        /// The Pub/sub subscription from which solve messages are read.
        /// </summary>
        public string SubscriptionId { get; set; } = "sudokumb2";
        /// <summary>
        /// The Pub/sub topic where solve messages are written.
        /// </summary>
        public string TopicId { get; set; } = "sudokumb2";
    }

    public interface ISolveRequester
    {
        Task<string> StartSolving(GameBoard gameBoard);
        Task<SolveState> GetProgress(string solveRequestId,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Reads sudoku puzzles from Pub/Sub and solves them.
    /// </summary>
    public class Solver : IHostedService, ISolveRequester
    {
        readonly PublisherServiceApiClient _publisherApi;
        readonly PublisherClient _publisherClient;
        readonly SubscriberClient _subscriberClient;

        readonly SolveStateStore _solveStateStore;
        readonly ILogger<Solver> _logger;
        readonly IOptions<SolverOptions> _options;
        readonly IDumb _idumb;

        readonly ICounter counter_;

        class Message
        {
            public string SolveRequestId { get; set; }
            public GameBoard Board { get; set; }
        }

        class NeverDumb : IDumb
        {
            public Task<bool> IsDumbAsync() => Task.FromResult(false);
        }

        public Solver(IOptions<SolverOptions> options,
            SolveStateStore solveStateStore,
            ILogger<Solver> logger)
            : this(options, solveStateStore, new NeverDumb(), logger,
                new InterlockedCounter()) { }

        public Solver(IOptions<SolverOptions> options,
            SolveStateStore solveStateStore,
            IDumb idumb,
            ILogger<Solver> logger,
            ICounter counter)
        {
            _logger = logger;
            _options = options;
            _idumb = idumb;
            _solveStateStore = solveStateStore;
            counter_ = counter;
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
                _logger.LogError(e, "Bad message in subscription {0}\n{1}",
                    MySubscription, text);
                return SubscriberClient.Reply.Ack;
            }
            var moves = new Stack<GameBoard>();
            moves.Push(message.Board);
            bool isDumb = await _idumb.IsDumbAsync();
            while (moves.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "Cancellation requested in ProcessOneMessage().");
                    return SubscriberClient.Reply.Nack;
                }
                counter_.Increase(1);
                _solveStateStore.IncreaseExaminedBoardCount(
                    message.SolveRequestId, 1);
                GameBoard board = moves.Pop();
                if (!board.HasEmptyCell())
                {
                    // Solved!
                    await _solveStateStore.SetAsync(message.SolveRequestId,
                        board);
                    return SubscriberClient.Reply.Ack;
                }
                // Enumerate the next possible board states.
                if (isDumb)
                {
                    await Publish(message.SolveRequestId,
                        board.FillNextEmptyCell());
                }
                else foreach (var move in board.FillNextEmptyCell())
                {
                    moves.Push(move);
                }
            }
            return SubscriberClient.Reply.Ack;
        }

        public async Task<string> StartSolving(GameBoard gameBoard)
        {
            // Create a new request and publish it to pubsub.
            string solveRequestId = Guid.NewGuid().ToString();
            await Publish(solveRequestId, new [] { gameBoard });
            return solveRequestId;
        }

        /// <summary>
        /// Publishes a new game board to Pub/Sub to be solved.
        /// </summary>
        /// <param name="solveRequestId">The solve request id.</param>
        /// <param name="gameBoards">The gameboards to be solved.</param>
        /// <returns>A Task that completes when the message has been published
        /// to Pub/Sub.</returns>
        async Task Publish(string solveRequestId,
            IEnumerable<GameBoard> gameBoards)
        {
            var messages = gameBoards.Select(board => new Message()
            {
                SolveRequestId = solveRequestId,
                Board = board
            });
            var pubsubMessages = messages.Select(message => new PubsubMessage()
            {
                Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(
                    message))
            });
            await _publisherApi.PublishAsync(MyTopic, pubsubMessages);
        }

        public Task<SolveState> GetProgress(string solveRequestId,
            CancellationToken cancellationToken) =>
            _solveStateStore.GetAsync(solveRequestId, cancellationToken);

        Task IHostedService.StartAsync(CancellationToken cancellationToken) =>
            _subscriberClient.StartAsync(
                (message, token) => ProcessOneMessage(message, token));

        Task IHostedService.StopAsync(CancellationToken cancellationToken) =>
            _subscriberClient.StopAsync(cancellationToken);
    }
}