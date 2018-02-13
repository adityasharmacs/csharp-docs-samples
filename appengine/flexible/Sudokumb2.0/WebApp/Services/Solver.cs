using Google.Cloud.PubSub.V1;
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
        }

        public TopicName Topic
        {
             get
             {
                 var opts = options_.Value;
                 return new TopicName(opts.ProjectId, opts.TopicId);
             }
        }

        public SubscriptionName Subscription
        {
             get
             {
                 var opts = options_.Value;
                 return new SubscriptionName(opts.ProjectId,
                    opts.SubscriptionId);
             }
        }
    }
}