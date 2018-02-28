using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Models;
using WebApp.Services;
using Sudokumb;
using Google.Cloud.Datastore.V1;
using Microsoft.Extensions.Hosting;
using System.Runtime.CompilerServices;

namespace WebApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.Configure<Models.AccountViewModels.AccountOptions>(
                Configuration.GetSection("Account"));
            services.Configure<SolverOptions>(
                Configuration.GetSection("Google"));
            services.AddSingleton<DatastoreDb>(provider => DatastoreDb.Create(
                Configuration["Google:ProjectId"],
                Configuration["Google:NamespaceId"] ?? ""));
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddDefaultTokenProviders();
            services.AddTransient<IUserStore<ApplicationUser>,
                DatastoreUserStore<ApplicationUser>>();
            services.AddTransient<IUserRoleStore<ApplicationUser>,
                DatastoreUserStore<ApplicationUser>>();
            services.AddTransient<IRoleStore<IdentityRole>,
                DatastoreRoleStore<IdentityRole>>();
            services.AddSingleton<SolveStateStore, SolveStateStore>();
            services.AddDatastoreCounter();
            services.AddSingleton<ISolveRequester, Solver>();
            services.AddSingleton<AdminSettings, AdminSettings>();
            services.AddTransient<IEmailSender, EmailSender>();

            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
