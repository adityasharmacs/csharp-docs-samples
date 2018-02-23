using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sudokumb
{
    class Program
    {
        static void Main(string[] args)
        {
            MemoryStream csvStream = new MemoryStream();
            TextWriter csv = new StreamWriter(csvStream);
            csv.WriteLine("Tasks\tCounter\tCount");

            foreach (var type in new []
            {
                typeof(UnsynchronizedCounter),
                typeof(LockingCounter),
                typeof(InterlockedCounter),
                typeof(ShardedCounter)
            })
            {
                RunBenchmark(1, type, csv);
            }
            foreach (int taskCount in new int [] {2, 4, 8, 16})
            {
                foreach (var type in new []
                {
                    typeof(LockingCounter),
                    typeof(InterlockedCounter),
                    typeof(ShardedCounter)
                })
                {
                    RunBenchmark(taskCount, type, csv);
                }
            }
            csv.Flush();
            csvStream.Seek(0, SeekOrigin.Begin);
            Console.WriteLine();
            csvStream.CopyTo(Console.OpenStandardOutput());
        }

        static void RunBenchmark(int taskCount, Type counterType, TextWriter csv)
        {
            long count = RunBenchmark(taskCount,
                (ICounter) Activator.CreateInstance(counterType));
            csv.WriteLine("{0}\t{1}\t{2}", taskCount, counterType.FullName, count);
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
