using Google.Cloud.Diagnostics.AspNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace TraceMVC
{
    public class MvcApplication : System.Web.HttpApplication
    {
        //
        // Summary:
        //     Executes custom initialization code after all event handler modules have been
        //     added.
        public override void Init()
        {
            base.Init();
            CloudTrace.Initialize(this, "surferjeff-phoenix");
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
