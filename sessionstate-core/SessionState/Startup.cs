// Copyright 2017 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SessionState
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
            services.AddMvc();
            services.AddOptions();
            string cache = Configuration["Cache"];

            // Add an implementation of IDistributedCache.
            switch (cache.ToLower())
            {
                case "datastore":
                    services.Configure<FirestoreDistributedCacheOptions>(
                        Configuration.GetSection("DatastoreCache"));
                    services.AddSingleton<IDistributedCache, FirestoreDistributedCache>();
                    break;
                case "redis":
                    services.Configure<RedisCacheOptions>(
                        Configuration.GetSection("RedisCache"));
                    services.AddDistributedRedisCache(options => { });
                    break;
                case "sqlserver":
                    services.Configure<SqlServerCacheOptions>(
                        Configuration.GetSection("SqlServerCache"));
                    services.AddDistributedSqlServerCache(options => { });
                    break;
                case "memory":
                    services.AddDistributedMemoryCache();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Cache", cache,
                        "Edit appsettings.json and set Cache to one of datastore, redis, sqlserver, or memory.");
            }

            services.AddSession(options =>
            {
                // Set a short timeout for easy testing.
                options.IdleTimeout = TimeSpan.FromSeconds(10);
                options.Cookie.HttpOnly = true;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseSession();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
