using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace WebApp
{
    public static class WebApiConfig
    {
        public static string UrlPrefix = "api";
        public static string UrlPrefixRelative = "~/api";

        public static void Register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: WebApiConfig.UrlPrefix + "/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
