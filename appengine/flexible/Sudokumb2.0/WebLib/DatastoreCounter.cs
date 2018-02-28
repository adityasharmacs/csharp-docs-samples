using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Datastore.V1;
using Microsoft.Extensions.Options;
using Sudokumb;

namespace Sudokumb
{
    public class DatastoreCounterOptions
    {
        public string Kind { get; set; } = "Counter";
        public int Shards { get; set; } = 32;
    }

    public class DatastoreCounter
    {
        const string COUNT = "count";
        readonly DatastoreDb _datastore;

        readonly IOptions<DatastoreCounterOptions> _options;

        readonly KeyFactory _keyFactory;

        [ThreadStatic] static readonly Random s_random = new Random();

        public DatastoreCounter(DatastoreDb datastore,
            IOptions<DatastoreCounterOptions> options)
        {
            _datastore = datastore;
            _options = options;
            var opts = options.Value;
            _keyFactory = new KeyFactory(datastore.ProjectId,
                datastore.NamespaceId, opts.Kind);
            System.Diagnostics.Debug.Assert(opts.Shards > 0);
        }

        public async Task IncreaseAsync(string key, long amount,
            CancellationToken cancellationToken)
        {
            int randomShard = s_random.Next(0, _options.Value.Shards);
            Key dkey = _keyFactory.CreateKey($"{key}:{randomShard}");
            var callSettings = CallSettings.FromCancellationToken(
                cancellationToken);
            using (var transaction =
                await _datastore.BeginTransactionAsync(callSettings))
            {
                Entity entity = await transaction.LookupAsync(dkey,
                    callSettings);
                if (null == entity || null == entity[COUNT])
                {
                    entity[COUNT] = amount;
                    entity.Key = dkey;
                }
                else
                {
                    entity[COUNT] = amount + (long)entity[COUNT];
                }
                entity[COUNT].ExcludeFromIndexes = true;
                transaction.Upsert(entity);
                await transaction.CommitAsync(callSettings);
            }
        }

        public async Task<long> GetCountAsync(string key,
            CancellationToken cancellationToken)
        {
            var callSettings = CallSettings.FromCancellationToken(
                cancellationToken);
            long count = 0;
            Task<Entity>[] lookups = new Task<Entity>[_options.Value.Shards];
            for (int i = 0; i < lookups.Length; ++i)
            {
                lookups[i] = _datastore.LookupAsync(
                    _keyFactory.CreateKey($"{key}:{i}"),
                    callSettings: callSettings);
            }
            for (int i = 0; i < lookups.Length; ++i)
            {
                Entity entity = await lookups[i];
                if (null != entity && null != entity[COUNT])
                {
                    count += (long) entity[COUNT];
                }
            }
            return count;
        }
    }
}


