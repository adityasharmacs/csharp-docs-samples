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
using SudokuLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pubsub.Controllers
{
    public class HomeController : Controller
    {
        readonly PubsubOptions _options;
        readonly PublisherClient _publisher;
        readonly TopicName _topicName;

        static string s_boardA =
            "123|   |789" +
            "   |   |   " +
            "   |   |   " +
            "---+---+---" +
            "   |4  |   " +
            " 7 | 5 |   " +
            "   |  6| 2 " +
            "---+---+---" +
            "  1|   |   " +
            " 5 |  3|   " +
            "3  |   |1  ";


        public HomeController(IOptions<PubsubOptions> options, 
            PublisherClient publisher)
        {
            _options = options.Value;
            _publisher = publisher;
            _topicName = new TopicName(_options.ProjectId,
                    _options.TopicId);
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ActionName("Index")]
        public IActionResult IndexPost()
        {
            var pubsubMessage = new PubsubMessage()
            {
                Data = ByteString.CopyFromUtf8(GameBoard.Create(s_boardA).Board)
            };
            pubsubMessage.Attributes["token"] = _options.VerificationToken;
            _publisher.Publish(_topicName, new[] { pubsubMessage }); return View();
        }

        /// <summary>
        /// Handle a push request coming from pubsub.
        /// </summary>
        [HttpPost]
        [Route("/Push")]
        public IActionResult Push([FromBody]PushBody body)
        {
            if (body.message.attributes["token"] != _options.VerificationToken)
            {
                return new BadRequestResult();
            }
            var messageBytes = Convert.FromBase64String(body.message.data);
            string message = System.Text.Encoding.UTF8.GetString(messageBytes);
            var board = new SudokuLib.GameBoard();
            try
            {
                board.Board = message;
            }
            catch (ArgumentException)
            {
                return new BadRequestResult();
            }
            var request = new PublishRequest();
            request.TopicAsTopicName = _topicName;
            var nextMoves = board.FillNextEmptyCell();
            foreach (var move in nextMoves)
            {
                var nextMessage = new PubsubMessage();
                nextMessage.Attributes["token"] = _options.VerificationToken;
                nextMessage.Data = ByteString.CopyFromUtf8(move.Board);
                request.Messages.Add(nextMessage);
            }
            if (request.Messages.Count > 0)
            {
                _publisher.Publish(request);
            }
            return new OkResult();
        }

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
