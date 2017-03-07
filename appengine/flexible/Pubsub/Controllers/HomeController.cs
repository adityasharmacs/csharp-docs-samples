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

using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pubsub.ViewModels;
using System;
using System.Collections.Generic;

namespace Pubsub.Controllers
{
    public class HomeController : Controller
    {
        readonly PubsubOptions _pubsubOptions;
        // See Startup.cs to see how the publisher and subscriber are
        // instantiated.
        readonly PublisherClient _publisher;
        readonly SubscriberClient _subscriber;
        // Keep the received messages in a list.
        static List<string> s_receivedMessages = new List<string>();
        static object s_receivedMessagesLock = new object();

        public HomeController(IOptions<PubsubOptions> options,
            PublisherClient publisher,
            SubscriberClient subscriber)
        {
            _pubsubOptions = options.Value;
            _publisher = publisher;
            _subscriber = subscriber;            
        }

        // [START index]
        [HttpGet]
        [HttpPost]
        public IActionResult Index(MessageForm messageForm)
        {
            var model = new MessageList();
            if (!string.IsNullOrEmpty(messageForm.Message))
            {
                // Publish the message.
                var topicName = new TopicName(_pubsubOptions.ProjectId, 
                    _pubsubOptions.TopicId);
                var pubsubMessage = new PubsubMessage()
                {
                    Data = ByteString.CopyFromUtf8(messageForm.Message)
                };
                pubsubMessage.Attributes["token"] = _pubsubOptions.VerificationToken;
                _publisher.Publish(topicName, new[] { pubsubMessage });
                model.PublishedMessage = messageForm.Message;
            }
            // Render the current list of messages.
            lock (s_receivedMessagesLock)
            {
                model.Messages = s_receivedMessages.ToArray();
            }
            return View(model);
        }
        // [END index]

        // [START push]
        /// <summary>
        /// Handle a push request coming from pubsub.
        /// </summary>
        [HttpPost]
        [Route("/Push")]
        public IActionResult Push([FromBody]PushBody body)
        {
            if (body.message.attributes["token"] != _pubsubOptions.VerificationToken)
            {
                return new BadRequestResult();
            }
            var messageBytes = Convert.FromBase64String(body.message.data);
            string message = System.Text.Encoding.UTF8.GetString(messageBytes);
            lock (s_receivedMessages)
            {
                s_receivedMessages.Add(message);
            }
            return new OkResult();
        }
        // [END push]

        public IActionResult Error()
        {
            return View();
        }
    }

    /// <summary>
    /// Pubsub messages will arrive in this format.
    /// </summary>
    public class PushBody
    {
        public PushMessage message { get; set; }
        public string subscription { get; set; }
    }

    public class PushMessage
    {
        public Dictionary<string, string> attributes { get; set; }
        public string data { get; set; }
        public string message_id { get; set; }
        public string publish_time { get; set; }
    }

}
