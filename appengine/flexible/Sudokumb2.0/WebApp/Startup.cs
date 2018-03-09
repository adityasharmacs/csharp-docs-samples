// Copyright (c) 2018 Google LLC.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy of
// the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations under
// the License.

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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Rewrite;

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
            services.Configure<PubsubGameBoardQueueOptions>(
                Configuration.GetSection("Google"));
            services.AddSingleton<DatastoreDb>(provider => DatastoreDb.Create(
                Configuration["Google:ProjectId"],
            Configuration["Google:NamespaceId"] ?? ""));
            services.Configure<KmsDataProtectionProviderOptions>(
                Configuration.GetSection("Google"));
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddDefaultTokenProviders();
            services.AddTransient<IUserStore<ApplicationUser>,
                DatastoreUserStore<ApplicationUser>>();
            services.AddTransient<IUserRoleStore<ApplicationUser>,
                DatastoreUserStore<ApplicationUser>>();
            services.AddTransient<IRoleStore<IdentityRole>,
                DatastoreRoleStore<IdentityRole>>();
            services.AddDatastoreCounter();
            services.AddSingleton<SolveStateStore>();
            services.AddSingleton<IGameBoardQueue, PubsubGameBoardQueue>();
            services.AddAdminSettings();
            services.AddTransient<IEmailSender, EmailSender>();
            services.AddSingleton<IDataProtectionProvider,
                KmsDataProtectionProvider>();

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

            // Configure redirects to HTTPS.
            var instance = Google.Api.Gax.Platform.Instance();
            var rewriteOptions = new RewriteOptions();
            if (null == instance.GaeDetails)
            {
                rewriteOptions.AddRedirectToHttps(302, 44393);
            }
            else
            {
                rewriteOptions.Add(new RewriteHttpsOnAppEngine(
                    HttpsPolicy.Required));
            }
            app.UseRewriter(rewriteOptions);

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
