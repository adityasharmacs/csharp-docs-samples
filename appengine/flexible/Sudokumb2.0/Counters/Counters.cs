using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Sudokumb
{
    public interface ICounter
    {
        void Increase(long amount);
        long Count {get; }

        // Returns the current count, and resets the value to 0;.
        long Reset();
    }

    public class UnsynchronizedCounter : ICounter
    {
        long _count = 0;
        public long Count => _count;
        public void Increase(long amount) => _count += amount;
        public long Reset()
        {
            long count = _count;
            _count = 0;
            return count;
        }

    }

    public class LockingCounter : ICounter
    {
        long _count = 0;
        object _thisLock = new object();

        public long Count
        {
            get
            {
                lock(_thisLock)
                {
                    return _count;
                }
            }
        }

        public long Reset()
        {
            lock(_thisLock)
            {
                long count = _count;
                _count = 0;
                return count;
            }
        }

        public void Increase(long amount)
        {
            lock(_thisLock)
            {
                _count += amount;
            }
        }
    }

    public class InterlockedCounter : ICounter
    {
        long _count = 0;

        public long Count => Interlocked.CompareExchange(ref _count, 0, 0);

        public void Increase(long amount)
        {
            Interlocked.Add(ref _count, amount);
        }

        public long Reset()
        {
            long count;
            do
            {
                count = _count;
            }
            while (count != Interlocked.CompareExchange(ref _count, 0, count));
            return count;
        }
    }

    public class ShardedCounter : ICounter
    {
        object _thisLock = new object();
        long _partialSum = 0;
        List<Shard> _shards = new List<Shard>();
        readonly LocalDataStoreSlot _slot = Thread.AllocateDataSlot();

        public long Count => GetCount(reset: false);

        public long Reset() => GetCount(reset: true);

        long GetCount(bool reset)
        {
            // Clean out dead shards as we calculate the count.
            long liveSum = 0;
            long deadSum = 0;
            long result;
            List<Shard> livingShards = new List<Shard>();
            lock (_thisLock)
            {
                foreach (Shard shard in _shards)
                {
                    if (shard.Owner.IsAlive)
                    {
                        livingShards.Add(shard);
                        liveSum += shard.Count;
                    }
                    else
                    {
                        deadSum += shard.Count;
                    }
                }
                _shards = livingShards;
                result = liveSum + deadSum + _partialSum;
                if (reset)
                {
                    _partialSum = -liveSum;
                }
                else
                {
                    _partialSum += deadSum;
                }
            }
            return result;
        }

        public void Increase(long amount)
        {
            Shard counter = Thread.GetData(_slot) as Shard;
            if (null == counter)
            {
                counter = new Shard()
                {
                    Owner = Thread.CurrentThread
                };
                Thread.SetData(_slot, counter);
                lock (_thisLock) _shards.Add(counter);
            }
            counter.Increase(amount);
        }

        class Shard : InterlockedCounter
        {
            public Thread Owner { get; set; }
        }
    }
}
