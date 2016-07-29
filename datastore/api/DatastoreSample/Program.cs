/*
 * Copyright (c) 2016 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */

/**
 * Sample code to issue several basic Datastore operations
 * using the Google Client Libraries.
 */

using System;
using Google.Datastore.V1Beta3;
using System.Linq;
using Google.Apis.Datastore.v1beta2;

namespace DatastoreSample
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1 || !new string[] { "apiary", "veneer" }.Contains(args[0].ToLower()))
            {
                Console.WriteLine("Usage: datastoresample apiary|veneer.");
                return -1;
            }
            var keyFilePath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            if (string.IsNullOrEmpty(keyFilePath))
            {
                Console.WriteLine("The environment variable GOOGLE_APPLICATION_CREDENTIALS is empty. " +
                    "Using credentials described by 'gcloud auth list'");
            }
            else
            {
                Console.WriteLine($"Using credentials from {keyFilePath}.");
            }
            var projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
            if (string.IsNullOrEmpty(projectId))
            {
                Console.WriteLine("Set the environment variable GOOGLE_PROJECT_ID to your project id.");
                return -1;
            }

            if (args[0].ToLower() == "veneer")
            {
                var db = DatastoreDb.Create(projectId);
                var query = new Query("Book") { Limit = 1 };
                foreach (var entity in db.RunQuery(query))
                {
                    foreach (var prop in entity.Properties)
                    {
                        Console.WriteLine($"{prop.Key}: {prop.Value.StringValue}");
                    }
                }
            }
            else
            {
                var credentials = Google.Apis.Auth.OAuth2.GoogleCredential
                    .GetApplicationDefaultAsync().Result;
                if (credentials.IsCreateScopedRequired)
                {
                    credentials = credentials.CreateScoped(new[] {
                    DatastoreService.Scope.Datastore,
                });
                }
                // Create our connection to datastore.
                var datastore = new DatastoreService(new Google.Apis.Services
                    .BaseClientService.Initializer()
                {
                    HttpClientInitializer = credentials,
                });

                var query = new Google.Apis.Datastore.v1beta2.Data.Query()
                {
                    Limit = 1,
                    Kinds = new[] { new Google.Apis.Datastore.v1beta2.Data.KindExpression() { Name = "Book" } },
                };

                var datastoreRequest = datastore.Datasets.RunQuery(
                    datasetId: projectId,
                    body: new Google.Apis.Datastore.v1beta2.Data.RunQueryRequest() { Query = query }
                );

                var response = datastoreRequest.Execute();
                var results = response.Batch.EntityResults;
                foreach (var entity in results)
                {
                    foreach (var prop in entity.Entity.Properties)
                    {
                        Console.WriteLine($"{prop.Key}: {prop.Value.StringValue}");
                    }
                }
            }
            return 0;
        }
    }
}