// Copyright 2017 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SessionState.Models;

namespace SessionState.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public ActionResult S(string id = null)
        {
            if (id == null)
            {
                ViewBag.Keys = HttpContext.Session.Keys;
                return View();
            }
            return Content(HttpContext.Session.GetString(id));
        }

        [HttpPost]
        public IActionResult S(Models.SessionVariable svar)
        {
            HttpContext.Session.SetString(svar.Key, svar.Value);
            ViewBag.Keys = HttpContext.Session.Keys;
            if (svar.Silent.HasValue && (bool)svar.Silent)
                return new EmptyResult();
            return View();
        }
    }
}
