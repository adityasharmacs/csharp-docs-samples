using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        [HttpGet]
        public ActionResult S(string id = null)
        {
            if (id == null)
            {
                ViewBag.Keys = Session.Keys;
                return View();
            }

            return Content((string) Session[id]);
        }

        [HttpPost]
        public ActionResult S(Models.SessionVariable svar)
        {
            Session[svar.Key] = svar.Value;
            ViewBag.Keys = Session.Keys;
            return View();
        }

        [HttpPut]
        public ActionResult S(string id, [System.Web.Http.FromBody]string value)
        {
            return new EmptyResult();
        }
    }
}