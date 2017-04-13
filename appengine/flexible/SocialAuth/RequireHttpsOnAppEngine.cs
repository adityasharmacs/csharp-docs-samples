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

namespace SocialAuth
{
    public class RequireHttpsOnAppEngine : IAuthorizationFilter
    {
        static PathString s_healthCheckPathString = new PathString("/_ah/health");

        void IAuthorizationFilter.OnAuthorization(AuthorizationFilterContext context)
        {
            var proto = context.HttpContext.Request.Headers["X-Forwarded-Proto"];
            if (proto.FirstOrDefault() == "https")
            {
                return;  // Using https like they should.
            }
            if (context.HttpContext.Request.Path
                .StartsWithSegments(s_healthCheckPathString))
            {
                // Accept health checks from non-ssl connections.
                return;
            }
            // Redirect to https.
            string httpsPath = string.Format("https://{0}{1}{2}",
                context.HttpContext.Request.Host, context.HttpContext.Request.Path,
                context.HttpContext.Request.QueryString);
            context.Result = new RedirectResult(httpsPath);
        }
    }
}
