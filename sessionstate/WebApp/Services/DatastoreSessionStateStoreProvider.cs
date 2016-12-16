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
    class SessionLock
    {
        public string Id;
        public int LockCount;
        public DateTime DateLocked;
        // Expiration date of the whole session, not just the lock.
        public DateTime ExpirationDate;
        public int TimeOutInMinutes;
        // Never stored in datastore.  Keep an original copy of the items
        // in case we have to break the lock in ReleaseItemExclusive.
        public byte[] Items;
    };

    class SessionItems
    {
        public string Id;
        public byte[] Items;
        public int ReleaseCount;
    }

    public class DatastoreSessionStateStoreProvider : SessionStateStoreProviderBase
    {
        DatastoreDb _datastore;
        KeyFactory _sessionKeyFactory, _lockKeyFactory;
        ILog _log;
        static Task _sweepTask;
        static Object _sweepTaskLock = new object();

        // Property names for the datastore entities.
        const string
            EXPIRES = "expires",
            LOCK_DATE = "lockDate",
            LOCK_COUNT = "lockCount",
            RELEASE_COUNT = "releaseCount",
            TIMEOUT = "timeout",
            ITEMS = "items";
        const string SESSION_KIND = "Session";
        const string SESSION_LOCK_KIND = "SessionLock";
        const string SWEEP_LOCK_KIND = "SweepLock";
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
                            SWEEPER = "sweeper";
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

        void ExcludeFromIndexes(Entity entity, params string[] properties)
        {
            foreach (string prop in properties)
            {
                entity[prop].ExcludeFromIndexes = true;
            }
        }

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

        Entity ToEntity(SessionItems sessionItems)
        {
            var entity = new Entity();
            entity.Key = _sessionKeyFactory.CreateKey(sessionItems.Id);
            entity[ITEMS] = sessionItems.Items;
            entity[RELEASE_COUNT] = sessionItems.ReleaseCount;
            ExcludeFromIndexes(entity, ITEMS, RELEASE_COUNT);
            return entity;
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

        public override void Dispose()
        {
        }

        public override void EndRequest(HttpContext context)
        {
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            _log.DebugFormat("GetItem({0})", id);
            return GetItemImpl(false, context, id, out locked, out lockAge,
                out lockId, out actions);
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
                    var entities = transaction.Lookup(new Key[]
                    {
                        _sessionKeyFactory.CreateKey(id),
                        _lockKeyFactory.CreateKey(id)
                    }, _callSettings);
                    SessionLock sessionLock = SessionLockFromEntity(entities[1]);
                    if (sessionLock == null || sessionLock.ExpirationDate < DateTime.UtcNow)
                    {
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


        public override void RemoveItem(HttpContext context, string id, object lockIdObject, SessionStateStoreData item)
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
                    sessionItems.Items = item.Items.Dirty ?
                        Serialize((SessionStateItemCollection)item.Items) : lockId.Items;
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

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }
    }
}
 