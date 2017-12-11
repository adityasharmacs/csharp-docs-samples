using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Google.Cloud.Datastore.V1;
using System;

class DatastoreDistributedCache : IDistributedCache
{
    /// <summary>
    /// My connection to Google Cloud Datastore.
    /// </summary>
    private DatastoreDb _datastore;
    private KeyFactory _sessionKeyFactory;

    /// <summary>
    /// Property names and kind names for the datastore entities.
    /// </summary>
    private const string
        EXPIRATION = "expires",
        SLIDING_EXPIRATION = "sliding",
        BYTES = "bytes",
        SESSION_KIND = "Session";

    public DatastoreDistributedCache()
    {
        _datastore = DatastoreDb.Create("arc-nl", "sessionStateApplication");
        _sessionKeyFactory = _datastore.CreateKeyFactory(SESSION_KIND);
    }

    public byte[] Get(string key)
    {
        var entity = _datastore.Lookup(_sessionKeyFactory.CreateKey(key));
        if (entity == null || HasExpired(entity))
        {
            return null;
        }
        else
        {
            return entity[BYTES]?.BlobValue?.ToByteArray();
        }
    }

    bool HasExpired(Entity entity) {
        var expiration = entity[EXPIRATION]?.TimestampValue?.ToDateTime();
        return expiration.HasValue ? DateTime.UtcNow > expiration.Value : false;
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken token = default(CancellationToken))
    {
        var entity = await _datastore.LookupAsync(_sessionKeyFactory.CreateKey(key), 
            callSettings:Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token));
        if (entity == null || HasExpired(entity))
        {
            return null;
        }
        else
        {
            return entity[BYTES]?.BlobValue?.ToByteArray();
        }
    }

    public void Refresh(string key)
    {
        using (var transaction = _datastore.BeginTransaction())
        {
            var entity = transaction.Lookup(_sessionKeyFactory.CreateKey(key));
            if (entity == null || HasExpired(entity))
            {
                return;            
            }
            var slidingExpiration = entity[SLIDING_EXPIRATION]?.DoubleValue;
            if (slidingExpiration.HasValue) 
            {
                entity[EXPIRATION] = DateTime.UtcNow.AddSeconds(slidingExpiration.Value);
                transaction.Commit();
            }
        }                
    }

    public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
    {
        using (var transaction = await _datastore.BeginTransactionAsync(
            Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token)))
        {
            var entity = await transaction.LookupAsync(_sessionKeyFactory.CreateKey(key));
            if (entity == null || HasExpired(entity))
            {
                return;            
            }
            var slidingExpiration = entity[SLIDING_EXPIRATION]?.DoubleValue;
            if (slidingExpiration.HasValue) 
            {
                entity[EXPIRATION] = DateTime.UtcNow.AddSeconds(slidingExpiration.Value);
                await transaction.CommitAsync();
            }
        }                
    }

    public void Remove(string key)
    {
        _datastore.Delete(_sessionKeyFactory.CreateKey(key));
    }

    public Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
    {
        return _datastore.DeleteAsync(_sessionKeyFactory.CreateKey(key));
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        _datastore.Insert(NewEntity(key, value, options));
    }

    Entity NewEntity(string key, byte[] value, DistributedCacheEntryOptions options) 
    {
        Entity entity = new Entity()
        {
            Key = _sessionKeyFactory.CreateKey(key),
            [BYTES] = value
        };
        entity[BYTES].ExcludeFromIndexes = true;
        if (options.AbsoluteExpiration.HasValue)
        {
            entity[EXPIRATION] = options.AbsoluteExpiration.Value;
        }
        else if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            entity[EXPIRATION] = DateTime.UtcNow.Add(
                options.AbsoluteExpirationRelativeToNow.Value 
            );
        }
        else if (options.SlidingExpiration.HasValue)
        {
            entity[SLIDING_EXPIRATION] = options.SlidingExpiration.Value.TotalSeconds;
            entity[SLIDING_EXPIRATION].ExcludeFromIndexes = true;
            entity[EXPIRATION] = DateTime.UtcNow.Add(
                options.SlidingExpiration.Value
            );
        }
        else
        {
            throw new ArgumentException("No expiration option was set", "options");
        }
        return entity;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default(CancellationToken))
    {
        return _datastore.InsertAsync(NewEntity(key, value, options),
            Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token));
    }
}