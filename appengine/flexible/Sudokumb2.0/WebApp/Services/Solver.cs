using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace WebApp.Services
{
    class SolverOptions
    {
        public string ProjectId { get; set; }
        public string SubscriptionId { get; set; } = "sudokumb";
        public string TopicId { get; set; } = "sudokumb";
    }
    class Solver
    {
        readonly PublisherServiceApiClient publisher_;
        readonly SubscriberServiceApiClient subscriber_;
        readonly IOptions<SolverOptions> options_;

        public Solver(PublisherServiceApiClient publisher,
            SubscriberServiceApiClient subscriber,
            IOptions<SolverOptions> options)
        {
            publisher_ = publisher;
            subscriber_ = subscriber;
            options_ = options;

            // Create the Topic and Subscription.
            try
            {
                publisher_.CreateTopic(MyTopic);
            }
            catch (RpcException e)
            when (e.Status.StatusCode == StatusCode.AlreadyExists)
            {
                // Already exists.  That's fine.
            }

            try
            {
                subscriber_.CreateSubscription(MySubscription, MyTopic,
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

        public async Task PullAndSolveLoopAsync(CancellationToken cancelationToken)
        {


        }
    }
}