using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Datastore.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sudokumb;

namespace WebSolver
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.Configure<SolverOptions>(
                Configuration.GetSection("Google"));
            services.AddSingleton<DatastoreDb>(provider => DatastoreDb.Create(
                Configuration["Google:ProjectId"],
                Configuration["Google:NamespaceId"] ?? ""));
            services.AddSingleton<SolveStateStore, SolveStateStore>();
            services.AddSingleton<IHostedService, Solver>();
            services.AddSingleton<IDumb, AdminSettings>();
            services.AddSingleton<ICounter, InterlockedCounter>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.Run(async (context) =>
            {
                long count = app.ApplicationServices
                    .GetService<ICounter>().Count;
                await context.Response.WriteAsync(string.Format(
                    "Examined {0} sudoku boards.", count));
            });
        }
    }
}
