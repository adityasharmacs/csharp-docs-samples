using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Google.Storage.V1;

namespace GoogleCloudSamples
{
    class Issue43
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine($"Usage:\n{System.AppDomain.CurrentDomain.FriendlyName} YOUR-PROJECT-ID");
                return -1;
            }
            string projectId = args[0];
            string bucketName = RandomBucketName();
            var storage = StorageClient.Create();
            storage.CreateBucket(projectId, bucketName);
            try
            {
                Console.WriteLine($"Created {bucketName}.");
                var bucket = storage.GetBucket(bucketName);
                if (bucket.Acl != null)
                {
                    foreach (var acl in bucket.Acl)
                    {
                        Console.WriteLine($"{acl.Role}:{acl.Entity}");
                    }
                }
            }
            finally
            {
                storage.DeleteBucket(bucketName);
            }
            return 0;
        }

        private static string RandomBucketName()
        {
            using (RandomNumberGenerator rng = new RNGCryptoServiceProvider())
            {
                string legalChars = "abcdefhijklmnpqrstuvwxyz";
                byte[] randomByte = new byte[1];
                var randomChars = new char[20];
                int nextChar = 0;
                while (nextChar < randomChars.Length)
                {
                    rng.GetBytes(randomByte);
                    if (legalChars.Contains((char)randomByte[0]))
                        randomChars[nextChar++] = (char)randomByte[0];
                }
                return new string(randomChars);
            }
        }
    }
}

