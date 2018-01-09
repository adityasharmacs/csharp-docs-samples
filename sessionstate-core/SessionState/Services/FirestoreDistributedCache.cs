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
using Google.Cloud.Firestore.V1Beta1;
using System;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Google.Api.Gax.Grpc;
using Google.Api.Gax;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using static Google.Cloud.Datastore.V1.ReadOptions.Types;

namespace SessionState
{
    class FirestoreDistributedCacheOptions
    {
        /// <summary>
        /// Your Google project id.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Optional.  The Firestore name to store the sessions in.
        /// </summary>
        public string DatabaseId { get; set; }
    }

    class FirestoreTransactionScope : IDisposable 
    {
        FirestoreDistributedCache _cache;
        public Google.Protobuf.ByteString Transaction {get; private set; }
        bool _committed = false;
        public List<Write> Writes { get; private set; } = new List<Write>();
        
        private FirestoreTransactionScope (FirestoreDistributedCache cache,
            BeginTransactionResponse response)
        {
            _cache = cache;
            Transaction = response.Transaction;
        }

        public static FirestoreTransactionScope Begin(
            FirestoreDistributedCache cache)
        {
            var response = cache._firestore.BeginTransaction(cache.DatabaseName);
            return new FirestoreTransactionScope(cache, response);                
        }
        
        public static async Task<FirestoreTransactionScope> BeginAsync(
            FirestoreDistributedCache cache)
        {
            var response = await cache._firestore.BeginTransactionAsync(
                cache.DatabaseName);
            return new FirestoreTransactionScope(cache, response);                
        }

        public void Commit() 
        {
            if (!_committed)
            {
                _committed = true;
                _cache._firestore.Commit(_cache.DatabaseName, _writes);
            }
        }

        public async Task CommitAsync() 
        {
            if (!_committed)
            {
                _committed = true;
                await _cache._firestore.CommitAsync(_cache.DatabaseName, _writes);
            }
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (_committed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                _cache._firestore.Rollback(_cache.DatabaseName, Transaction);
                _committed = true;
            }
        }

        ~FirestoreTransactionScope() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }

    class FirestoreDistributedCache : IDistributedCache
    {
        /// <summary>
        /// My connection to Google Cloud Firestore.
        /// </summary>
        internal FirestoreClient _firestore;
        internal ILogger _logger;

        internal string _projectId;

        private string _databaseId;

        public string DatabaseName 
        { 
            get => $"projects/{_projectId}/databases/{_databaseId}"; 
            private set {} 
        }

        /// <summary>
        /// Only run one sweep task per process.
        /// </summary>
        private static Task s_sweepTask;
        private static readonly Object s_sweepTaskLock = new object();
        /// <summary>
        /// Retry Datastore operations when they fail.
        /// </summary>
        private readonly CallSettings _callSettings =
            CallSettings.FromCallTiming(CallTiming.FromRetry(new RetrySettings(
                new BackoffSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), 2),
                new BackoffSettings(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20), 1),
                Expiration.FromTimeout(TimeSpan.FromSeconds(30)))));

        /// <summary>
        /// Property names and kind names for the datastore entities.
        /// </summary>
        private const string
            // The Session entity just stores the bytes in the entity.
            SESSION_KIND = "Session",
            BYTES = "bytes",
            // The SessionExpires entity stores expiration information.
            SESSION_EXPIRES_KIND = "SessionExpires",
            EXPIRATION = "expires",
            SLIDING_EXPIRATION = "sliding";

        public FirestoreDistributedCache(IOptions<FirestoreDistributedCacheOptions> options,
            ILogger<FirestoreDistributedCache> logger)
        {
            _logger = logger;
            var opts = options.Value;
            _firestore = FirestoreClient.Create();
            lock (s_sweepTaskLock)
            {
                if (s_sweepTask == null)
                {
                    s_sweepTask = Task.Run(() => SweepTaskMain());
                }
            }
        }

        string ToDocName(string key) =>
            $"projects/{_projectId}/databases/{_databaseId}/documents/{key}";

        public byte[] Get(string key) 
        {
            _logger.LogDebug("Get({0})", key);
            var doc = _firestore.GetDocument(new GetDocumentRequest() {
                Name = ToDocName(key)
            });            
            return BytesFromDoc(doc);
        }

        public async Task<byte[]> GetAsync(string key, 
            CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug("GetAsync({0})", key);
            var doc = await _firestore.GetDocumentAsync(new GetDocumentRequest() {
                Name = ToDocName(key)
            });            
            return BytesFromDoc(doc);
        }

        public void Refresh(string key)
        {
            _logger.LogDebug("Refresh({0})", key);
            using (var transaction = FirestoreTransactionScope.Begin(this))
            {
                var doc = _firestore.GetDocument(new GetDocumentRequest() {
                    Name = ToDocName(key),
                    Transaction = transaction.Transaction
                });
                if (UpdateExpiration(doc))
                {
                    transaction.Writes.Add(new Write() {
                        Update = doc,
                        UpdateMask = new DocumentMask() {
                            FieldPaths = {EXPIRATION}
                        }
                    });
                    transaction.Commit();
                }
            }
        }

        public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug("Refresh({0})", key);
            using (var transaction = await FirestoreTransactionScope.BeginAsync(this))
            {
                var doc = await _firestore.GetDocumentAsync(new GetDocumentRequest() {
                    Name = ToDocName(key),
                    Transaction = transaction.Transaction
                });
                if (UpdateExpiration(doc))
                {
                    transaction.Writes.Add(new Write() {
                        Update = doc,
                        UpdateMask = new DocumentMask() {
                            FieldPaths = {EXPIRATION}
                        }
                    });
                    await transaction.CommitAsync();
                }
            }
        }

        public void Remove(string key) 
        {
            _logger.LogDebug("Remove({0})", key);
            _datastore.Delete(ToEntityKeys(key), _callSettings);
        }

        public Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug("RemoveAsync({0})", key);
            return _datastore.DeleteAsync(ToEntityKeys(key), 
                _callSettings.WithCancellationToken(token));
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _logger.LogDebug("Set({0})", key);
            _datastore.Upsert(NewEntities(key, value, options), _callSettings);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default(CancellationToken))        
        {
            _logger.LogDebug("SetAsync({0})", key);
            return _datastore.UpsertAsync(NewEntities(key, value, options),
                _callSettings.WithCancellationToken(token));
        }

        bool HasExpired(Document doc) 
        {
            if (doc == null)
            {
                return true;
            }
            var expiration = doc.Fields[EXPIRATION]?.TimestampValue?.ToDateTime();
            return expiration.HasValue ? DateTime.UtcNow > expiration.Value : false;
        }

        /// Returns the bytes (cache payload) stored in the entity.
        byte[] BytesFromDoc(Document doc) {            
            if (HasExpired(doc)) {
                return null;
            }
            return doc.Fields[BYTES]?.BytesValue?.ToByteArray() ?? null;
        }

        Entity[] NewEntities(string key, byte[] value, DistributedCacheEntryOptions options) 
        {
            Entity session = new Entity()
            {
                Key = _sessionKeyFactory.CreateKey(key),
                [BYTES] = value
            };
            session[BYTES].ExcludeFromIndexes = true;
            Entity sessionExpires = new Entity() 
            {
                Key = _sessionExpiresKeyFactory.CreateKey(key)
            };
            if (options.AbsoluteExpiration.HasValue)
            {
                sessionExpires[EXPIRATION] = options.AbsoluteExpiration.Value;
            }
            else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                sessionExpires[EXPIRATION] = DateTime.UtcNow.Add(
                    options.AbsoluteExpirationRelativeToNow.Value 
                );
            }
            else if (options.SlidingExpiration.HasValue)
            {
                sessionExpires[SLIDING_EXPIRATION] = options.SlidingExpiration.Value.TotalSeconds;
                sessionExpires[SLIDING_EXPIRATION].ExcludeFromIndexes = true;
                sessionExpires[EXPIRATION] = DateTime.UtcNow.Add(
                    options.SlidingExpiration.Value
                );
            }
            else
            {
                throw new ArgumentException("Required expiration option was not set.", "options");
            }
            return new [] {session, sessionExpires};
        }

        bool UpdateExpiration(Entity sessionExpires)
        {
            if (sessionExpires == null)
            {
                return false;            
            }
            var slidingExpiration = sessionExpires[SLIDING_EXPIRATION]?.DoubleValue;
            if (slidingExpiration.HasValue) 
            {
                sessionExpires[EXPIRATION] = DateTime.UtcNow.AddSeconds(slidingExpiration.Value);
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
                            _logger.LogError(1, e, "Error reading sweep begin date.");
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
                    var grace_period = TimeSpan.FromMinutes(20);
                    var query = new Query(SESSION_EXPIRES_KIND)
                    {
                        Filter = Filter.LessThan(EXPIRATION, now - grace_period),
                        Projection = { "__key__" }
                    };
                    foreach (Entity expiredSession in _datastore.RunQueryLazily(query))
                    {
                        try
                        {
                            using (var transaction = _datastore.BeginTransaction(_callSettings))
                            {
                                var sessionExpires = transaction.Lookup(expiredSession.Key, _callSettings);
                                if (sessionExpires != null && sessionExpires[EXPIRATION] != null
                                    || sessionExpires[EXPIRATION].TimestampValue.ToDateTime()> now) 
                                {
                                    continue;
                                }
                                var keys = ToEntityKeys(expiredSession.Key.Path.First().Name);
                                transaction.Delete(keys);
                                transaction.Commit(_callSettings);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(2, e, "Failed to delete session.");
                        }
                    }
                    _logger.LogInformation("Done sweep.");
                }
                catch (Exception e)
                {
                    _logger.LogError(3, e, "Failed to query expired sessions.");
                }
            }
        }        
    }
}