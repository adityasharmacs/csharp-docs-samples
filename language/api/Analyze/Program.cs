// Copyright(c) 2016 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy of
// the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations under
// the License.
using System;
using Google.Apis.CloudNaturalLanguageAPI.v1beta1;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.CloudNaturalLanguageAPI.v1beta1.Data;

namespace Analyze
{
    class Program
    {
        public static CloudNaturalLanguageAPIService 
            CreateNaturalLanguageAPIClient()
        {
            var credentials = 
                GoogleCredential.GetApplicationDefaultAsync().Result;
            if (credentials.IsCreateScopedRequired)
            {
                credentials = credentials.CreateScoped(new[] 
                {
                    CloudNaturalLanguageAPIService.Scope.CloudPlatform
                });
            }
            var serviceInitializer = new BaseClientService.Initializer()
            {
                ApplicationName = "NL Sample",
                HttpClientInitializer = credentials
            };
            return new CloudNaturalLanguageAPIService(serviceInitializer);
        }

        static void AnalyzeEntities(string text, string encoding="UTF16")
        {
            var service = CreateNaturalLanguageAPIClient();
            var response = service.Documents.AnalyzeEntities(
                new AnalyzeEntitiesRequest()
            {
                Document = new Document()
                {
                    Content = text,
                    Type = "PLAIN_TEXT"
                },
                EncodingType = "UTF16"
            }).Execute();
            string entity_separator = "";
            foreach (var entity in response.Entities)
            {
                Console.WriteLine($"Name: {entity.Name}");
                Console.WriteLine($"Type: {entity.Type}");
                Console.WriteLine($"Salience: {entity.Salience}");
                Console.WriteLine("Mentions:");
                foreach(var mention in entity.Mentions)
                    Console.WriteLine($"\t{mention.Text.BeginOffset}: {mention.Text.Content}");
                Console.WriteLine("Metadata:");
                foreach (var keyval in entity.Metadata)
                    Console.WriteLine($"\t{keyval.Key}: {keyval.Value}");
                Console.Write(entity_separator);
                entity_separator = "\n";
            }
        }

        static void Main(string[] args)
        {
            AnalyzeEntities("The rain in Spain stays mainly in the plain.");
        }
    }
}
