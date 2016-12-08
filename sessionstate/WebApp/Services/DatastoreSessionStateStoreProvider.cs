using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.SessionState;

namespace WebApp.Services
{
    public class DatastoreSessionStateStoreProvider : SessionStateStoreProviderBase
    {
        readonly Google.Datastore.V1.DatastoreDb _client = 
            Google.Datastore.V1.DatastoreDb.Create(
                Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID"));
        readonly string _applicationName = 
            System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;
        readonly Google.Datastore.V1.KeyFactory _keyFactory;

        DatastoreSessionStateStoreProvider()
        {
            _keyFactory = _client.CreateKeyFactory("Session");
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(),
                SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            // I think I can get away with doing nothing here.  Not much point in inserting an
            // empty value into Datastore.
        }

        public override void Dispose()
        {
        }

        public override void EndRequest(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            throw new NotImplementedException();
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            throw new NotImplementedException();
        }

        public override void InitializeRequest(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            throw new NotImplementedException();
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

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            var key = EntityKeyFromSessionId(id);
            using (var transaction = _client.BeginTransaction())
            {
                var entity = transaction.Lookup(key);
                if (entity == null)
                    return;
                if (!LockIdsMatch(lockId, entity["lockId"]))
                    return;
                transaction.Delete(key);
                transaction.Commit();
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            throw new NotImplementedException();
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            throw new NotImplementedException();
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }
    }
}
 