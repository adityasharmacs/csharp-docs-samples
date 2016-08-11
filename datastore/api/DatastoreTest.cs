﻿// Copyright 2016 Google Inc.
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
using System.Linq;
using Google.Protobuf;
using System.Collections.Generic;

namespace GoogleCloudSamples
{
    [TestClass]
    public class DatastoreTest
    {
        private readonly string _projectId;
        private readonly DatastoreDb _db;
        private readonly Entity _sampleTask;
        private readonly KeyFactory _keyFactory;
        private readonly DateTime _includedDate = 
            new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        private readonly DateTime _startDate =
            new DateTime(1998, 4, 18, 0, 0, 0, DateTimeKind.Utc);
        private readonly DateTime _endDate =
            new DateTime(2013, 4, 18, 0, 0, 0, DateTimeKind.Utc);


        public DatastoreTest()
        {
            _projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
            _db = DatastoreDb.Create(_projectId);
            _keyFactory = _db.CreateKeyFactory("Task");
            _sampleTask = new Entity()
            {
                Key = _keyFactory.CreateKey("sampleTask"),
            };
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
            Entity task = new Entity()
            {
                Key = _db.CreateKeyFactory("Task").CreateKey("sampleTask"),
                ["type"] = "Personal",
                ["created"] = new DateTime(1999, 01, 01, 0, 0, 0, DateTimeKind.Utc),
                ["done"] = false,
                ["priority"] = 4,
                ["percent_complete"] = 10.0,
                ["description"] = new Value()
                {
                    StringValue = "Learn Cloud Datastore",
                    ExcludeFromIndexes = true
                },
            };
            // [END properties]
            AssertValidEntity(task);
        }

        [TestMethod]
        public void TestArrayValue()
        {
            // [START array_value]
            Entity task = new Entity()
            {
                Key = _db.CreateKeyFactory("Task").CreateKey("sampleTask"),
                ["collaborators"] = new ArrayValue() { Values = { "alice", "bob" } },
                ["tags"] = new ArrayValue() { Values = { "fun", "programming" } }
            };
            // [END array_value]
            AssertValidEntity(task);
        }

        [TestMethod]
        public void TestBasicEntity()
        {
            // [START basic_entity]
            Entity task = new Entity()
            {
                Key = _db.CreateKeyFactory("Task").CreateKey("sampleTask"),
                ["type"] = "Personal",
                ["done"] = false,
                ["priority"] = 4,
                ["description"] = "Learn Cloud Datastore"
            };
            // [END basic_entity]
            AssertValidEntity(task);
        }

        [TestMethod]
        public void TestUpsert()
        {
            // [START upsert]
            _db.Upsert(_sampleTask);
            // [END upsert]
            Assert.AreEqual(_sampleTask, _db.Lookup(_sampleTask.Key));
            // Make sure a second upsert doesn't throw an exception.
            _db.Upsert(_sampleTask);
        }

        [TestMethod]
        public void TestInsert()
        {
            // [START insert]
            Entity task = new Entity()
            {
                Key = _keyFactory.CreateIncompleteKey()
            };
            task.Key = _db.Insert(task);
            // [END insert]
            Assert.AreEqual(task, _db.Lookup(task.Key));
            // Make sure a second insert throws an exception.
            try
            {
                _db.Insert(task);
                Assert.Fail("_db.Insert should throw an exception because an " +
                    $"entity with the key {task.Key} already exits.");
            }
            catch (Grpc.Core.RpcException e)
            {
                Assert.AreEqual(Grpc.Core.StatusCode.InvalidArgument, e.Status.StatusCode);
            }
        }

        [TestMethod]
        public void TestLookup()
        {
            _db.Upsert(_sampleTask);
            // [START lookup]
            Entity task = _db.Lookup(_sampleTask.Key);
            // [END lookup]
            Assert.AreEqual(_sampleTask, task);
        }


        [TestMethod]
        public void TestUpdate()
        {
            _db.Upsert(_sampleTask);
            // [START update]
            _sampleTask["priority"] = 5;
            _db.Update(_sampleTask);
            // [END update]
            Assert.AreEqual(_sampleTask, _db.Lookup(_sampleTask.Key));
        }

        [TestMethod]
        public void TestDelete()
        {
            _db.Upsert(_sampleTask);
            // [START delete]
            _db.Delete(_sampleTask.Key);
            // [END delete]
            Assert.IsNull(_db.Lookup(_sampleTask.Key));
        }

        private Entity[] UpsertBatch(Key taskKey1, Key taskKey2)
        {
            var taskList = new[]
            {
                new Entity()
                {
                    Key = taskKey1,
                    ["type"] = "Personal",
                    ["done"] = false,
                    ["priority"] = 4,
                    ["description"] = "Learn Cloud Datastore"
                },
                new Entity()
                {
                    Key = taskKey2,
                    ["type"] = "Personal",
                    ["done"] = "false",
                    ["priority"] = 5,
                    ["description"] = "Integrate Cloud Datastore"
                }
            };
            _db.Upsert(taskList);
            return taskList;
        }

        [TestMethod]
        public void TestBatchUpsert()
        {
            // [START batch_upsert]
            var taskList = new[]
            {
                new Entity()
                {
                    Key = _keyFactory.CreateIncompleteKey(),
                    ["type"] = "Personal",
                    ["done"] = false,
                    ["priority"] = 4,
                    ["description"] = "Learn Cloud Datastore"
                },
                new Entity()
                {
                    Key = _keyFactory.CreateIncompleteKey(),
                    ["type"] = "Personal",
                    ["done"] = "false",
                    ["priority"] = 5,
                    ["description"] = "Integrate Cloud Datastore"
                }
            };
            var keyList = _db.Upsert(taskList[0], taskList[1]);
            // [END batch_upsert]
            taskList[0].Key = keyList[0];
            taskList[1].Key = keyList[1];
            Assert.AreEqual(taskList[0], _db.Lookup(keyList[0]));
            Assert.AreEqual(taskList[1], _db.Lookup(keyList[1]));
        }

        [TestMethod]
        public void TestBatchLookup()
        {
            // [START batch_lookup]
            var keys = new Key[] { _keyFactory.CreateKey(1), _keyFactory.CreateKey(2) };
            // [END batch_lookup]
            var expectedTasks = UpsertBatch(keys[0], keys[1]);
            // [START batch_lookup]
            var tasks = _db.Lookup(keys[0], keys[1]);
            // [END batch_lookup]
            Assert.AreEqual(expectedTasks[0], tasks[0]);
            Assert.AreEqual(expectedTasks[1], tasks[1]);
        }

        [TestMethod]
        public void TestBatchDelete()
        {
            // [START batch_delete]
            var keys = new Key[] { _keyFactory.CreateKey(1), _keyFactory.CreateKey(2) };
            // [END batch_delete]
            UpsertBatch(keys[0], keys[1]);
            var lookups = _db.Lookup(keys);
            Assert.IsNotNull(lookups[0]);
            Assert.IsNotNull(lookups[1]);
            // [START batch_delete]
            _db.Delete(keys);
            // [END batch_delete]
            lookups = _db.Lookup(keys[0], keys[1]);
            Assert.IsNull(lookups[0]);
            Assert.IsNull(lookups[1]);
        }

        private void UpsertTaskList()
        {
            Key taskListKey = _db.CreateKeyFactory("TaskList").CreateKey("default");
            Key taskKey = new KeyFactory(taskListKey, "Task").CreateKey("someTask");
            Entity task = new Entity()
            {
                Key = taskKey,
                ["type"] = "Personal",
                ["done"] = false,
                ["completed"] = false,
                ["priority"] = 4,
                ["created"] = _includedDate,
                ["percent_complete"] = 10.0,
                ["description"] = new Value()
                {
                    StringValue = "Learn Cloud Datastore",
                    ExcludeFromIndexes = true
                },
                ["tag"] = new ArrayValue() { Values = { "fun", "l", "programming" } }
            };
            _db.Upsert(task);
        }

        private static bool IsEmpty(DatastoreQueryResults results)
        {
            foreach (var result in results)
                return false;
            return true;
        }

        [TestMethod]
        public void TestBasicQuery()
        {
            UpsertTaskList();
            // [START basic_query]
            Query query = new Query("Task")
            {
                Filter = Filter.And(Filter.Equal("done", false),
                    Filter.GreaterThanOrEqual("priority", 4)),
                Order = { { "priority", PropertyOrder.Types.Direction.Descending } }
            };
            // [END basic_query]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestRunQuery()
        {
            UpsertTaskList();
            // [START run_query]
            Query query = new Query("Task");
            DatastoreQueryResults tasks = _db.RunQuery(query);
            // [END run_query]
            Assert.IsFalse(IsEmpty(tasks));
        }

        [TestMethod]
        public void TestPropertyFilter()
        {
            UpsertTaskList();
            // [START property_filter]
            Query query = new Query("Task")
            {
                Filter = Filter.Equal("done", false)
            };
            // [END property_filter]
            var tasks = _db.RunQuery(query);
            Assert.IsFalse(IsEmpty(tasks));
        }

        [TestMethod]
        public void TestCompositeFilter()
        {
            UpsertTaskList();
            // [START composite_filter]
            Query query = new Query("Task")
            {
                Filter = Filter.And(Filter.Equal("done", false),
                    Filter.Equal("priority", 4)),
            };
            // [END composite_filter]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestKeyFilter()
        {
            UpsertTaskList();
            // [START key_filter]
            Query query = new Query("Task")
            {
                Filter = Filter.GreaterThan("__key__", _keyFactory.CreateKey("aTask"))
            };
            // [END key_filter]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestAscendingSort()
        {
            UpsertTaskList();
            // [START ascending_sort]
            Query query = new Query("Task")
            {
                Order = { { "created", PropertyOrder.Types.Direction.Ascending } }
            };
            // [END ascending_sort]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestDescendingSort()
        {
            UpsertTaskList();
            // [START descending_sort]
            Query query = new Query("Task")
            {
                Order = { { "created", PropertyOrder.Types.Direction.Descending } }
            };
            // [END descending_sort]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestMultiSort()
        {
            UpsertTaskList();
            // [START multi_sort]
            Query query = new Query("Task")
            {
                Order = { { "priority", PropertyOrder.Types.Direction.Descending },
                    { "created", PropertyOrder.Types.Direction.Ascending } }
            };
            // [END multi_sort]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestKindlessQuery()
        {
            UpsertTaskList();
            // [START kindless_query]
            Query query = new Query()
            {
                Filter = Filter.GreaterThan("__key__",
                    _keyFactory.CreateKey("aTask"))
            };
            // [END kindless_query]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestAncestorQuery()
        {
            UpsertTaskList();
            // [START ancestor_query]
            Query query = new Query("Task")
            {
                Filter = Filter.HasAncestor(_db.CreateKeyFactory("TaskList")
                    .CreateKey("default"))
            };
            // [END ancestor_query]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestProjectionQuery()
        {
            UpsertTaskList();
            // [START projection_query]
            Query query = new Query("Task")
            {
                Projection = { "priority", "percent_complete" }
            };
            // [END projection_query]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestKeysOnlyQuery()
        {
            UpsertTaskList();
            // [START keys_only_query]
            Query query = new Query("Task")
            {
                Projection = { "__key__" }
            };
            // [END keys_only_query]
            foreach (Entity task in _db.RunQuery(query))
            {
                Assert.AreNotEqual(0, task.Key.Path[0].Id);
                Assert.AreEqual(0, task.Properties.Count);
                break;
            };
        }

        [TestMethod]
        public void TestDistinctQuery()
        {
            UpsertTaskList();
            // [START distinct_query]
            Query query = new Query("Task")
            {
                DistinctOn = { "priority" }
            };
            // [END distinct_query]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestArrayValueInequalityRange()
        {
            UpsertTaskList();
            // [START array_value_inequality_range]
            Query query = new Query("Task")
            {
                Filter = Filter.And(Filter.GreaterThan("tag", "learn"),
                    Filter.LessThan("tag", "math"))
            };
            // [END array_value_inequality_range]
            Assert.IsTrue(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestArrayValueEquality()
        {
            UpsertTaskList();
            // [START array_value_equality]
            Query query = new Query("Task")
            {
                Filter = Filter.And(Filter.Equal("tag", "fun"),
                    Filter.Equal("tag", "programming"))
            };
            // [END array_value_equality]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestInequalityRange()
        {
            UpsertTaskList();
            // [START inequality_range]
            Query query = new Query("Task")
            {
                Filter = Filter.And(Filter.GreaterThan("created", _startDate),
                    Filter.LessThan("created", _endDate))
            };
            // [END inequality_range]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        [ExpectedException(typeof(Grpc.Core.RpcException))]
        public void TestInequalityInvalid()
        {
            UpsertTaskList();
            // [START inequality_invalid]
            Query query = new Query("Task")
            {
                Filter = Filter.And(Filter.GreaterThan("created", _startDate),
                    Filter.GreaterThan("priority", 3))
            };
            // [END inequality_invalid]
            IsEmpty(_db.RunQuery(query));
        }

        [TestMethod]
        public void TestEqualAndInequalityRange()
        {
            UpsertTaskList();
            // [START equal_and_inequality_range]
            Query query = new Query("Task")
            {
                Filter = Filter.And(Filter.Equal("priority", 4),
                    Filter.GreaterThan("created", _startDate),
                    Filter.LessThan("created", _endDate))
            };
            // [END equal_and_inequality_range]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        public void TestInequalitySort()
        {
            UpsertTaskList();
            // [START inequality_sort]
            Query query = new Query("Task")
            {
                Filter = Filter.GreaterThan("priority", 3),
                Order = { { "priority", PropertyOrder.Types.Direction.Ascending},
                    {"created", PropertyOrder.Types.Direction.Ascending } }
            };
            // [END inequality_sort]
            Assert.IsFalse(IsEmpty(_db.RunQuery(query)));
        }

        [TestMethod]
        [ExpectedException(typeof(Grpc.Core.RpcException))]
        public void TestInequalitySortInvalidNotSame()
        {
            UpsertTaskList();
            // [START inequality_sort_invalid_not_same]
            Query query = new Query("Task")
            {
                Filter = Filter.GreaterThan("priority", 3),
                Order = { {"created", PropertyOrder.Types.Direction.Ascending } }
            };
            // [END inequality_sort_invalid_not_same]
            IsEmpty(_db.RunQuery(query));
        }

        [TestMethod]
        [ExpectedException(typeof(Grpc.Core.RpcException))]
        public void TestInequalitySortInvalidNotFirst()
        {
            UpsertTaskList();
            // [START inequality_sort_invalid_not_first]
            Query query = new Query("Task")
            {
                Filter = Filter.GreaterThan("priority", 3),
                Order = { {"created", PropertyOrder.Types.Direction.Ascending },
                    { "priority", PropertyOrder.Types.Direction.Ascending} }
            };
            // [END inequality_sort_invalid_not_first]
            IsEmpty(_db.RunQuery(query));
        }

        [TestMethod]
        public void TestLimit()
        {
            UpsertTaskList();
            // [START limit]
            Query query = new Query("Task")
            {
                Limit = 1,
            };
            // [END limit]
            Assert.AreEqual(1, _db.RunQuery(query).Count());
        }

        [TestMethod]
        public void TestCursorPaging()
        {
            UpsertTaskList();
            _db.Upsert(_sampleTask);
            var pageOneCursor = CursorPaging(1, null);
            Assert.IsNotNull(pageOneCursor);
            var pageTwoCursor = CursorPaging(1, pageOneCursor);
            Assert.IsNotNull(pageTwoCursor);
            Assert.AreNotEqual(pageOneCursor, pageTwoCursor);
        }

        private string CursorPaging(int pageSize, string pageCursor)
        {
            // [START cursor_paging]
            Query query = new Query("Task")
            {
                Limit = pageSize,
            };
            if (!string.IsNullOrEmpty(pageCursor))
                query.StartCursor = ByteString.FromBase64(pageCursor);

            ByteString finalCursor = null;
            foreach (EntityResult result in _db.RunQuery(query)
                .AsEntityResults())
            {
                var task = result.Entity;
                // Do something with the task.
                finalCursor = result.Cursor;
            }
            return finalCursor?.ToBase64();
            // [END cursor_paging]
        }

        private IReadOnlyList<Key> UpsertBalances()
        {
            KeyFactory keyFactory = _db.CreateKeyFactory("People");
            Entity from = new Entity()
            {
                Key = keyFactory.CreateKey("from"),
                ["balance"] = 100
            };
            Entity to = new Entity()
            {
                Key = keyFactory.CreateKey("to"),
                ["balance"] = 0
            };
            var keys = _db.Upsert(from, to);
            // TODO: return keys; when following bug is fixed:
            // https://github.com/GoogleCloudPlatform/google-cloud-dotnet/issues/308
            return new[] { from.Key, to.Key };
        }

        // [START transactional_update]
        private void TransferFunds(Key fromKey, Key toKey, long amount)
        {
            using (var transaction = _db.BeginTransaction()) 
            {
                var entities = _db.Lookup(fromKey, toKey);
                entities[0]["balance"].IntegerValue -= amount;
                entities[1]["balance"].IntegerValue += amount;
                transaction.Update(entities);
                transaction.Commit();
            }
        }
        // [END transactional_update]

        [TestMethod]
        public void TestTransactionalUpdate()
        {
            var keys = UpsertBalances();
            TransferFunds(keys[0], keys[1], 10);
            var entities = _db.Lookup(keys);
            Assert.AreEqual(90, entities[0]["balance"]);
            Assert.AreEqual(10, entities[1]["balance"]);
        }
    }

}