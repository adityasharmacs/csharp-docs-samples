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

    public class PubsubGameBoardQueue : IGameBoardQueue
    {
        readonly PublisherServiceApiClient _publisherApi;
        readonly PublisherClient _publisherClient;
        readonly SubscriberClient _subscriberClient;

        readonly SolveStateStore _solveStateStore;
        readonly ILogger<PubsubGameBoardQueue> _logger;
        readonly IOptions<PubsubGameBoardQueueOptions> _options;


        public PubsubGameBoardQueue(
            IOptions<PubsubGameBoardQueueOptions> options,
            ILogger<PubsubGameBoardQueue> logger)
        {
            _logger = logger;
            _options = options;
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

        public async Task Publish(string solveRequestId,
            IEnumerable<GameBoard> gameBoards,
            CancellationToken cancellationToken)
        {
            var messages = gameBoards.Select(board => new GameBoardMessage()
            {
                SolveRequestId = solveRequestId,
                Board = board
            });
            var pubsubMessages = messages.Select(message => new PubsubMessage()
            {
                Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(
                    message))
            });
            await _publisherApi.PublishAsync(MyTopic, pubsubMessages,
                CallSettings.FromCancellationToken(cancellationToken));
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
            return await GameBoardArrived(message, cancellationToken) ?
                SubscriberClient.Reply.Ack : SubscriberClient.Reply.Nack;
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken) =>
            _subscriberClient.StartAsync(
                (message, token) => ProcessOneMessage(message, token));

        Task IHostedService.StopAsync(CancellationToken cancellationToken) =>
            _subscriberClient.StopAsync(cancellationToken);
     }
}