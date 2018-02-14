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
            RunBenchmark(new LockingCounter());
            RunBenchmark(new InterlockedCounter());
            RunBenchmark(new ShardedCounter());
        }

        static void RunBenchmark(Counter counter)
        {
            Console.WriteLine("Running benchmark for {0}",
                counter.GetType().FullName);
            CancellationTokenSource cancel = new CancellationTokenSource();
            const int TASK_COUNT = 100;
            Task[] tasks = new Task[TASK_COUNT];
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
