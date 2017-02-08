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
        public IActionResult Index()
        {
            var model = new WhoCount()
            {
                Who = _cache.GetString("who") ?? "",
                Count = int.Parse(_cache.GetString("count") ?? "0"),
            };
            return View(model);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
