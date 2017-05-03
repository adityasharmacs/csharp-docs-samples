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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;

namespace SocialAuth
{
    public class RequireHttpsOnAppEngine : IActionFilter
    {
        static PathString s_healthCheckPathString = new PathString("/_ah/health");

        readonly ILogger _logger;

        public RequireHttpsOnAppEngine(ILogger logger)
        {
            _logger = logger;
        }

        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {
            // Do nothing.
        }

        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            string myUrl = string.Concat(
                                    request.Scheme, "://",
                                    request.Host.ToUriComponent(),
                                    request.PathBase.ToUriComponent(),
                                    request.Path.ToUriComponent(),
                                    request.QueryString.ToUriComponent());
            string proto = request.Headers["X-Forwarded-Proto"]
                .FirstOrDefault();
            if (proto == "https")
            {
                _logger.LogInformation("OnAuthorization({0}); X-Forwarded-Proto: {1}", myUrl, proto);
                request.IsHttps = true;
                request.Scheme = "https";
                return;  // Using https like they should.
            }
            if (request.Path.StartsWithSegments(s_healthCheckPathString))
            {
                // Accept health checks from non-ssl connections.
                return;
            }

            // Redirect to https
            _logger.LogInformation("OnAuthorization({0}); X-Forwarded-Proto: {1}", myUrl, proto);
            var newUrl = string.Concat(
                                "https://",
                                request.Host.ToUriComponent(),
                                request.PathBase.ToUriComponent(),
                                request.Path.ToUriComponent(),
                                request.QueryString.ToUriComponent());
            context.Result = new RedirectResult(newUrl);
        }
    }

    public class RequireHttpsOnAppEngineAttribute : Attribute, IFilterFactory
    {
        bool IFilterFactory.IsReusable
        {
            get
            {
                return false;
            }
        }

        IFilterMetadata IFilterFactory.CreateInstance(IServiceProvider serviceProvider)
        {
            var loggerFactory = (ILoggerFactory) serviceProvider.GetService(typeof(ILoggerFactory));
            return new RequireHttpsOnAppEngine(loggerFactory.CreateLogger<RequireHttpsOnAppEngine>());
        }
    }
}
