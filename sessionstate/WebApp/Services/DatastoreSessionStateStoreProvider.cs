using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.SessionState;

namespace WebApp.Services
{
    class SessionLock
    {
        public string Id;
        public int Count;
        public DateTime DateLocked;
        // Expiration date of the whole session, not just the lock.
        public DateTime ExpirationDate;
        public int TimeOutInMinutes;
    };

    struct SessionRelease
    {
        public string Id;
        public int Count;
    }

    struct SessionItems
    {
        public string Id;
        public byte[] Items;
    }

    public class DatastoreSessionStateStoreProvider : SessionStateStoreProviderBase
    {
        // Stores a session in datastore.
        // The metadata for the session is stored in a parent entity.
        // And the items (payload) are stored in a child entity.

        readonly Google.Datastore.V1.DatastoreDb _datastore =
            Google.Datastore.V1.DatastoreDb.Create("bookshelf-dotnet");
        readonly string _applicationName =
            System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;
        readonly Google.Datastore.V1.KeyFactory _sessionKeyFactory,
            _releaseKeyFactory, _lockKeyFactory;

        // Property names for the datastore entity.
        const string
            EXPIRES = "expires",
            LOCK_DATE = "lockDate",
            LOCK_COUNT = "lockCount",
            TIMEOUT = "timeout",
            ITEMS = "items";

        public DatastoreSessionStateStoreProvider()
        {
            _sessionKeyFactory = _datastore.CreateKeyFactory("Session");
            _lockKeyFactory = _datastore.CreateKeyFactory("SessionLock");
            _releaseKeyFactory = _datastore.CreateKeyFactory("SessionRelease");
        }

        void ExcludeFromIndexes(Google.Datastore.V1.Entity entity, params string[] properties)
        {
            foreach (string prop in properties)
            {
                entity[prop].ExcludeFromIndexes = true;
            }
        }

        Google.Datastore.V1.Entity ToEntity(SessionLock sessionLock)
        {
            var entity = new Google.Datastore.V1.Entity();
            entity.Key = _lockKeyFactory.CreateKey(sessionLock.Id);
            entity[LOCK_COUNT] = sessionLock.Count;
            entity[LOCK_DATE] = sessionLock.DateLocked;
            entity[TIMEOUT] = sessionLock.TimeOutInMinutes;
            entity[EXPIRES] = sessionLock.ExpirationDate;
            ExcludeFromIndexes(entity, LOCK_COUNT, LOCK_DATE, TIMEOUT);
            return entity;
        }

        Google.Datastore.V1.Entity ToEntity(SessionRelease sessionRelease)
        {
            var entity = new Google.Datastore.V1.Entity();
            entity.Key = _releaseKeyFactory.CreateKey(sessionRelease.Id);
            entity[LOCK_COUNT] = sessionRelease.Count;
            ExcludeFromIndexes(entity, LOCK_COUNT);
            return entity;
        }

        Google.Datastore.V1.Entity ToEntity(SessionItems sessionItems)
        {
            var entity = new Google.Datastore.V1.Entity();
            entity.Key = _sessionKeyFactory.CreateKey(sessionItems.Id);
            entity[ITEMS] = sessionItems.Items;
            ExcludeFromIndexes(entity, ITEMS);
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
            sessionLock.Count = 0;
            using (var transaction = _datastore.BeginTransaction())
            {
                transaction.Upsert(ToEntity(sessionLock));
                transaction.Delete(_sessionKeyFactory.CreateKey(id),
                    _releaseKeyFactory.CreateKey(id));
                transaction.Commit();
            }
        }

        public override void Dispose()
        {
        }

        public override void EndRequest(HttpContext context)
        {
            // Nothing to do here, I guess.
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
            try
            {
                return GetItemImpl(true, context, id, out locked, out lockAge,
                    out lockId, out actions);
            }
            catch (Grpc.Core.RpcException e) when (e.Status.StatusCode == Grpc.Core.StatusCode.Aborted)
            {
                Debug.WriteLine("Too much contention.");
                locked = true;
                lockAge = TimeSpan.Zero;
                lockId = 0;
                actions = SessionStateActions.None;
                return null;
            }
        }

        public SessionStateStoreData GetItemImpl(bool exclusive, 
            HttpContext context, string id, out bool locked, 
            out TimeSpan lockAge, out object lockId, 
            out SessionStateActions actions)
        {
            using (var transaction = _datastore.BeginTransaction())
            {
                var entities = transaction.Lookup(
                    _sessionKeyFactory.CreateKey(id),
                    _lockKeyFactory.CreateKey(id),
                    _releaseKeyFactory.CreateKey(id));
                SessionLock sessionLock = SessionLockFromEntity(entities[1]);
                if (sessionLock == null || sessionLock.ExpirationDate < DateTime.UtcNow)
                {
                    lockAge = TimeSpan.Zero;
                    locked = false;
                    lockId = null;
                    actions = SessionStateActions.None;
                    return null;
                }
                SessionRelease sessionRelease = SessionReleaseFromEntity(id, entities[2]);
                locked = sessionLock.Count > sessionRelease.Count;
                lockAge = locked ? DateTime.UtcNow - sessionLock.DateLocked
                    : TimeSpan.Zero;
                lockId = sessionLock;
                actions = SessionStateActions.None;
                if (locked)
                    return null;
                if (exclusive)
                {
                    sessionLock.Count = sessionRelease.Count + 1;
                    sessionLock.DateLocked = DateTime.UtcNow;
                    sessionLock.ExpirationDate = 
                        DateTime.UtcNow.AddMinutes(sessionLock.TimeOutInMinutes);
                    transaction.Upsert(ToEntity(sessionLock));
                    transaction.Commit();
                    locked = true;
                }
                SessionItems? sessionItems = SessionItemsFromEntity(entities[0]);
                return sessionItems == null ? 
                    CreateNewStoreData(context, sessionLock.TimeOutInMinutes) :
                    Deserialize(context, sessionItems.Value.Items, sessionLock.TimeOutInMinutes);
            }            
        }

        private SessionItems? SessionItemsFromEntity(Google.Datastore.V1.Entity entity)
        {
            if (null == entity)
                return null;
            SessionItems sessionItems = new SessionItems();
            sessionItems.Id = entity.Key.Path.First().Name;
            sessionItems.Items = entity[ITEMS].BlobValue.ToArray();
            return sessionItems;
        }

        private SessionLock SessionLockFromEntity(Google.Datastore.V1.Entity entity)
        {
            if (null == entity)
                return null;
            SessionLock sessionLock = new SessionLock();
            sessionLock.Id = entity.Key.Path.First().Name;
            sessionLock.Count = (int)entity[LOCK_COUNT];
            sessionLock.DateLocked = (DateTime)entity[LOCK_DATE];
            sessionLock.ExpirationDate = (DateTime)entity[EXPIRES];
            sessionLock.TimeOutInMinutes = (int)entity[TIMEOUT];
            return sessionLock;
        }

        private SessionRelease SessionReleaseFromEntity(string id, Google.Datastore.V1.Entity entity)
        {
            SessionRelease sessionRelease = new SessionRelease();
            sessionRelease.Id = id;
            if (entity != null)
            {
                sessionRelease.Count = (int)entity[LOCK_COUNT];
            }
            return sessionRelease;
        }

        public override void InitializeRequest(HttpContext context)
        {            
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            Debug.WriteLine("{0}: ReleaseItemExclusive({1})", DateTime.Now, id);
            SessionRelease release = new SessionRelease();
            release.Id = id;
            release.Count = ((SessionLock)lockId).Count;
            _datastore.Upsert(ToEntity(release));
        }


        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            Debug.WriteLine("{0}: RemoveItem({1})", DateTime.Now, id);
            using (var transaction = _datastore.BeginTransaction())
            {
                transaction.Delete(_sessionKeyFactory.CreateKey(id),
                        _lockKeyFactory.CreateKey(id),
                        _releaseKeyFactory.CreateKey(id));
                transaction.Commit();
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            Debug.WriteLine("{0}: ResetItemTimeout({1})", DateTime.Now, id);
            // Ignore it.  To minimize writes to datastore, we reset the timeout
            // when we lock the session.
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            Debug.WriteLine("{0}: SetAndReleaseItemExclusive({1})", DateTime.Now, id);
            SessionLock sessionLock = (SessionLock)lockId;
            var entities = new List<Google.Datastore.V1.Entity>();
            if (item.Items.Dirty)
            {
                SessionItems sessionItems = new SessionItems();
                sessionItems.Id = id;
                sessionItems.Items = Serialize((SessionStateItemCollection)item.Items);
                entities.Add(ToEntity(sessionItems));
            }
            SessionRelease sessionRelease = new SessionRelease();
            sessionRelease.Id = id;
            sessionRelease.Count = newItem ? 0 : sessionLock.Count;         
            entities.Add(ToEntity(sessionRelease));
            using (var transaction = _datastore.BeginTransaction())
            {
                transaction.Upsert(entities);
                transaction.Commit();
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
 