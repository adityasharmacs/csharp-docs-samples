using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialAuth
{
    public class RequireHttpsOnAppEngine : IActionFilter
    {
        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {            
        }

        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            var proto = context.HttpContext.Request.Headers["X-Forwarded-Proto"];
            if (proto.FirstOrDefault() == "https")
            {
                return;  // Using https like they should.
            }
            // Redirect to https.
            string httpsPath = string.Format("https://{0}{1}{2}",
                context.HttpContext.Request.Host, context.HttpContext.Request.Path,
                context.HttpContext.Request.QueryString);
            context.Result = new RedirectResult(httpsPath);
        }
    }
}
