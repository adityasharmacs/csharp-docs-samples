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
    struct SessionLock
    {
        public string Id;
        public int Count;
        public DateTime DateLocked;
    };

    struct SessionRelease
    {
        public string Id;
        public int Count;
    }

    struct SessionExpirationDate
    {
        public string Id;
        public DateTime XDate;
        public int TimeOutInMinutes;
    }

    struct SessionItems
    {
        public string Id;
        public byte[] Items;
    }

    class LockId
    {
        public int LockCount { get; set; }
        public int TimeoutInMinutes { get; set; }
    }

    public class DatastoreSessionStateStoreProvider : SessionStateStoreProviderBase
    {
        // Stores a session in datastore.
        // The metadata for the session is stored in a parent entity.
        // And the items (payload) are stored in a child entity.

        readonly Google.Datastore.V1.DatastoreDb _datastore =
            Google.Datastore.V1.DatastoreDb.Create("surferjeff-test2");
        readonly string _applicationName =
            System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;
        readonly Google.Datastore.V1.KeyFactory _sessionKeyFactory,
            _expirationDateKeyFactory, _releaseKeyFactory, _lockKeyFactory;

        // Property names for the datastore entity.
        const string
            EXPIRES = "expires",
            LOCK_DATE = "lockDate",
            LOCK_ID = "lockId",
            TIMEOUT = "timeout",
            LOCKED = "locked",
            ITEMS = "items",
            FLAGS = "flags";

        public DatastoreSessionStateStoreProvider()
        {
            _sessionKeyFactory = _datastore.CreateKeyFactory("Session");
            _expirationDateKeyFactory = _datastore.CreateKeyFactory("SessionXDate");
            _lockKeyFactory = _datastore.CreateKeyFactory("SessionLock");
            _releaseKeyFactory = _datastore.CreateKeyFactory("SessionRelease");
        }


        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(),
                SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            Debug.WriteLine("{0}: CreateUninitializedItem({1})", DateTime.Now, id);
            SessionExpirationDate expirationDate = new SessionExpirationDate();
            expirationDate.Id = id;
            expirationDate.XDate = DateTime.UtcNow.AddMinutes(timeout);
            expirationDate.TimeOutInMinutes = timeout;
            using (var transaction = _datastore.BeginTransaction())
            {
                transaction.Upsert(ToEntity(expirationDate));
                transaction.Delete(_sessionKeyFactory.CreateKey(id));
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
                    _expirationDateKeyFactory.CreateKey(id),
                    _releaseKeyFactory.CreateKey(id));
                SessionExpirationDate? expirationDate = ExpirationDateFromEntity(entities[2]);
                if (expirationDate == null || expirationDate.Value.XDate < DateTime.UtcNow)
                {
                    lockAge = TimeSpan.Zero;
                    locked = false;
                    lockId = null;
                    actions = SessionStateActions.None;
                    return null;
                }
                SessionLock sessionLock = SessionLockFromEntity(entities[1]);
                SessionRelease sessionRelease = SessionReleaseFromEntity(entities[3]);
                locked = sessionLock.Count > sessionRelease.Count;
                lockAge = locked ? DateTime.UtcNow - sessionLock.DateLocked
                    : TimeSpan.Zero;
                int timeout = expirationDate.Value.TimeOutInMinutes;
                lockId = new LockId()
                {
                    LockCount = sessionLock.Count,
                    TimeoutInMinutes = timeout
                };
                actions = SessionStateActions.None;
                if (locked)
                    return null;
                if (exclusive)
                {
                    sessionLock.Count += 1;
                    sessionLock.DateLocked = DateTime.UtcNow;
                    transaction.Update(ToEntity(sessionLock));
                    transaction.Commit();
                }
                SessionItems? sessionItems = SessionItemsFromEntity(entities[0]);
                return sessionItems == null ? CreateNewStoreData(context, timeout) :
                    Deserialize(context, sessionItems.Value.Items, timeout);
            }            
        }

        public override void InitializeRequest(HttpContext context)
        {            
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            Debug.WriteLine("{0}: ReleaseItemExclusive({1})", DateTime.Now, id);
            SessionRelease release = new SessionRelease();
            release.Id = id;
            release.Count = ((LockId)lockId).LockCount;
            _datastore.Upsert(ToEntity(release));
        }


        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            Debug.WriteLine("{0}: RemoveItem({1})", DateTime.Now, id);
            using (var transaction = _datastore.BeginTransaction())
            {
                transaction.Delete(_sessionKeyFactory.CreateKey(id),
                        _lockKeyFactory.CreateKey(id),
                        _expirationDateKeyFactory.CreateKey(id),
                        _releaseKeyFactory.CreateKey(id));
                transaction.Commit();
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            Debug.WriteLine("{0}: ResetItemTimeout({1})", DateTime.Now, id);
            SessionExpirationDate expirationDate = SessionExpirationDateFromEntity(
                _datastore.Lookup(_expirationDateKeyFactory.CreateKey(id)));
            expirationDate.XDate = DateTime.Now.AddMinutes(expirationDate.TimeOutInMinutes);
            _datastore.Upsert(ToEntity(expirationDate));
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockIdObject, bool newItem)
        {
            Debug.WriteLine("{0}: SetAndReleaseItemExclusive({1})", DateTime.Now, id);
            LockId lockId = (LockId)lockIdObject;
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
            sessionRelease.Count = lockId.LockCount;         
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
 