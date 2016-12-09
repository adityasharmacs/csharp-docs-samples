using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.SessionState;

namespace WebApp.Services
{
    public class DatastoreSessionStateStoreProvider : SessionStateStoreProviderBase
    {
        readonly Google.Datastore.V1.DatastoreDb _datastore =
            Google.Datastore.V1.DatastoreDb.Create(
                Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID"));
        readonly string _applicationName =
            System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;
        readonly Google.Datastore.V1.KeyFactory _keyFactory;

        // Property names for the datastore entity.
        const string CREATED = "created",
            EXPIRES = "expires",
            LOCK_DATE = "lockDate",
            LOCK_ID = "lockId",
            TIMEOUT = "timeout",
            LOCKED = "locked",
            ITEMS = "items",
            FLAGS = "flags";

        readonly string[] UNINDEXED_PROPERTIES =
        {  // Everything but expires
            CREATED,
            LOCK_DATE,
            LOCK_ID,
            TIMEOUT,
            LOCKED,
            FLAGS,
        };

        DatastoreSessionStateStoreProvider()
        {
            _keyFactory = _datastore.CreateKeyFactory("Session");
        }

        private Google.Datastore.V1.Entity NewEntity(string id, int timeout,
            SessionStateActions flags = SessionStateActions.None)
        {
            var now = DateTime.UtcNow;
            var entity = new Google.Datastore.V1.Entity()
            {
                Key = EntityKeyFromSessionId(id),
                [CREATED] = now,
                [EXPIRES] = now + TimeSpan.FromMinutes(timeout),
                [LOCK_DATE] = now,
                [LOCK_ID] = 0,
                [TIMEOUT] = timeout,
                [LOCKED] = false,
                [FLAGS] = (int) flags
            };
            foreach (string prop in UNINDEXED_PROPERTIES)
            {
                entity[prop].ExcludeFromIndexes = true;
            }
            return entity;
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(),
                SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            var now = DateTime.UtcNow;
            var entity = NewEntity(id, timeout, SessionStateActions.InitializeItem);
            _datastore.Upsert(entity);
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
            return GetItemImpl(true, context, id, out locked, out lockAge,
                out lockId, out actions);
        }

        public SessionStateStoreData GetItemImpl(bool exclusive, 
            HttpContext context, string id, out bool locked, 
            out TimeSpan lockAge, out object lockId, 
            out SessionStateActions actions)
        {
            using (var transaction = _datastore.BeginTransaction())
            {
                var key = EntityKeyFromSessionId(id);
                var entity = transaction.Lookup(key);
                if (entity == null || 
                    (DateTime)entity[EXPIRES] < DateTime.UtcNow)
                {
                    lockAge = TimeSpan.Zero;
                    locked = false;
                    lockId = null;
                    actions = SessionStateActions.None;
                    return null;
                }
                locked = (bool)entity[LOCKED];
                lockAge = locked ? DateTime.UtcNow - (DateTime)entity[LOCK_DATE]
                    : TimeSpan.Zero;
                lockId = entity[LOCK_ID];
                actions = (SessionStateActions)(int)entity[FLAGS];
                if (locked)
                    return null;
                if (exclusive)
                {
                    entity[LOCKED] = true;
                    entity[LOCK_DATE] = DateTime.UtcNow;
                    entity[LOCK_ID] = (int)entity[LOCK_ID] + 1;
                    transaction.Update(entity);
                }
                if (actions == SessionStateActions.InitializeItem)
                {
                    if (exclusive) transaction.Commit();
                    return CreateNewStoreData(context, (int)entity[TIMEOUT]);
                }
                else
                {
                    var items = transaction.Lookup(ItemsKeyFromEntityKey(key));
                    if (exclusive) transaction.Commit();
                    return items == null ? CreateNewStoreData(context, (int)entity[TIMEOUT]) :
                        Deserialize(context, items[ITEMS].BlobValue.ToArray(), (int)entity[TIMEOUT]);
                }
            }            
        }

        public override void InitializeRequest(HttpContext context)
        {            
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            using (var transaction = _datastore.BeginTransaction())
            {
                var entity = transaction.Lookup(EntityKeyFromSessionId(id));
                if (entity == null || !LockIdsMatch(lockId, entity[LOCK_ID]))
                    return;
                entity[LOCKED] = false;
                entity[EXPIRES] = DateTime.UtcNow + TimeSpan.FromMinutes((int)entity[TIMEOUT]);
                transaction.Commit();
            }
        }

        private static bool LockIdsMatch(Google.Datastore.V1.Value a, object b)
        {
            if (a == null)
                return b == null;
            if (b == null)
                return false;
            return a.IntegerValue == (int)b;
        }
        private static bool LockIdsMatch(object a, Google.Datastore.V1.Value b)
        {
            return LockIdsMatch(b, a);
        }

        private Google.Datastore.V1.Key EntityKeyFromSessionId(string id)
        {
            return _keyFactory.CreateKey($"{id}-{_applicationName}");
        }

        private Google.Datastore.V1.Key ItemsKeyFromEntityKey(Google.Datastore.V1.Key entityKey)
        {
            return entityKey.WithElement(new Google.Datastore.V1.Key.Types.PathElement("Items", 1));
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            var key = EntityKeyFromSessionId(id);
            using (var transaction = _datastore.BeginTransaction())
            {
                var entity = transaction.Lookup(key);
                if (entity == null)
                    return;
                if (!LockIdsMatch(lockId, entity["lockId"]))
                    return;
                transaction.Delete(ItemsKeyFromEntityKey(key), key);
                transaction.Commit();
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            var key = EntityKeyFromSessionId(id);
            using (var transaction = _datastore.BeginTransaction())
            {
                var entity = transaction.Lookup(key);
                entity[EXPIRES] = DateTime.UtcNow + TimeSpan.FromMinutes((int)entity[TIMEOUT]);
                transaction.Update(entity);
                transaction.Commit();
            }
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            var key = EntityKeyFromSessionId(id);
            var itemBlob = Serialize((SessionStateItemCollection)item.Items);
            var items = new Google.Datastore.V1.Entity()
            {
                Key = ItemsKeyFromEntityKey(key),
                [ITEMS] = itemBlob
            };
            Google.Datastore.V1.Entity entity;
            if (newItem)
            {
                entity = NewEntity(id, item.Timeout);
                _datastore.Upsert(entity, items);
            }
            else
            {
                using (var transaction = _datastore.BeginTransaction())
                {
                    entity = transaction.Lookup(key);
                    if (!LockIdsMatch(lockId, entity[LOCK_ID]))
                        return;
                    entity[EXPIRES] = DateTime.UtcNow + TimeSpan.FromMinutes(item.Timeout);
                    entity[LOCKED] = false;
                    transaction.Upsert(entity, items);
                    transaction.Commit();
                }
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
 