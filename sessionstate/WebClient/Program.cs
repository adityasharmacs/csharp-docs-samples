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
        static void Main(string[] args)
        {
            // Create a client with a cookie jar.
            var baseAddress = new Uri(args[0]);
            var cookieJar = new CookieContainer();
            var handler = new HttpClientHandler()
            {
                CookieContainer = cookieJar
            };
            var client = new HttpClient(handler) { BaseAddress = baseAddress };
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            // Add 20 session vars:
            for (int i = 0; i < 20; ++i)
            {
                string content = new string((char)('A' + i), 40 * (i+1));
                client.PutAsync($"Home/S/{i}", new StringContent(content)).Wait();
            }
            // Read and write the session vars a bunch of times.
            uint contentChar = 'A';
            for (int i = 0; i < 5000; ++i)
            {
                int cookieId = i % 20;
                if (i % 3 == 0)
                {
                    string content = new string((char)(contentChar), 40 * (cookieId + 1));
                    contentChar = contentChar == 'Z' ? 'A' : contentChar + 1;
                    client.PutAsync($"Home/S/{cookieId}", new StringContent(content)).Wait();
                }
                else
                {
                    client.GetAsync($"Home/S/{cookieId}").Wait();
                }
            }
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);
            // Console.Write(client.GetAsync("Home/S").Result.Content.ReadAsStringAsync().Result);
        }
    }
}
