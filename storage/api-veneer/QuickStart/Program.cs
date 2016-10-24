using Google.Apis.Storage.v1.Data;
using Google.Storage.V1;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace GoogleCloudSamples
{
    public class QuickStart
    {
        private static readonly string s_projectId = "bookshelf-dotnet"; // "YOUR-PROJECT-ID";

        private static readonly string s_usage =
                "Usage: \n" +
                "  QuickStart create [new-bucket-name]\n" +
                "  QuickStart list\n" +
                "  QuickStart list bucket-name [prefix] [delimiter]\n" +
                "  QuickStart upload bucket-name local-file-path [object-name]\n" +
                "  QuickStart delete bucket-name\n" +
                "  QuickStart delete bucket-name object-name\n";

        // [START storage_create_bucket]
        private static void CreateBucket(string bucketName)
        {
            var storage = StorageClient.Create();
            if (bucketName == null)
                bucketName = RandomBucketName();
            storage.CreateBucket(s_projectId, new Bucket { Name = bucketName });
            Console.WriteLine($"Created {bucketName}.");
        }
        // [END storage_create_bucket]

        // [START storage_list_buckets]
        private static void ListBuckets()
        {
            var storage = StorageClient.Create();
            foreach (var bucket in storage.ListBuckets(s_projectId))
            {
                Console.WriteLine(bucket.Name);
            }
        }
        // [END storage_list_buckets]

        // [START storage_delete_bucket]
        private static void DeleteBucket(string bucketName)
        {
            var storage = StorageClient.Create();
            storage.DeleteBucket(new Bucket { Name = bucketName });
            Console.WriteLine($"Deleted {bucketName}.");
        }
        // [END storage_delete_bucket]

        // [START storage_list_files]
        private static void ListObjects(string bucketName)
        {
            var storage = StorageClient.Create();
            foreach (var bucket in storage.ListObjects(bucketName, ""))
            {
                Console.WriteLine(bucket.Name);
            }
        }
        // [END storage_list_files]

        // [START storage_list_files_with_prefix]
        private static void ListObjects(string bucketName, string prefix,
            string delimiter)
        {
            var storage = StorageClient.Create();
            var options = new ListObjectsOptions() { Delimiter = delimiter };
            foreach (var storageObject in storage.ListObjects(
                bucketName, prefix, options))
            {
                Console.WriteLine(storageObject.Name);
            }
        }
        // [END storage_list_files_with_prefix]

        // [START storage_upload_file]
        private static void UploadFile(string bucketName, string localPath,
            string objectName = null)
        {
            var storage = StorageClient.Create();
            using (var f = File.OpenRead(localPath))
            {
                var storageObject = new Google.Apis.Storage.v1.Data.Object
                {
                    Bucket = bucketName,
                    Name = objectName ?? Path.GetFileName(localPath)
                };
                storage.UploadObject(storageObject, f);
                Console.WriteLine($"Uploaded {storageObject.Name}.");
            }
        }
        // [END storage_upload_file]

        // [START storage_delete_file]
        private static void DeleteObject(string bucketName, string objectName)
        {
            var storage = StorageClient.Create();
            storage.DeleteObject(new Google.Apis.Storage.v1.Data.Object()
            {
                Bucket = bucketName,
                Name = objectName,
            });
            Console.WriteLine($"Deleted {objectName}.");
        }
        // [END storage_delete_file]

        private static void NukeBucket(string bucketName)
        {
            var storage = StorageClient.Create();
            foreach (var storageObject in storage.ListObjects(bucketName, ""))
            {
                storage.DeleteObject(new Google.Apis.Storage.v1.Data.Object()
                {
                    Bucket = bucketName,
                    Name = storageObject.Name,
                });
                Console.WriteLine($"Deleted {storageObject.Name}.");
            }
            storage.DeleteBucket(new Bucket { Name = bucketName });
            Console.WriteLine($"Deleted {bucketName}.");
        }

        public static bool PrintUsage()
        {
            Console.WriteLine(s_usage);
            return true;
        }

        public static int Main(string[] args)
        {
            if (args.Length < 1 && PrintUsage()) return -1;
            try
            {
                switch (args[0].ToLower())
                {
                    case "create":
                        CreateBucket(args.Length < 2 ? null : args[1]);
                        break;

                    case "list":
                        if (args.Length < 2)
                            ListBuckets();
                        else if (args.Length < 3)
                            ListObjects(args[1]);
                        else
                            ListObjects(args[1], args[2], 
                                args.Length < 4 ? null : args[3]);
                        break;

                    case "delete":
                        if (args.Length < 2 && PrintUsage()) return -1;
                        if (args.Length < 3)
                        {
                            DeleteBucket(args[1]);
                        }
                        else
                        {
                            DeleteObject(args[1], args[2]);
                        }
                        break;

                    case "upload":
                        if (args.Length < 3 && PrintUsage()) return -1;
                        UploadFile(args[1], args[2], args.Length < 4 ? null : args[3]);
                        break;

                    case "nuke":
                        if (args.Length < 2 && PrintUsage()) return -1;
                        NukeBucket(args[1]);
                        break;

                    default:
                        PrintUsage();
                        return -1;
                }
                return 0;
            }
            catch (Google.GoogleApiException e)
            {
                Console.WriteLine(e.Message);
                return e.Error.Code;
            }
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
