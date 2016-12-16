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

    struct SessionItems
    {
        public string Id;
        public byte[] Items;
        public int ReleaseCount;
    }

    public class DatastoreSessionStateStoreProvider : SessionStateStoreProviderBase
    {
        readonly DatastoreDb _datastore;
        readonly KeyFactory _sessionKeyFactory,
            _lockKeyFactory;
        readonly ILog _log;
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
        readonly CallSettings _callSettings =
            CallSettings.FromCallTiming(CallTiming.FromRetry(new RetrySettings(
                new BackoffSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), 2),
                new BackoffSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), 2),
                Expiration.FromTimeout(TimeSpan.FromSeconds(30)))));

        public DatastoreSessionStateStoreProvider()
        {
            // Read the google project id and the application name from the config.
            Configuration cfg =
              WebConfigurationManager.OpenWebConfiguration(
                  System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath);
            var appSettings = (AppSettingsSection)cfg.GetSection("appSettings");
            string projectId = appSettings.Settings["GoogleProjectId"]?.Value;
            string applicationName = appSettings.Settings["ApplicationName"]?.Value ?? "";
            if (string.IsNullOrWhiteSpace(projectId) || projectId == "YOUR-PROJECT" + "-ID")
            {
                throw new ConfigurationErrorsException("Set the googleProjectId in Web.config");
            }
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

        async Task SweepTaskMain()
        {
            var random = System.Security.Cryptography.RandomNumberGenerator.Create();
            var randomByte = new byte[1];
            while (true)
            {
                random.GetBytes(randomByte);
                // Not a perfect distrubution, but fine for our limited purposes.
                int randomMinute = randomByte[0] % 60;
                _log.InfoFormat("Delaying {0} minutes before sweeping...", randomMinute);
                await Task.Delay(TimeSpan.FromMinutes(randomMinute));
                // Find old sessions to clean up
                var now = DateTime.UtcNow;
                var query = new Query(SESSION_LOCK_KIND)
                {
                    Filter = Filter.LessThan(EXPIRES, now),
                    Projection = { "__key__" }
                };
                try
                {
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
                        catch (Grpc.Core.RpcException e)
                        {
                            _log.Error(e.Message);
                        }
                    }
                }
                catch (Grpc.Core.RpcException e)
                {
                    _log.Error(e.Message);
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
            Debug.WriteLine("{0}: CreateUninitializedItem({1})", DateTime.Now, id);
            var sessionLock = new SessionLock();
            sessionLock.Id = id;
            sessionLock.ExpirationDate = DateTime.UtcNow.AddMinutes(timeout);
            sessionLock.TimeOutInMinutes = timeout;
            sessionLock.DateLocked = DateTime.UtcNow;
            sessionLock.LockCount = 0;
            try
            {
                using (var transaction = _datastore.BeginTransaction(_callSettings))
                {
                    transaction.Upsert(ToEntity(sessionLock));
                    transaction.Delete(_sessionKeyFactory.CreateKey(id));
                    transaction.Commit(_callSettings);
                }
            }
            catch (Grpc.Core.RpcException e)
            {
                _log.Error(e.Message);
            }
        }

        public override void Dispose()
        {
        }

        public override void EndRequest(HttpContext context)
        {
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            Debug.WriteLine("{0}: GetItem({1})", DateTime.Now, id);
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
            Debug.WriteLine("{0}: GetItemExclusive({1})", DateTime.Now, id);
            return GetItemImpl(true, context, id, out locked, out lockAge,
                out lockId, out actions);
        }

        public SessionStateStoreData GetItemImpl(bool exclusive, 
            HttpContext context, string id, out bool locked, 
            out TimeSpan lockAge, out object lockId, 
            out SessionStateActions actions)
        {
            try
            {
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
            catch (Grpc.Core.RpcException e)
            {
                _log.Error(e.Message);
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
            Debug.WriteLine("{0}: ReleaseItemExclusive({1})", DateTime.Now, id);
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
        }


        public override void RemoveItem(HttpContext context, string id, object lockIdObject, SessionStateStoreData item)
        {
            Debug.WriteLine("{0}: RemoveItem({1})", DateTime.Now, id);
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
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            Debug.WriteLine("{0}: ResetItemTimeout({1})", DateTime.Now, id);
            // Ignore it.  To minimize writes to datastore, we reset the timeout
            // when we lock the session.
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockIdObject, bool newItem)
        {
            Debug.WriteLine("{0}: SetAndReleaseItemExclusive({1})", DateTime.Now, id);
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
 