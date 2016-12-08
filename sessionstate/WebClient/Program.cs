using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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

            // Add 20 session vars:
            for (int i = 0; i < 20; ++i)
            {
                string content = new string((char)('A' + i), 40 * (i+1));
                client.PutAsync($"Home/S/{i}", new StringContent(content)).Wait();
            }
            // Read them a few times:
            for (int i = 0; i < 500; ++i)
            {
                client.GetAsync($"Home/S/{i % 20}").Wait();
            }
            // Console.Write(client.GetAsync("Home/S").Result.Content.ReadAsStringAsync().Result);
        }
    }
}
