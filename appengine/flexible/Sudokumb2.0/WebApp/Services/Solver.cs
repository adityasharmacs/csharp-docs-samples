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
    public class Solver : IHostedService
    {
        readonly PublisherServiceApiClient publisherApi_;
        readonly PublisherClient publisherClient_;
        readonly SubscriberClient subscriberClient_;

        readonly SolveStateStore solveStateStore_;
        readonly ILogger<Solver> logger_;
        readonly IOptions<SolverOptions> options_;

        class Message
        {
            public string SolveRequestId { get; set; }
            public GameBoard Board { get; set; }
        }

        public Solver(IOptions<SolverOptions> options,
            SolveStateStore solveStateStore,
            ILogger<Solver> logger)
        {
            logger_ = logger;
            options_ = options;
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
            while (moves.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                GameBoard board = moves.Pop();
                if (!board.HasEmptyCell())
                {
                    // Solved!
                    await solveStateStore_.SetAsync(message.SolveRequestId,
                        board);
                    return SubscriberClient.Reply.Ack;
                }
                foreach (var move in board.FillNextEmptyCell())
                {
                    moves.Push(move);
                }
            }
            return SubscriberClient.Reply.Nack;
        }

        public async Task<string> StartSolving(GameBoard gameBoard)
        {
            // Create a new request and publish it to pubsub.
            var message = new Message()
            {
                SolveRequestId = Guid.NewGuid().ToString(),
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
            return message.SolveRequestId;
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