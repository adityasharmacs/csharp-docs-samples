using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Storage.V1;
using Google.Apis.Storage.v1.Data;

namespace GoogleCloudSamples
{
    class QuickStart
    {
        private static string _usage =
                "Usage: \n" +
                "  QuickStart <new-bucket-name>";
        static void Main(string[] args)
        {
            var storage = StorageClient.Create();
            storage.CreateBucket("YOUR-PROJECT-ID", new Bucket
            {
                Name = args.Length > 0 ? args[1] : "my-new-bucket"
            });
        }
    }
}
