using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using RedisCache.ViewModels;

namespace RedisCache.Controllers
{
    public class HomeController : Controller
    {
        private IDistributedCache _cache;
        public HomeController(IDistributedCache cache)
        {
            _cache = cache;
        }

        [HttpGet]
        [HttpPost]
        public IActionResult Index(WhoForm whoForm)
        {
            var model = new WhoCount()
            {
                Who = _cache.GetString("who") ?? "",
                Count = int.Parse(_cache.GetString("count") ?? "0"),
            };
            if (ModelState.IsValid && HttpContext.Request.Method.ToUpper() == "POST")
            {
                model.Who = whoForm.Who;
                model.Count += 1;
                _cache.SetString("who", model.Who ?? "");
                _cache.SetString("count", (model.Count).ToString());
            }
            return View(model);
        }

        [HttpPost]
        public IActionResult Reset()
        {
            var model = new WhoCount()
            {
                Who = "",
                Count = 0,
            };
            _cache.SetString("who", "");
            _cache.SetString("count", "0");
            return View("/Views/Home/Index.cshtml", model);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
