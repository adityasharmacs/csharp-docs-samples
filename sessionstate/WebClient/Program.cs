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
    class Program
    {
        static async Task TaskMainAsync(Uri baseAddress)
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
                await Task.Delay(1000);
            }
            // Read and write the session vars a bunch of times.
            uint contentChar = 'A';
            for (int i = 0; i < 50; ++i)
            {
                await Task.Delay(1000);
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

        static void Main(string[] args)
        {
            Uri baseAddress = new Uri(args[0]);
            var stopwatch = new Stopwatch();
            var tasks = new Task[100];
            stopwatch.Start();
            for (int i = 0; i < tasks.Length; ++i)
            {
                tasks[i] = Task.Run(async () => await TaskMainAsync(baseAddress));
            }
            Task.WaitAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);
            // Console.Write(client.GetAsync("Home/S").Result.Content.ReadAsStringAsync().Result);
        }
    }
}
