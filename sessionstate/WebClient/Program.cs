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
        static async Task TaskMainAsync(Uri baseAddress, int delayInMilliseconds)
        {
            // Create a client with a cookie jar.
            var cookieJar = new CookieContainer();
            // Create 100 HTTP clients, to simulate 100 browsers hitting the website.
            var handler = new HttpClientHandler()
            {
                CookieContainer = cookieJar
            };
            HttpClient client = new HttpClient(handler) { BaseAddress = baseAddress };

            // Add 10 session vars:
            for (int i = 0; i < 10; ++i)
            {
                string content = new string((char)('A' + i), 40 * (i + 1));
                await client.PutAsync($"Home/S/{i}", new StringContent(content));
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
                    await client.PutAsync($"Home/S/{sessionVarId}", new StringContent(content));
                }
                else
                {
                    await client.GetAsync($"Home/S/{sessionVarId}");
                }
            }
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
            var tasks = new Task[options.ClientCount];
            stopwatch.Start();
            for (int i = 0; i < tasks.Length; ++i)
            {
                tasks[i] = Task.Run(async () => await TaskMainAsync(baseAddress, options.Delay));
            }
            Task.WaitAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);
            return 0;
        }
    }
}
