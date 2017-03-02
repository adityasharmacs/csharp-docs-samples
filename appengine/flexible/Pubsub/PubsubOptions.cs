using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pubsub
{
    public class PubsubOptions
    {
        public string VerificationToken { get; set; }
        public string TopicId { get; set; }
        public string SubscriptionId { get; set; }
        public string ProjectId { get; set; }
    }
}
