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
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> Index(SendForm sendForm)
        {
            var model = new HomeIndex();
            if (ModelState.IsValid && HttpContext.Request.Method.ToUpper() == "POST")
            {
                model.Recipient = sendForm.Recipient ?? "";
                model.sendGridResponse = await CallSendGrid(sendForm.Recipient);
            }
            return View(model);
        }


        Task<HttpResponseMessage> CallSendGrid(string recipient)
        {
            var request = new
            {
                personalizations = new[] {
                    new {
                        to = new[]
                        {
                            new {email = recipient}
                        },
                        subject = "Hello World!"
                    }
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
            var sendgridApiKey = @"";
            HttpClient sendgrid3 = new HttpClient();
            sendgrid3.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sendgridApiKey);
            string jsonRequest = JsonConvert.SerializeObject(request);
            return sendgrid3.PostAsync("https://api.sendgrid.com/v3/mail/send",
                new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json"));
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
