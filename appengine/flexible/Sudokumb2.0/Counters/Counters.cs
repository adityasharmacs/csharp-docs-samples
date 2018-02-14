using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Counters
{
    public interface Counter
    {
        void Increase(long amount);
        long Count {get; }
    }

    public class LockingCounter : Counter
    {
        long count_ = 0;
        object thisLock = new object();

        public long Count
        {
            get
            {
                lock(thisLock)
                {
                    return count_;
                }
            }
        }

        public void Increase(long amount)
        {
            lock(thisLock)
            {
                count_ += amount;
            }
        }
    }

    public class InterlockedCounter : Counter
    {
        long count_ = 0;

        public long Count => count_;

        public void Increase(long amount)
        {
            Interlocked.Add(ref count_, amount);
        }
    }

    public class ShardedCounter : Counter
    {
        ConcurrentBag<LockingCounter> shards_ =
            new ConcurrentBag<LockingCounter>();
        LocalDataStoreSlot slot_ = Thread.AllocateDataSlot();

        public long Count
        {
            get
            {
                long sum = 0;
                foreach (LockingCounter counter in shards_)
                {
                    sum += counter.Count;
                }
                return sum;
            }
        }

        public void Increase(long amount)
        {
            LockingCounter counter = Thread.GetData(slot_) as LockingCounter;
            if (null == counter)
            {
                counter = new LockingCounter();
                Thread.SetData(slot_, counter);
                shards_.Add(counter);
            }
            counter.Increase(amount);
        }
    }
}
