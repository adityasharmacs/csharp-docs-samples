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
            var incompleteKey = _db.CreateKeyFactory("Task").CreateIncompleteKey();
            var key = _db.AllocateId(incompleteKey);
            // [END incomplete_key]
            Assert.IsTrue(IsValidKey(key));
        }

        [TestMethod]
        public void TestNamedKey()
        {
            // [START incomplete_key]
            var key = _db.CreateKeyFactory("Task").CreateKey("sampleTask");
            // [END incomplete_key]
            Assert.IsTrue(IsValidKey(key));
        }

    }
}
