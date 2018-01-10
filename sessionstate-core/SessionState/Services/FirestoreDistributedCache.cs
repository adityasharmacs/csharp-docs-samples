// Copyright 2018 Google Inc.
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
using Google.Cloud.Firestore;
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

        // Optional.
        public string CollectionId { get; set; }
    }

    class FirestoreDistributedCache : IDistributedCache
    {
        /// <summary>
        /// My connection to Google Cloud Firestore.
        /// </summary>

        readonly FirestoreDb _firestore;
        readonly ILogger _logger;

        readonly string _projectId;
        readonly string _sessionCollectionId;
        readonly CollectionReference _sessionCollection;

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

        const string SLIDING = "sliding",
            EXPIRES = "expires";

        class SessionDoc {
            [FirestoreProperty]
            public byte[] Bytes { get; set; }

            [FirestoreProperty(EXPIRES)]
            public DateTime? ExpirationDate { get; set; }

            [FirestoreProperty(SLIDING)]
            public TimeSpan? SlidingExpiration { get; set; }
        }

        public FirestoreDistributedCache(IOptions<FirestoreDistributedCacheOptions> options,
            ILogger<FirestoreDistributedCache> logger)
        {
            _logger = logger;
            var opts = options.Value;
            _projectId = opts.ProjectId;
            _sessionCollectionId = string.IsNullOrWhiteSpace(opts.CollectionId) ?
                "sessions" : opts.CollectionId;
            _firestore = FirestoreDb.Create(_projectId);            
            lock (s_sweepTaskLock)
            {
                if (s_sweepTask == null)
                {
                    s_sweepTask = Task.Run(() => SweepTaskMain());
                }
            }
        }
        public byte[] Get(string key) 
        {
            _logger.LogDebug("Get({0})", key);
            var snapshot = _sessionCollection.Document(key).SnapshotAsync().Result;
            return snapshot?.Deserialize<SessionDoc>()?.Bytes;
        }

        public async Task<byte[]> GetAsync(string key, 
            CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug("GetAsync({0})", key);
            var snapshot = await _sessionCollection.Document(key).SnapshotAsync();
            return snapshot?.Deserialize<SessionDoc>()?.Bytes;
        }

        /// Common implementation for Refresh and RefreshAsync().
        public Task RefreshCommonAsync(string key,
            CancellationToken token = default(CancellationToken))
        {
            var sessionRef = _sessionCollection.Document(key);
            return _firestore.RunTransactionAsync(async (transaction) => {
                var sessionSnapshot = await
                    transaction.GetDocumentSnapshotAsync(sessionRef, token);
                var sessionDoc = sessionSnapshot.Deserialize<SessionDoc>();
                var now = DateTime.UtcNow;
                if (sessionDoc.ExpirationDate >= now) {
                    sessionDoc.ExpirationDate = now + sessionDoc.SlidingExpiration;
                    Dictionary<FieldPath, object> updates = 
                        new Dictionary<FieldPath, object>() 
                    {
                        [new FieldPath(EXPIRES)] = sessionDoc.ExpirationDate
                    };
                    transaction.Update(sessionRef, updates);
                }
            }, cancellationToken: token);
        }


        public void Refresh(string key)
        {
            _logger.LogDebug("Refresh({0})", key);
            RefreshCommonAsync(key).Wait();
        }

        public Task RefreshAsync(string key,
            CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug("Refresh({0})", key);
            return RefreshCommonAsync(key, token);
        }

        // This is a very complicated way to do a write.
        // And I suspect it's very inefficient.
        async Task<WriteResponse> WriteAsync(Write write) 
        {
            var writeStream = _firestore.Write();
            try 
            {
                await writeStream.WriteAsync(new WriteRequest() {
                    Writes = { write },
                    Database = SessionDatabaseName
                });
                await writeStream.ResponseStream.MoveNext();
                return writeStream.ResponseStream.Current;
            } 
            finally
            {
                await writeStream.WriteCompleteAsync();
            }
        }

        public void Remove(string key) 
        {
            _logger.LogDebug("Remove({0})", key);
            
            WriteAsync(new Write() { Delete = key}).Wait();
        }

        public Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            _logger.LogDebug("RemoveAsync({0})", key);
            return WriteAsync(new Write() { Delete = key});
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _logger.LogDebug("Set({0})", key);
            var docMask = new DocumentMask();
            _firestore.UpdateDocument(NewDoc(key, value, options, ref docMask), 
                docMask);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default(CancellationToken))        
        {
            _logger.LogDebug("SetAsync({0})", key);
                        var docMask = new DocumentMask();
            return _firestore.UpdateDocumentAsync(
                NewDoc(key, value, options, ref docMask), 
                docMask, _callSettings.WithCancellationToken(token));
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

        Document NewDoc(string key, byte[] value, DistributedCacheEntryOptions options,
            ref DocumentMask docMask) 
        {
            var doc = new Document() { Name = ToSessionDocName(key) };
            docMask.FieldPaths.Clear();
            var bytes = new Value { 
                BytesValue = Google.Protobuf.ByteString.CopyFrom(value)};
            // How do I mark the bytes so they're not indexed?
            doc.Fields[BYTES] = bytes;
            // I want a a class that automatically adds field paths as I set
            // fields.  Maybe that's not possible due to map types.  I dunno.
            docMask.FieldPaths.Add(BYTES);
            DateTimeOffset expires;
            if (options.AbsoluteExpiration.HasValue)
            {
                expires = options.AbsoluteExpiration.Value;
            }
            else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                expires = DateTime.UtcNow.Add(
                    options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.SlidingExpiration.HasValue)
            {
                doc.Fields[SLIDING_EXPIRATION] = new Value() {
                    DoubleValue = options.SlidingExpiration.Value.TotalSeconds
                };
                docMask.FieldPaths.Add(SLIDING_EXPIRATION);
                expires = DateTime.UtcNow.Add(options.SlidingExpiration.Value);
            }
            else
            {
                throw new ArgumentException("Required expiration option was not set.", "options");
            }
            // Converting between C# Timestamp times and Proto timestamp types
            // is hideous.
            doc.Fields[EXPIRATION] = new Value() {
                TimestampValue = Google.Protobuf.WellKnownTypes.Timestamp
                    .FromDateTimeOffset(expires)
            };
            docMask.FieldPaths.Add(EXPIRATION);
            return doc;
        }

        bool UpdateExpiration(Document doc)
        {
            if (doc == null)
            {
                return false;            
            }
            var slidingExpiration = doc.Fields[SLIDING_EXPIRATION]?.DoubleValue;
            if (slidingExpiration.HasValue) 
            {
                doc.Fields[EXPIRATION] = new Value() {
                    TimestampValue = 
                        Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(
                        DateTime.UtcNow.AddSeconds(slidingExpiration.Value))
                };
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
                    using (var transaction = await FirestoreTransactionScope.BeginAsync(this))
                    {
                        const string SWEEP_BEGIN_DATE = "beginDate",
                            SWEEPER = "sweeper";
                        var sweepLock = await _firestore.GetDocumentAsync(new GetDocumentRequest() {
                            Name = SweepLockDocName,
                            Transaction = transaction.Transaction
                        });
                        bool shouldSweep = true;
                        try
                        {
                            var sweepBeginDate =
                                sweepLock.Fields[SWEEP_BEGIN_DATE]
                                .TimestampValue.ToDateTime();
                            shouldSweep = (DateTime.UtcNow - sweepBeginDate) >
                                TimeSpan.FromHours(1);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(1, e, "Error reading sweep begin date.");
                        }
                        if (!shouldSweep)
                        {
                            _logger.LogDebug("Not yet time to sweep.");
                            continue;
                        }
                        sweepLock.Fields[SWEEP_BEGIN_DATE] = new Value() {
                            TimestampValue = Google.Protobuf.WellKnownTypes
                                .Timestamp.FromDateTime(DateTime.UtcNow)
                        };                            
                        sweepLock.Fields[SWEEPER] = new Value() {
                            StringValue = Environment.MachineName
                        };
                        transaction.Writes.Add(new Write() {
                            Update = sweepLock,
                            UpdateMask = new DocumentMask() {
                                FieldPaths = {SWEEP_BEGIN_DATE, SWEEPER}
                            }
                        });
                        await transaction.CommitAsync();
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Error acquiring sweep lock.", e);
                    continue;
                }
#if false                
                try
                {
                    _logger.LogInformation("Beginning sweep.");
                    // Find old sessions to clean up
                    var now = DateTime.UtcNow;
                    var grace_period = TimeSpan.FromMinutes(20);
                    // Wow.  Running a query is a huge mess.
                    _firestore.RunQuery(new RunQueryRequest() {
                        StructuredQuery = new StructuredQuery() {
                            Where = new StructuredQuery.Types.Filter() {
                                FieldFilter = new StructuredQuery.Types.FieldFilter() {
                                    Field = new StructuredQuery.Types.FieldReference() {
                                        FieldPath = EXPIRATION
                                    },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.GreaterThan,
                                    Value = new Value() { 
                                        TimestampValue = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                                            now + grace_period)
                                    }
                                }
                            },
                            // From = // How do I specify a databaseId.?
                        }
                    });


                    });
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
#endif
            }
        }        
    }
}