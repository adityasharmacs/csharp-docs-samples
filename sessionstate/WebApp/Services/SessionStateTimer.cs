using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.SessionState;

namespace WebApp.Services
{
    public class SessionStateTimer : SessionStateStoreProviderBase
    {
        private readonly SessionStateStoreProviderBase _innerProvider;

        public SessionStateTimer(SessionStateStoreProviderBase innerProvider)
        {
            _innerProvider = innerProvider;
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return _innerProvider.CreateNewStoreData(context, timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            _innerProvider.CreateUninitializedItem(context, id, timeout);
        }

        public override void Dispose()
        {
            _innerProvider.Dispose();
        }

        public override void EndRequest(HttpContext context)
        {
            _innerProvider.EndRequest(context);
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return _innerProvider.GetItem(context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return _innerProvider.GetItemExclusive(context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override void InitializeRequest(HttpContext context)
        {
            _innerProvider.InitializeRequest(context);
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            _innerProvider.ReleaseItemExclusive(context, id, lockId);
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            _innerProvider.RemoveItem(context, id, lockId, item);
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            _innerProvider.ResetItemTimeout(context, id);
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            _innerProvider.SetAndReleaseItemExclusive(context, id, item, lockId, newItem);
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return _innerProvider.SetItemExpireCallback(expireCallback);
        }
    }
}