using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Counters
{
    class Program
    {
        static void Main(string[] args)
        {
            RunBenchmark(1, new UnsynchronizedCounter());
            RunBenchmark(1, new LockingCounter());
            RunBenchmark(1, new InterlockedCounter());
            RunBenchmark(1, new ShardedCounter());
            
            RunBenchmark(32, new LockingCounter());
            RunBenchmark(32, new InterlockedCounter());
            RunBenchmark(32, new ShardedCounter());
        }

        static void RunBenchmark(int taskCount, Counter counter)
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
            for (int i = 0; i < 10; ++ i)
            {
                Thread.Sleep(1000);
                Console.WriteLine(counter.Count);
            }
            cancel.Cancel();
            Task.WaitAll(tasks);
        }
    }
}
