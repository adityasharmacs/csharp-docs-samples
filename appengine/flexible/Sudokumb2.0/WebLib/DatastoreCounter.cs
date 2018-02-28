using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Datastore.V1;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sudokumb;

namespace Sudokumb
{
    public class DatastoreCounterOptions
    {
        public string Kind { get; set; } = "Counter";
    }

    public class DatastoreCounter : IHostedService
    {
        const string COUNT = "count", TIMESTAMP = "timestamp";
        readonly DatastoreDb _datastore;
        readonly IOptions<DatastoreCounterOptions> _options;
        readonly KeyFactory _keyFactory;
        readonly string _shard = Guid.NewGuid().ToString();
        CancellationTokenSource _cancelHostedService;
        Task _hostedService;
        ILogger _logger;
        ConcurrentDictionary<string, ICounter> _localCounters
             = new ConcurrentDictionary<string, ICounter>();
        Dictionary<string, long> _localCountersSnapshot
            = new Dictionary<string, long>();

        public DatastoreCounter(DatastoreDb datastore,
            IOptions<DatastoreCounterOptions> options,
            ILogger<DatastoreCounter> logger)
        {
            _datastore = datastore;
            _options = options;
            _logger = logger;
            var opts = options.Value;
            _keyFactory = new KeyFactory(datastore.ProjectId,
                datastore.NamespaceId, opts.Kind);
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

       // /////////////////////////////////////////////////////////////////////
        // IHostedService implementation periodically saves
        // counts to datastore.
        public Task StartAsync(CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.Assert(null == _cancelHostedService);
            _cancelHostedService = new CancellationTokenSource();
            _hostedService = Task.Run(async() => await HostedServiceMainAsync(
                _cancelHostedService.Token));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancelHostedService.Cancel();
            return _hostedService;
        }

        public ICounter GetLocalCounter(string id) =>
            _localCounters.GetOrAdd(id,
                (key) => (ICounter) new InterlockedCounter());

        Task UpdateDatastoreFromLocalCountersAsync(CancellationToken
            cancellationToken)
        {
            Dictionary<string, long> snapshot = new Dictionary<string, long>();
            List<Entity> entities = new List<Entity>();
            var now = DateTime.UtcNow;
            foreach (var keyValue in _localCounters)
            {
                long count = snapshot[keyValue.Key] = keyValue.Value.Count;
                if (count != _localCountersSnapshot
                    .GetValueOrDefault(keyValue.Key))
                {
                    var entity = new Entity()
                    {
                        Key = _keyFactory.CreateKey($"{keyValue.Key}:{_shard}"),
                        [COUNT] = count,
                        [TIMESTAMP] = now
                    };
                    entities.Add(entity);
                }
            }
            _localCountersSnapshot = snapshot;
            if (entities.Count > 0)
            {
                return _datastore.UpsertAsync(entities, CallSettings
                    .FromCancellationToken(cancellationToken));
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        public async Task HostedServiceMainAsync(
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("DatastoreCounter.HostedServiceMainAsync()");
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                try
                {
                    await Task.Delay(1000, cancellationToken);
                    await UpdateDatastoreFromLocalCountersAsync(
                        cancellationToken);
                }
                catch (Exception e)
                when (!(e is OperationCanceledException))
                {
                    _logger.LogError(1, e, "Error while updating datastore.");
                }
            }
        }
    }
}


