// Copyright (c) 2018 Google LLC.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy of
// the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations under
// the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Sudokumb
{
    class Record
    {
        public int x { get; set; }
        public long y { get; set; }
        public int group { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var counterTypes = new []
            {
                typeof(UnsynchronizedCounter),
                typeof(LockingCounter),
                typeof(InterlockedCounter),
                typeof(ShardedCounter)
            };
            List<Record> records = new List<Record>();
            int groupNumber = 0;
            foreach (var type in counterTypes)
            {
                records.Add(RunBenchmark(1, type, groupNumber++));
            }
            foreach (int taskCount in new int [] {2, 4, 8, 16})
            {
                groupNumber = 1;
                foreach (var type in counterTypes.Skip(1))
                {
                    records.Add(RunBenchmark(taskCount, type, groupNumber++));
                }
            }
            Console.WriteLine(JsonConvert.SerializeObject(records));
        }

        static Record RunBenchmark(int taskCount, Type counterType,
            int groupNumber)
        {
            long count = RunBenchmark(taskCount,
                (ICounter) Activator.CreateInstance(counterType));
            return new Record()
            {
                x = taskCount,
                y = count,
                group = groupNumber
            };
        }

        static long RunBenchmark(int taskCount, ICounter counter)
        {
            Console.WriteLine("Running benchmark for {0} with {1} tasks...",
                counter.GetType().FullName, taskCount);
            CancellationTokenSource cancel = new CancellationTokenSource();
            Task[] tasks = new Task[taskCount];
            for (int i = 0; i < tasks.Length; ++i)
            {
                tasks[i] = Task.Run(() =>
                {
                    while (!cancel.Token.IsCancellationRequested)
                        counter.Increase(1);
                });
            }
            long count = 0;
            for (int i = 0; i < 10; ++ i)
            {
                Thread.Sleep(1000);
                count = counter.Count;
                Console.WriteLine(count);
            }
            cancel.Cancel();
            Task.WaitAll(tasks);
            return count;
        }
    }
}
