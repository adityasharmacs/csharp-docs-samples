using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) => 
                {
                    var env = context.HostingEnvironment;
                    config.AddJsonFile("appsecrets.json", optional: true, reloadOnChange: true);
                    config.AddJsonFile($"appsecrets.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                }).UseStartup<Startup>()
                .Build();
    }
}
