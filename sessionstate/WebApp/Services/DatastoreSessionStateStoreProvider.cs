using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.Datastore.V1;
using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;
using System.Xml;

namespace WebApp.Services
{
    /// <summary>
    /// Implements a SessionStateStoreProvider by storing session information
    /// in Google Cloud Datastore.  Use when running on Google Compute Engine.
    /// </summary>
    public class DatastoreSessionStateStoreProvider : SessionStateStoreProviderBase
    {
        // Datastore limits writes to 1 per second per entity group.  
        // Therefore, implement a session that can be locked and unlocked in
        // under a second, we split the session into two independent Datastore
        // entities.
        // SessionLock gets written to lock the session.  
        // SessionItems gets written to unlock the session.
        // The session is locked when 
        //   SessionLock.LockCount > SessionItems.ReleaseCount.
        // Otherwise, the session is unlocked.

        /// <summary>
        /// An entity stored in Datastore.
        /// Gets written to lock a session.
        /// </summary>
        class SessionLock
        {
            /// <summary>
            /// The session id.
            /// </summary>
            public string Id;
            /// <summary>
            /// How many times has the session been locked?
            /// </summary>
            public int LockCount;
            /// <summary>
            /// When was the last time the session was locked?
            /// </summary>
            public DateTime DateLocked;
            /// <summary>
            /// When will the session expire?
            /// </summary>
            public DateTime ExpirationDate;
            /// <summary>
            /// After how many minutes will the session timeout?
            /// </summary>
            public int TimeOutInMinutes;
            /// <summary>
            /// The serialized items.  This property is not stored as part of 
            /// the SessionLock Datastore entity.
            /// </summary>
            public byte[] Items;
        };

        class SessionItems
        {
            /// <summary>
            /// The session id.
            /// </summary>
            public string Id;
            /// <summary>
            /// The serialized items.
            public byte[] Items;
            /// <summary>
            /// How many times has the lock been released?  
            /// If ReleaseCount == LockCount, then the session is unlocked.
            /// </summary>
            public int ReleaseCount;
        }

        /// <summary>
        /// Our connection to Google Cloud Datastore.
        /// </summary>
        DatastoreDb _datastore;
        KeyFactory _sessionKeyFactory, _lockKeyFactory;
        ILog _log;
        /// <summary>
        /// Only run one sweep task per process.
        /// </summary>
        static Task _sweepTask;
        static Object _sweepTaskLock = new object();

        /// <summary>
        /// Property names and kind names for the datastore entities. 
        /// </summary>
        const string
            EXPIRES = "expires",
            LOCK_DATE = "lockDate",
            LOCK_COUNT = "lockCount",
            RELEASE_COUNT = "releaseCount",
            TIMEOUT = "timeout",
            ITEMS = "items",
            SESSION_KIND = "Session",
            SESSION_LOCK_KIND = "SessionLock";
        
        /// <summary>
        /// Retry Datastore operations when they fail.
        /// </summary>
        readonly CallSettings _callSettings =
            CallSettings.FromCallTiming(CallTiming.FromRetry(new RetrySettings(
                new BackoffSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), 2),
                new BackoffSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), 2),
                Expiration.FromTimeout(TimeSpan.FromSeconds(30)))));

        public override void Initialize(string name, NameValueCollection config)
        {
            string projectId = config["projectId"];
            if (string.IsNullOrWhiteSpace(projectId) || projectId == "YOUR-PROJECT" + "-ID")
            {
                throw new ConfigurationErrorsException("Set the projectId in Web.config");
            }
            string applicationName = config["applicationName"] ?? "";
            log4net.Config.XmlConfigurator.Configure();
            _log = LogManager.GetLogger(this.GetType());
            _datastore = DatastoreDb.Create(projectId, applicationName);
            _sessionKeyFactory = _datastore.CreateKeyFactory(SESSION_KIND);
            _lockKeyFactory = _datastore.CreateKeyFactory(SESSION_LOCK_KIND);
            lock (_sweepTaskLock)
            {
                if (_sweepTask == null)
                {
                    _sweepTask = Task.Run(() => SweepTaskMain());
                }
            }
        }


        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(),
                SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            _log.DebugFormat("CreateUninitializedItem({0})", id);
            LogExceptions("CreateUninitializedItem()", () =>
            {
                var sessionLock = new SessionLock();
                sessionLock.Id = id;
                sessionLock.ExpirationDate = DateTime.UtcNow.AddMinutes(timeout);
                sessionLock.TimeOutInMinutes = timeout;
                sessionLock.DateLocked = DateTime.UtcNow;
                sessionLock.LockCount = 0;
                using (var transaction = _datastore.BeginTransaction(_callSettings))
                {
                    transaction.Upsert(ToEntity(sessionLock));
                    transaction.Delete(_sessionKeyFactory.CreateKey(id));
                    transaction.Commit(_callSettings);
                }
            });
        }

        public override SessionStateStoreData GetItem(HttpContext context, 
            string id, out bool locked, out TimeSpan lockAge, 
            out object lockId, out SessionStateActions actions)
        {
            _log.DebugFormat("GetItem({0})", id);
            return GetItemImpl(false, context, id, out locked, out lockAge,
                out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(
            HttpContext context, string id, out bool locked,
            out TimeSpan lockAge, out object lockId,
            out SessionStateActions actions)
        {
            _log.DebugFormat("GetItemExclusive({0})", id);
            return GetItemImpl(true, context, id, out locked, out lockAge,
                out lockId, out actions);
        }

        public SessionStateStoreData GetItemImpl(bool exclusive, 
            HttpContext context, string id, out bool locked, 
            out TimeSpan lockAge, out object lockId, 
            out SessionStateActions actions)
        {
            try { 
                using (var transaction = _datastore.BeginTransaction(_callSettings))
                {
                    // Look up both entities in datastore.
                    var entities = transaction.Lookup(new Key[]
                    {
                        _sessionKeyFactory.CreateKey(id),
                        _lockKeyFactory.CreateKey(id)
                    }, _callSettings);
                    SessionLock sessionLock = SessionLockFromEntity(entities[1]);
                    if (sessionLock == null || sessionLock.ExpirationDate < DateTime.UtcNow)
                    {
                        // No such session.
                        lockAge = TimeSpan.Zero;
                        locked = false;
                        lockId = null;
                        actions = SessionStateActions.None;
                        return null;
                    }
                    SessionItems sessionItems = SessionItemsFromEntity(id, entities[0]);
                    sessionLock.Items = sessionItems.Items;
                    locked = sessionLock.LockCount > sessionItems.ReleaseCount;
                    lockAge = locked ? DateTime.UtcNow - sessionLock.DateLocked
                        : TimeSpan.Zero;
                    lockId = sessionLock;
                    actions = SessionStateActions.None;
                    if (locked)
                        return null;
                    if (exclusive)
                    {
                        // Lock the session.
                        sessionLock.LockCount = sessionItems.ReleaseCount + 1;
                        sessionLock.DateLocked = DateTime.UtcNow;
                        sessionLock.ExpirationDate = 
                            DateTime.UtcNow.AddMinutes(sessionLock.TimeOutInMinutes);
                        transaction.Upsert(ToEntity(sessionLock));
                        transaction.Commit(_callSettings);
                        locked = true;
                    }
                    return Deserialize(context, sessionItems.Items, 
                        sessionLock.TimeOutInMinutes);
                }
            }
            catch (Exception e)
            {
                _log.Error("GetItemImpl()", e);
                locked = true;
                lockAge = TimeSpan.Zero;
                lockId = 0;
                actions = SessionStateActions.None;
                return null;
            }
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockIdObject)
        {
            _log.DebugFormat("ReleaseItemExclusive({0})", id);
            LogExceptions("ReleaseItemExclusive()", () =>
            {
                SessionLock lockId = (SessionLock)lockIdObject;
                SessionItems sessionItems = new SessionItems();
                sessionItems.Id = id;
                sessionItems.ReleaseCount = lockId.LockCount;
                sessionItems.Items = lockId.Items;
                using (var transaction = _datastore.BeginTransaction(_callSettings))
                {
                    SessionLock sessionLock = SessionLockFromEntity(
                        transaction.Lookup(_lockKeyFactory.CreateKey(id), _callSettings));
                    if (sessionLock == null || sessionLock.LockCount != lockId.LockCount)
                        return;  // Something else locked it in the meantime.
                    transaction.Upsert(ToEntity(sessionItems));
                    transaction.Commit(_callSettings);
                }
            });
        }


        public override void RemoveItem(HttpContext context, string id, object lockIdObject,
            SessionStateStoreData item)
        {
            _log.DebugFormat("RemoveItem({0})", id);
            LogExceptions("RemoveItem()", () =>
            {
                SessionLock lockId = (SessionLock)lockIdObject;
                using (var transaction = _datastore.BeginTransaction(_callSettings))
                {
                    SessionLock sessionLock = SessionLockFromEntity(
                        transaction.Lookup(_lockKeyFactory.CreateKey(id), _callSettings));
                    if (sessionLock == null || sessionLock.LockCount != lockId.LockCount)
                        return;  // Something else locked it in the meantime.
                    transaction.Delete(_sessionKeyFactory.CreateKey(id),
                            _lockKeyFactory.CreateKey(id));
                    transaction.Commit(_callSettings);
                }
            });
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            _log.DebugFormat("ResetItemTimeout({0})", id);
            // Ignore it.  To minimize writes to datastore, we reset the timeout
            // when we lock the session.
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockIdObject, bool newItem)
        {
            _log.DebugFormat("SetAndReleaseItemExclusive({0})", id);
            LogExceptions("SetAndReleaseItemExclusive()", () =>
            {
#if DEBUG
                // To test the exception handling code.
                if (null != item.Items["throwthrowthrow"])
                    throw new Exception("throwthrowthrow");
#endif
                SessionLock lockId = (SessionLock)lockIdObject;
                SessionItems sessionItems = new SessionItems();
                sessionItems.Id = id;
                if (newItem)
                {
                    // Insert new SessionLock and SessionItems.
                    sessionItems.Items = Serialize((SessionStateItemCollection)item.Items);
                    sessionItems.ReleaseCount = 0;
                    SessionLock sessionLock = new SessionLock
                    {
                        Id = id,
                        TimeOutInMinutes = item.Timeout,
                        ExpirationDate = DateTime.UtcNow.AddMinutes(item.Timeout),
                        DateLocked = DateTime.UtcNow,
                        LockCount = 0
                    };
                    using (var transaction = _datastore.BeginTransaction(_callSettings))
                    {
                        transaction.Upsert(ToEntity(sessionItems), ToEntity(sessionLock));
                        transaction.Commit(_callSettings);
                    }
                    return;
                }
                using (var transaction = _datastore.BeginTransaction(_callSettings))
                {
                    // Update existing session items.
                    sessionItems.Items = item.Items.Dirty ?
                        Serialize((SessionStateItemCollection)item.Items) : lockId.Items;
                    // Unlock the session.
                    sessionItems.ReleaseCount = lockId.LockCount;
                    SessionLock sessionLock = SessionLockFromEntity(
                        transaction.Lookup(_lockKeyFactory.CreateKey(id), _callSettings));
                    if (sessionLock == null || sessionLock.LockCount != lockId.LockCount)
                        return;  // Something else locked it in the meantime.
                    transaction.Upsert(ToEntity(sessionItems));
                    transaction.Commit(_callSettings);
                }
            });
        }

        private byte[] Serialize(SessionStateItemCollection items)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            if (items != null)
                items.Serialize(writer);
            writer.Close();
            return ms.ToArray();
        }

        private static SessionStateStoreData Deserialize(HttpContext context,
            byte[] serializedItems, int timeout)
        {
            SessionStateItemCollection items =
                serializedItems != null && serializedItems.Length > 0 ?
                SessionStateItemCollection.Deserialize(
                    new BinaryReader(new MemoryStream(serializedItems))) :
                new SessionStateItemCollection();

            return new SessionStateStoreData(items,
                SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        /// <summary>
        /// Execute the action, and log any exceptions that are thrown.
        /// </summary>
        /// <param name="message">Print this message if an exception is thrown.</param>
        /// <param name="action">The action to exceute.</param>
        void LogExceptions(string message, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                _log.Error(message, e);
                throw;
            }
        }

        /// <summary>
        /// Pack a SessionLock into a Datastore entity.
        /// </summary>
        Entity ToEntity(SessionLock sessionLock)
        {
            var entity = new Entity();
            entity.Key = _lockKeyFactory.CreateKey(sessionLock.Id);
            entity[LOCK_COUNT] = sessionLock.LockCount;
            entity[LOCK_DATE] = sessionLock.DateLocked;
            entity[TIMEOUT] = sessionLock.TimeOutInMinutes;
            entity[EXPIRES] = sessionLock.ExpirationDate;
            ExcludeFromIndexes(entity, LOCK_COUNT, LOCK_DATE, TIMEOUT);
            return entity;
        }

        /// <summary>
        /// Pack a SessionItems into a Datastore entity.
        /// </summary>
        Entity ToEntity(SessionItems sessionItems)
        {
            var entity = new Entity();
            entity.Key = _sessionKeyFactory.CreateKey(sessionItems.Id);
            entity[ITEMS] = sessionItems.Items;
            entity[RELEASE_COUNT] = sessionItems.ReleaseCount;
            ExcludeFromIndexes(entity, ITEMS, RELEASE_COUNT);
            return entity;
        }

        /// <summary>
        /// Mark the properties to be excluded from indexes.
        /// </summary>
        /// <param name="entity">A datastore entity.</param>
        /// <param name="properties">Property names to exclude from indexes.</param>
        void ExcludeFromIndexes(Entity entity, params string[] properties)
        {
            foreach (string prop in properties)
            {
                entity[prop].ExcludeFromIndexes = true;
            }
        }

        /// <summary>
        /// Unpack a SessionItems instance from a Datastore entity.
        /// </summary>
        /// <param name="id">The id to use if entity in null.</param>
        /// <param name="entity">The datastore entity.</param>
        /// <returns>A SessionItems instance.  Never returns null.</returns>
        private SessionItems SessionItemsFromEntity(string id, Entity entity)
        {
            SessionItems sessionItems = new SessionItems();
            sessionItems.Id = id;
            if (entity != null)
            {
                sessionItems.Items = entity[ITEMS].BlobValue.ToArray();
                sessionItems.ReleaseCount = (int)entity[RELEASE_COUNT];
            }
            else
            {
                sessionItems.Items = new byte[0];
            }
            return sessionItems;
        }

        /// <summary>
        /// Unpack a SessionLock from a Datastore entity.
        /// </summary>
        /// <param name="entity">The datastore entity.</param>
        /// <returns>A SessionLock instance.  Returns null if entity is null.</returns>
        private SessionLock SessionLockFromEntity(Entity entity)
        {
            if (null == entity)
                return null;
            SessionLock sessionLock = new SessionLock();
            sessionLock.Id = entity.Key.Path.First().Name;
            sessionLock.LockCount = (int)entity[LOCK_COUNT];
            sessionLock.DateLocked = (DateTime)entity[LOCK_DATE];
            sessionLock.ExpirationDate = (DateTime)entity[EXPIRES];
            sessionLock.TimeOutInMinutes = (int)entity[TIMEOUT];
            return sessionLock;
        }

        public override void InitializeRequest(HttpContext context)
        {
        }

        public override void EndRequest(HttpContext context)
        {
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        public override void Dispose()
        {
        }

        /// <summary>
        /// The main loop of a Task that periodically cleans up expired sessions.
        /// Never returns.
        /// </summary>
        async Task SweepTaskMain()
        {
            var random = System.Security.Cryptography.RandomNumberGenerator.Create();
            var randomByte = new byte[1];
            while (true)
            {
                random.GetBytes(randomByte);
                // Not a perfect distrubution, but fine for our limited purposes.
                int randomMinute = randomByte[0] % 60;
                _log.DebugFormat("Delaying {0} minutes before checking sweep lock.", randomMinute);
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
                            sweep =
                                DateTime.UtcNow - ((DateTime)sweepLock[SWEEP_BEGIN_DATE]) > TimeSpan.FromHours(1);
                        }
                        catch (Exception e)
                        {
                            _log.Error("Error reading sweep begin date.", e);
                        }
                        if (!sweep)
                        {
                            _log.Debug("Not yet time to sweep.");
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
                    _log.Error("Error acquiring sweep lock.", e);
                    continue;
                }
                try
                {
                    _log.Debug("Beginning sweep.");
                    // Find old sessions to clean up
                    var now = DateTime.UtcNow;
                    var query = new Query(SESSION_LOCK_KIND)
                    {
                        Filter = Filter.LessThan(EXPIRES, now),
                        Projection = { "__key__" }
                    };
                    foreach (Entity lockEntity in _datastore.RunQueryLazily(query))
                    {
                        try
                        {
                            using (var transaction = _datastore.BeginTransaction(_callSettings))
                            {
                                var sessionLock =
                                    SessionLockFromEntity(transaction.Lookup(lockEntity.Key, _callSettings));
                                if (sessionLock == null || sessionLock.ExpirationDate > now)
                                    continue;
                                transaction.Delete(lockEntity.Key,
                                    _sessionKeyFactory.CreateKey(lockEntity.Key.Path.First().Name));
                                transaction.Commit(_callSettings);
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error("Failed to delete session.", e);
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error("Failed to query expired sessions.", e);
                }
            }
        }
    }
}
 