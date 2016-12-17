// Copyright 2016 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace WebClient
{
    class Options
    {
        [Option('d', "delay", DefaultValue = 1000, HelpText = "Milliseconds to delay between page fetches.")]
        public int Delay { get; set; }

        [Option('c', "clients", DefaultValue = 100, HelpText = "Number of HTTP clients.")]
        public int ClientCount { get; set; }

        [Option('u', "baseUri", Required = true, HelpText = "The base url running the WebApp.")]
        public string BaseUri { get; set; }
    }

    class Program
    {
        // Returns the average page fetch time.
        static async Task<double> TaskMainAsync(Uri baseAddress, int delayInMilliseconds)
        {
            var handler = new HttpClientHandler()
            {
                CookieContainer = new CookieContainer()
            };
            HttpClient client = new HttpClient(handler) { BaseAddress = baseAddress };
            Stopwatch stopwatch = new Stopwatch();
            // Add 10 session vars:
            for (int i = 0; i < 10; ++i)
            {
                string content = new string((char)('A' + i), 40 * (i + 1));
                stopwatch.Start();
                await client.PutAsync($"Home/S/{i}", new StringContent(content));
                stopwatch.Stop();
                await Task.Delay(delayInMilliseconds);
            }
            // Read and write the session vars a bunch of times.
            uint contentChar = 'A';
            for (int i = 0; i < 50; ++i)
            {
                await Task.Delay(delayInMilliseconds);
                int sessionVarId = i % 10;
                if (i % 3 == 0)
                {
                    string content = new string((char)(contentChar), 40 * (sessionVarId + 1));
                    contentChar = contentChar == 'Z' ? 'A' : contentChar + 1;
                    stopwatch.Start();
                    await client.PutAsync($"Home/S/{sessionVarId}", new StringContent(content));
                    stopwatch.Stop();
                }
                else
                {
                    stopwatch.Start();
                    await client.GetAsync($"Home/S/{sessionVarId}");
                    stopwatch.Stop();
                }
            }
            return stopwatch.ElapsedMilliseconds / 60.0;
        }

        static int Main(string[] args)
        {
            var options = new Options();
            var parsed = Parser.Default.ParseArguments(args, options);
            if (!parsed)
            {
                Console.WriteLine(
                    HelpText.AutoBuild(options).RenderParsingErrorsText(options, 0));
                return -1;
            }
            Uri baseAddress = new Uri(options.BaseUri);
            var stopwatch = new Stopwatch();
            var tasks = new Task<double>[options.ClientCount];
            stopwatch.Start();
            for (int i = 0; i < tasks.Length; ++i)
            {
                tasks[i] = Task.Run(async () => await TaskMainAsync(baseAddress, options.Delay));
            }
            Task.WaitAll(tasks);
            stopwatch.Stop();
            Console.WriteLine("Total elapsed seconds: {0}", stopwatch.ElapsedMilliseconds / 1000.0);
            var averagePageFetchTime = tasks.Select((task) => task.Result).Average();
            Console.WriteLine("Average page fetch time in milliseconds: {0}", averagePageFetchTime);
            return 0;
        }
    }
}
