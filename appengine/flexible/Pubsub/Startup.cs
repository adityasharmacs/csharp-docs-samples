/*
 * Copyright (c) 2017 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace Pubsub
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddOptions();
            services.Configure<PubsubOptions>(
                Configuration.GetSection("Pubsub"));
            services.AddMvc();
            services.AddSingleton((provider) =>
                Google.Cloud.PubSub.V1.PublisherClient.Create());
            services.AddSingleton((provider) =>
                Google.Cloud.PubSub.V1.SubscriberClient.Create());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            CreateTopicAndSubscription(app.ApplicationServices);
        }

        void CreateTopicAndSubscription(System.IServiceProvider provider)
        {
            var options = provider.GetService<IOptions<PubsubOptions>>().Value;
            Debug.Assert(options.ProjectId != "your-project-id", 
                "Set ProjectId to your Google project id in appsettings.json");
            var topicName = new Google.Cloud.PubSub.V1.TopicName(
                    options.ProjectId, options.TopicId);
            try
            {
                provider.GetService<Google.Cloud.PubSub.V1.PublisherClient>().CreateTopic(topicName);
            }
            catch (Grpc.Core.RpcException e) when (e.Status.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            {
            }
            var subscriptionName = new Google.Cloud.PubSub.V1.SubscriptionName(
                    options.ProjectId, options.SubscriptionId);
            var pushConfig = new Google.Cloud.PubSub.V1.PushConfig()
            {
                PushEndpoint = $"https://{options.ProjectId}.appspot.com/Push"
            };
            try
            {
                provider.GetService<Google.Cloud.PubSub.V1.SubscriberClient>()
                    .CreateSubscription(subscriptionName, topicName, pushConfig, 20);
            }
            catch (Grpc.Core.RpcException e) when (e.Status.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            {
                provider.GetService<Google.Cloud.PubSub.V1.SubscriberClient>()
                    .ModifyPushConfig(subscriptionName, pushConfig);
            }
        }
    }
}
