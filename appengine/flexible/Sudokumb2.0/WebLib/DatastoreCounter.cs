using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    }

    public class DatastoreCounter
    {
        const string COUNT = "count";
        readonly DatastoreDb _datastore;

        readonly IOptions<DatastoreCounterOptions> _options;

        readonly KeyFactory _keyFactory;

        readonly string _shard = Guid.NewGuid().ToString();

        public DatastoreCounter(DatastoreDb datastore,
            IOptions<DatastoreCounterOptions> options)
        {
            _datastore = datastore;
            _options = options;
            var opts = options.Value;
            _keyFactory = new KeyFactory(datastore.ProjectId,
                datastore.NamespaceId, opts.Kind);
        }

        public Task SetCountAsync(string key, long value,
            CancellationToken cancellationToken)
        {
            Entity entity = new Entity()
            {
                Key = _keyFactory.CreateKey($"{key}:{_shard}"),
                [COUNT] = value
            };
            entity[COUNT].ExcludeFromIndexes = true;
            return _datastore.UpsertAsync(entity, CallSettings
                .FromCancellationToken(cancellationToken));
        }

        public async Task<long> GetCountAsync(string key,
            CancellationToken cancellationToken)
        {
            var callSettings = CallSettings.FromCancellationToken(
                cancellationToken);
            var query = new Query(_options.Value.Kind)
            {
                Filter = Filter.GreaterThan("__key__", _keyFactory.CreateKey(key)),
                Order = { { "__key__", PropertyOrder.Types.Direction.Ascending } }
            };
            long count = 0;
            var lazyResults = _datastore.RunQueryLazilyAsync(query, 
                callSettings:callSettings).GetEnumerator();
            while (await lazyResults.MoveNext())
            {
                Entity entity = lazyResults.Current;
                if (!entity.Key.Path.First().Name.StartsWith(key))
                {
                    break;
                }
                count += (long)entity[COUNT];
            }
            return count;
        }
    }
}


