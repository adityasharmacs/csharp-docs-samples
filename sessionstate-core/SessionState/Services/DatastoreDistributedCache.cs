// Copyright 2017 Google Inc.
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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Google.Cloud.Datastore.V1;
using System;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Google.Api.Gax.Grpc;
using Google.Api.Gax;
using System.Linq;

namespace SessionState
{
    class DatastoreDistributedCacheOptions
    {
        /// <summary>
        /// Your Google project id.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Optional.  The Datastore namespace to store the sessions in.
        /// </summary>
        public string Namespace { get; set; }
    }

    class DatastoreDistributedCache : IDistributedCache
    {
        /// <summary>
        /// My connection to Google Cloud Datastore.
        /// </summary>
        private DatastoreDb _datastore;
        private KeyFactory _sessionKeyFactory;

        private ILogger _logger;

        /// <summary>
        /// Retry Datastore operations when they fail.
        /// </summary>
        private readonly CallSettings _callSettings =
            CallSettings.FromCallTiming(CallTiming.FromRetry(new RetrySettings(
                new BackoffSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), 2),
                new BackoffSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), 2),
                Expiration.FromTimeout(TimeSpan.FromSeconds(30)))));

        /// <summary>
        /// Property names and kind names for the datastore entities.
        /// </summary>
        private const string
            EXPIRATION = "expires",
            SLIDING_EXPIRATION = "sliding",
            BYTES = "bytes",
            SESSION_KIND = "Session";            

        public DatastoreDistributedCache(IOptions<DatastoreDistributedCacheOptions> options,
            ILogger<DatastoreDistributedCache> logger)
        {
            _logger = logger;
            var opts = options.Value;
            _datastore = DatastoreDb.Create(opts.ProjectId, opts.Namespace ?? "");
            _sessionKeyFactory = _datastore.CreateKeyFactory(SESSION_KIND);
        }

        public byte[] Get(string key) 
        {
            _logger.LogDebug($"Get({key})");
            return BytesFromEntity(_datastore.Lookup(_sessionKeyFactory.CreateKey(key)));
        }

        public async Task<byte[]> GetAsync(string key, 
            CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug($"GetAsync({key})");
            var entity = await _datastore.LookupAsync(_sessionKeyFactory.CreateKey(key), 
                callSettings:Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token));
            return BytesFromEntity(entity);
        }

        public void Refresh(string key)
        {
            _logger.LogDebug($"Refresh({key})");
            using (var transaction = _datastore.BeginTransaction())
            {
                var entity = transaction.Lookup(_sessionKeyFactory.CreateKey(key));
                if (UpdateExpiration(entity, transaction))
                {
                    transaction.Commit();
                }
            }
        }

        public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug($"RefreshAsync({key})");            
            using (var transaction = await _datastore.BeginTransactionAsync(
                Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token)))
            {
                var entity = await transaction.LookupAsync(_sessionKeyFactory.CreateKey(key));
                if (UpdateExpiration(entity, transaction)) 
                {
                    await transaction.CommitAsync();
                }
            }                
        }

        public void Remove(string key) 
        {
            _logger.LogDebug($"Remove({key})");
            _datastore.Delete(_sessionKeyFactory.CreateKey(key));
        }

        public Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug($"RemoveAsync({key})");
            return _datastore.DeleteAsync(_sessionKeyFactory.CreateKey(key),
                Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token));
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _logger.LogDebug($"Set({key})");
            _datastore.Upsert(NewEntity(key, value, options));
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default(CancellationToken))        
        {
            _logger.LogDebug($"SetAsync({key})");
            return _datastore.UpsertAsync(NewEntity(key, value, options),
                Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token));
        }

        bool HasExpired(Entity entity) {
            var expiration = entity[EXPIRATION]?.TimestampValue?.ToDateTime();
            return expiration.HasValue ? DateTime.UtcNow > expiration.Value : false;
        }

        /// Returns the bytes (cache payload) stored in the entity.
        byte[] BytesFromEntity(Entity entity) {
            if (entity == null || HasExpired(entity))
            {
                return null;
            }
            else
            {
                return entity[BYTES]?.BlobValue?.ToByteArray() ?? null;
            }        
        }

        Entity NewEntity(string key, byte[] value, DistributedCacheEntryOptions options) 
        {
            Entity entity = new Entity()
            {
                Key = _sessionKeyFactory.CreateKey(key),
                [BYTES] = value
            };
            entity[BYTES].ExcludeFromIndexes = true;
            if (options.AbsoluteExpiration.HasValue)
            {
                entity[EXPIRATION] = options.AbsoluteExpiration.Value;
            }
            else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                entity[EXPIRATION] = DateTime.UtcNow.Add(
                    options.AbsoluteExpirationRelativeToNow.Value 
                );
            }
            else if (options.SlidingExpiration.HasValue)
            {
                entity[SLIDING_EXPIRATION] = options.SlidingExpiration.Value.TotalSeconds;
                entity[SLIDING_EXPIRATION].ExcludeFromIndexes = true;
                entity[EXPIRATION] = DateTime.UtcNow.Add(
                    options.SlidingExpiration.Value
                );
            }
            else
            {
                throw new ArgumentException("Required expiration option was not set.", "options");
            }
            return entity;
        }

        bool UpdateExpiration(Entity entity, DatastoreTransaction transaction)
        {
            if (entity == null || HasExpired(entity))
            {
                return false;            
            }
            var slidingExpiration = entity[SLIDING_EXPIRATION]?.DoubleValue;
            if (slidingExpiration.HasValue) 
            {
                entity[EXPIRATION] = DateTime.UtcNow.AddSeconds(slidingExpiration.Value);
                transaction.Update(entity);
                return true;
            }
            return false;        
        }
 
         /// <summary>
        /// The main loop of a Task that periodically cleans up expired sessions.
        /// Never returns.
        /// </summary>
        private async Task SweepTaskMain()
        {
            var random = System.Security.Cryptography.RandomNumberGenerator.Create();
            var randomByte = new byte[1];
            while (true)
            {
                random.GetBytes(randomByte);
                // Not a perfect distrubution, but fine for our limited purposes.
                int randomMinute = randomByte[0] % 60;
                _logger.LogDebug("Delaying {0} minutes before checking sweep lock.", randomMinute);
                await Task.Delay(TimeSpan.FromMinutes(randomMinute));

                // Use a lock to make sure no clients are sweeping at the same time, or
                // sweeping more often than once per hour.
                try
                {
                    using (var transaction = await _datastore.BeginTransactionAsync(_callSettings))
                    {
                        const string SWEEP_BEGIN_DATE = "beginDate",
                            SWEEPER = "sweeper",
                            SWEEP_LOCK_KIND = "SweepLock";
                        var key = _datastore.CreateKeyFactory(SWEEP_LOCK_KIND).CreateKey(1);
                        Entity sweepLock = await transaction.LookupAsync(key, _callSettings) ??
                            new Entity() { Key = key };
                        bool sweep = true;
                        try
                        {
                            sweep = DateTime.UtcNow - ((DateTime)sweepLock[SWEEP_BEGIN_DATE]) >
                                TimeSpan.FromHours(1);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError("Error reading sweep begin date.", e);
                        }
                        if (!sweep)
                        {
                            _logger.LogDebug("Not yet time to sweep.");
                            continue;
                        }
                        sweepLock[SWEEP_BEGIN_DATE] = DateTime.UtcNow;
                        sweepLock[SWEEPER] = Environment.MachineName;
                        transaction.Upsert(sweepLock);
                        await transaction.CommitAsync(_callSettings);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Error acquiring sweep lock.", e);
                    continue;
                }
                try
                {
                    _logger.LogInformation("Beginning sweep.");
                    // Find old sessions to clean up
                    var now = DateTime.UtcNow;
                    var query = new Query(SESSION_KIND)
                    {
                        Filter = Filter.LessThan(EXPIRATION, now),
                        Projection = { "__key__" }
                    };
                    foreach (Entity entity in _datastore.RunQueryLazily(query))
                    {
                        try
                        {
                            using (var transaction = _datastore.BeginTransaction(_callSettings))
                            {
                                var sessionLock = SessionLockFromEntity(
                                    transaction.Lookup(entity.Key, _callSettings));
                                if (sessionLock == null || sessionLock.ExpirationDate > now)
                                    continue;
                                transaction.Delete(entity.Key,
                                    _sessionKeyFactory.CreateKey(entity.Key.Path.First().Name));
                                transaction.Commit(_callSettings);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError("Failed to delete session.", e);
                        }
                    }
                    _logger.LogInformation("Done sweep.");
                }
                catch (Exception e)
                {
                    _logger.LogError("Failed to query expired sessions.", e);
                }
            }
        }        
    }
}