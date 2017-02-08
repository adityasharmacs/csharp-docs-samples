using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Google.Cloud.Diagnostics.AspNetCore;
using Microsoft.Extensions.Configuration;

namespace Logging
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            if (env.IsDevelopment())
            {
                loggerFactory.AddConsole();
            }
            else
            {
                loggerFactory.AddGoogle(Configuration["GOOGLE_PROJECT_ID"]);
            }

            app.Run(async (context) =>
            {
                if (Configuration["GOOGLE_PROJECT_ID"] == "YOUR-PROJECT-ID")
                {
                    await context.Response.WriteAsync(@"
                    <html>
                    <head><title>Error</title></head>
                    <body>Set the environment variable GOOGLE_PROJECT_ID to your project id in app.yaml.</body>
                    </html>");
                    return;
                }
                loggerFactory.CreateLogger("Home").LogInformation("Greeted user with Hello World");
                await context.Response.WriteAsync(@"
                    <html>
                    <head><title>Hello Google Cloud Diagnostics</title></head>
                    <body>Hello World!</body>
                    </html>");
            });
        }
    }
}
