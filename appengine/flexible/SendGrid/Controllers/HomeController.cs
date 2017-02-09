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

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using SendGrid.ViewModels;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SendGrid.Controllers
{
    public class HomeController : Controller
    {
        private IDistributedCache _cache;
        public HomeController(IDistributedCache cache)
        {
            _cache = cache;
        }

        [HttpPost]
        public async Task<IActionResult> Index(SendForm sendForm)
        {
            var model = new HomeIndex();
            if (ModelState.IsValid)
            {
                var response = await CallSendGrid(sendForm.Recipient);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Index() => View(new HomeIndex());



        Task<HttpResponseMessage> CallSendGrid(string recipient)
        {
            var request = new
            {
                personalizations = new
                {
                    to = new[]
                    {
                        new {email = recipient}
                    },
                    subject = "Hello World!"
                },
                from = new
                {
                    email = "alice@example.com"
                },
                content = new[]
                {
                    new {
                        type = "text/plain",
                        value = "Hello, World!"
                    }
                }
            };
            HttpClient sendgrid3 = new HttpClient()
            {
                BaseAddress = new Uri("https://api.sendgrid.com/v3")
            };
            return sendgrid3.PostAsync("mail/send",
                new StringContent(JsonConvert.SerializeObject(request)));
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
