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

        // [START update_entity]
        /// <summary>
        /// Marks a task entity as done.
        /// </summary>
        /// <param name="id">The ID of the task entity as given by Key.</param>
        /// <returns>true if the task was found.</returns>
        bool MarkDone(long id)
        {
            using (var transaction = _db.BeginTransaction())
            {
                Entity task = transaction.Lookup(_keyFactory.CreateKey(id));
                if (task != null)
                {
                    task["done"] = true;
                    transaction.Update(task);
                }
                transaction.Commit();
                return task != null;
            }
        }
        // [END update_entity]

        // [START retrieve_entities]
        /// <summary>
        /// Returns a list of all task entities in ascending order of creation time.
        /// </summary>
        IEnumerable<Entity> ListTasks()
        {
            Query query = new Query("Task")
            {
                Order = { { "created", PropertyOrder.Types.Direction.Descending } }
            };
            return _db.RunQuery(query);
        }
        // [END retrieve_entities]

        // [START delete_entity]
        /// <summary>
        /// Deletes a task entity.
        /// </summary>
        /// <param name="id">he ID of the task entity as given by Key.</param>
        void DeleteTask(long id)
        {
            _db.Delete(_keyFactory.CreateKey(id));
        }
        // [END delete_entity]

        // [START format_results]
        static IEnumerable<string> FormatTasks(IEnumerable<Entity> tasks)
        {
            var results = new List<string>();
            foreach(Entity task in tasks)
            {
                if ((bool)task["done"])
                {
                    results.Add($"{task.Key.Path.First().Id} : " + 
                        $"{(string)task["description"]} (done)");
                }
                else
                {
                    results.Add($"{task.Key.Path.First().Id} : " + 
                        $"{(string)task["description"]} " + 
                        $"(created {(DateTime)task["created"]})");
                }
            }
            return results;
        }


        // [END format_results]
        static void Main(string[] args)
        {
        }
    }
}
