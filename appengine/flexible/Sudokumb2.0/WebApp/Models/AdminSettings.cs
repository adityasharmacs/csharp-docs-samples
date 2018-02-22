using System;
using System.Threading.Tasks;
using Google.Cloud.Datastore.V1;
using Microsoft.Extensions.Caching.Memory;

namespace WebApp.Models
{
    /// <summary>
    /// Stores settings readable by everyone, but should only be set by
    /// administrators.
    /// </summary>
    public class AdminSettings
    {
        /// <summary>
        /// Settings get stored in datastore.
        /// </summary>
        readonly DatastoreDb datastore_;
        /// <summary>
        /// The key to the one entity that contains all the settings.
        /// </summary>
        readonly Key key_;

        // Cache the datastore entity so we don't query datastore a million
        // times, which would slow us down.  Performance optimization.
        object cachedEntityLock_ = new object();
        Task<Entity> cachedEntity_;
        DateTime cachedEntityExpires_;

        static object firstInstanceLock_ = new object();
        static AdminSettings firstInstance_;

        public AdminSettings(DatastoreDb datastore)
        {
            cachedEntityExpires_ = DateTime.MinValue;
            datastore_ = datastore;
            key_ = new KeyFactory(datastore.ProjectId, datastore.NamespaceId,
                ENTITY_KIND).CreateKey(1);
            lock (firstInstanceLock_)
            {
                if (null == firstInstance_)
                {
                    firstInstance_ = this;
                }
            }
        }

        static public AdminSettings FirstInstance
        {
            get
            {
                lock(firstInstanceLock_) return firstInstance_;
            }
        }

        const string ENTITY_KIND = "AdminSettings",
            DUMB = "dumb";

        /// <summary>
        /// Dumb means every next possible move on the sudoku board gets
        /// its own pub/sub message.
        /// </summary>
        public async Task<bool> IsDumbAsync()
        {
            var entity = await LookupEntityAsync();
            return null == entity ? false : (bool)entity[DUMB];
        }

        Task<Entity> LookupEntityAsync()
        {
            lock(cachedEntityLock_)
            {
                var now = DateTime.Now;
                if (now > cachedEntityExpires_)
                {
                    cachedEntityExpires_ = now.AddSeconds(10);
                    cachedEntity_ = datastore_.LookupAsync(key_);
                }
                return cachedEntity_;
            }
        }

        public Task SetDumbAsync(bool dumb)
        {
            Entity entity = new Entity()
            {
                Key = key_,
                [DUMB] = dumb
            };
            lock(cachedEntityLock_)
            {
                cachedEntity_ = Task.FromResult(entity);
                cachedEntityExpires_ = DateTime.Now.AddSeconds(10);
            }
            return datastore_.UpsertAsync(entity);
        }
    }
}