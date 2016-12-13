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
        static async Task TaskMainAsync(HttpClient client)
        {
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
                int sessionVarId = i % 10;
                if (i % 3 == 0)
                {
                    await Task.Delay(1000);
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
            // Create a client with a cookie jar.
            var baseAddress = new Uri(args[0]);
            var cookieJar = new CookieContainer();
            // Create 100 HTTP clients, to simulate 100 browsers hitting the website.
            HttpClient[] clients = new HttpClient[100];
            for (int i = 0; i < clients.Length; ++i)
            {
                var handler = new HttpClientHandler()
                {
                    CookieContainer = cookieJar
                };
                clients[i] = new HttpClient(handler) { BaseAddress = baseAddress };
            }
            var stopwatch = new Stopwatch();
            Task[] tasks = new Task[clients.Length];
            stopwatch.Start();
            for (int i = 0; i < tasks.Length; ++i)
            {
                int task = i;
                tasks[task] = Task.Run(async () => await TaskMainAsync(clients[task]));
            }
            Task.WaitAll(tasks);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);
            // Console.Write(client.GetAsync("Home/S").Result.Content.ReadAsStringAsync().Result);
        }
    }
}
