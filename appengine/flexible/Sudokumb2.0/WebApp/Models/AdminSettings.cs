using System;
using System.Threading.Tasks;
using Google.Cloud.Datastore.V1;
using Microsoft.Extensions.Caching.Memory;

namespace WebApp.Models
{
    class AdminSettings
    {
        readonly DatastoreDb datastore_;
        readonly Key key_;

        MemoryCache cache_;

        public AdminSettings(DatastoreDb datastore, MemoryCache cache)
        {
            cache_ = cache;
            datastore_ = datastore;
            key_ = new KeyFactory(datastore.ProjectId, datastore.NamespaceId, ENTITY_KIND).CreateKey(0);
        }

        const string ENTITY_KIND = "AdminSettings",
            DUMB = "dumb";

        public Task<bool> IsDumbAsync()
        {
            object isDumbObject;
            if (cache_.TryGetValue(DUMB, out isDumbObject))
            {
                return (Task<bool>)isDumbObject;
            }
            else
            {
                Task<bool> result = LookupIsDumbAsync();
                cache_.Set(DUMB, result, TimeSpan.FromSeconds(10));
                return result;
            }
        }

        async Task<bool> LookupIsDumbAsync()
        {
            Entity entity = await datastore_.LookupAsync(key_);
            return entity != null || (bool)entity[DUMB];
        }

        public Task SetDumbAsync(bool dumb)
        {
            Entity entity = new Entity()
            {
                Key = key_,
                [DUMB] = dumb
            };
            return datastore_.UpsertAsync(entity);
        }
    }
}