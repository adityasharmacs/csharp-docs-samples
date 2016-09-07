// Copyright 2016 Google Inc.
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
using Google.Datastore.V1Beta3;
using Google.Protobuf;
using System;
using System.Linq;
using System.Collections.Generic;

namespace GoogleCloudSamples
{
    /**
     * A simple Task List application demonstrating how to connect to Cloud
     * Datastore, create, modify, delete, and query entities.
     */

    class TaskList
    {
        private readonly DatastoreDb _db;
        private readonly KeyFactory _keyFactory;

        // [START add_entity]
        /// <summary>
        ///  Adds a task entity to the Datastore
        /// </summary>
        /// <param name="description">The task description.</param>
        /// <returns>The key of the entity.</returns>
        Key AddTask(string description)
        {
            Key key = _db.AllocateId(_keyFactory.CreateIncompleteKey());
            Entity task = new Entity()
            {
                ["description"] = new Value()
                {
                    StringValue = description,
                    ExcludeFromIndexes = true
                },
                ["created"] = DateTime.UtcNow,
                ["done"] = false
            };
            return _db.Insert(task);
        }
        // [END add_entity]

        static void Main(string[] args)
        {
        }
    }
}
