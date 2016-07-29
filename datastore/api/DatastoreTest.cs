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
using System;
using Google.Datastore.V1Beta3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GoogleCloudSamples
{
    [TestClass]
    public class DatastoreTest
    {
        private readonly string _projectId;
        private readonly DatastoreDb _db;

        public DatastoreTest()
        {
            _projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
            _db = DatastoreDb.Create(_projectId);
        }

        private bool IsValidKey(Key key)
        {
            foreach (var element in key.Path)
            {
                if (element.Id == 0 && string.IsNullOrEmpty(element.Name))
                    return false;
                if (string.IsNullOrEmpty(element.Kind))
                    return false;
            }
            return true;
        }

        [TestMethod]
        public void TestIncompleteKey()
        {
            // [START incomplete_key]
            Key incompleteKey = _db.CreateKeyFactory("Task").CreateIncompleteKey();
            Key key = _db.AllocateId(incompleteKey);
            // [END incomplete_key]
            Assert.IsTrue(IsValidKey(key));
        }

        [TestMethod]
        public void TestNamedKey()
        {
            // [START named_key]
            Key key = _db.CreateKeyFactory("Task").CreateKey("sampleTask");
            // [END named_key]
            Assert.IsTrue(IsValidKey(key));
        }

        [TestMethod]
        public void TestKeyWithParent()
        {
            // [START key_with_parent]
            Key rootKey = _db.CreateKeyFactory("TaskList").CreateKey("default");
            Key key = new KeyFactory(rootKey, "Task").CreateKey("sampleTask");
            // [END key_with_parent]
            Assert.IsTrue(IsValidKey(key));
        }

        [TestMethod]
        public void TestKeyWithMultilevelParent()
        {
            // [START key_with_multilevel_parent]
            Key rootKey = _db.CreateKeyFactory("User").CreateKey("Alice");
            Key taskListKey = new KeyFactory(rootKey, "TaskList").CreateKey("default");
            Key key = new KeyFactory(taskListKey, "Task").CreateKey("sampleTask");
            // [END key_with_multilevel_parent]
            Assert.IsTrue(IsValidKey(key));
        }

        private void AssertValidEntity(Entity original)
        {
            _db.Upsert(original);
            Assert.AreEqual(original, _db.Lookup(original.Key));
        }

        [TestMethod]
        public void TestEntityWithParent()
        {
            // [START entity_with_parent]
            Key taskListKey = _db.CreateKeyFactory("TaskList").CreateKey("default");
            Key taskKey = new KeyFactory(taskListKey, "Task").CreateKey("sampleTask");
            Entity task = new Entity()
            {
                Key = taskKey,
                ["type"] = "Personal",
                ["done"] = false,
                ["priority"] = 4,
                ["description"] = "Learn Cloud Datastore"
            };
            // [END entity_with_parent]
            AssertValidEntity(task);
        }

        [TestMethod]
        public void TestProperties()
        {
            // [START properties]
            Key taskListKey = _db.CreateKeyFactory("TaskList").CreateKey("default");
            Key taskKey = new KeyFactory(taskListKey, "Task").CreateKey("sampleTask");
            Entity task = new Entity()
            {
                Key = _db.CreateKeyFactory("Task").CreateKey("taskOne"),
                ["type"] = "Personal",
                ["created"] = new DateTime(1999, 01, 01, 0, 0, 0, DateTimeKind.Utc),
                ["done"] = false,
                ["priority"] = 4,
                ["percent_complete"] = 10.0,                
            };
            (task["description"] = "Learn Cloud Datastore").ExcludeFromIndexes = true;
            // [END properties]
            AssertValidEntity(task);
        }

    }
}
