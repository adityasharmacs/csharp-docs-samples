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
using System.Linq;
using Microsoft.AspNetCore.Rewrite;

namespace SocialAuth
{
    public class RedirectToHttpsOnAppEngine : IRule
    {
        /// <summary>
        /// Redirects requests to https.  Also examines X-Forwarded-Proto 
        /// header.
        /// </summary>
        /// <returns>
        /// A RedirectResult if the request needs to be redirected to https.
        /// Otherwise null.
        /// </returns>
        public static RedirectResult Redirect(HttpRequest request)
        {
            if (request.Scheme == "https" || request.Headers["X-Forwarded-Proto"]
                .FirstOrDefault() == "https")
            {
                return null;  // Already https.
            }

            // Redirect to https.
            var newUrl = string.Concat(
                                "https://",
                                request.Host.ToUriComponent(),
                                request.PathBase.ToUriComponent(),
                                request.Path.ToUriComponent(),
                                request.QueryString.ToUriComponent());
            return new RedirectResult(newUrl);
        }

        void IRule.ApplyRule(RewriteContext context)
        {
            RedirectResult redirect = Redirect(context.HttpContext.Request);
            if (redirect == null)
            {
                context.Result = RuleResult.ContinueRules;
            }
            else
            {
                // Execute the redirect.
                ActionContext actionContext = new ActionContext()
                {
                    HttpContext = context.HttpContext
                };
                redirect.ExecuteResult(actionContext);
                context.Result = RuleResult.EndResponse;
            }
        }
    }
}
