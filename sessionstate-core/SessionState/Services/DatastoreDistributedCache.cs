using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Google.Cloud.Datastore.V1;
using System;
using Microsoft.Extensions.Options;

class DatastoreDistributedCacheOptions
{
    /// <summary>
    /// Your Google project id.
    /// </summary>
    public string ProjectId { get; set; }

    /// <summary>
    /// Optional.  The Datastore namespace to store the sessions in.
    /// </summary>
    public string Namespace { get; set; }
}

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

    public DatastoreDistributedCache(IOptions<DatastoreDistributedCacheOptions> options)
    {
        var opts = options.Value;
        _datastore = DatastoreDb.Create(opts.ProjectId, opts.Namespace);
        _sessionKeyFactory = _datastore.CreateKeyFactory(SESSION_KIND);
    }

    public byte[] Get(string key) => BytesFromEntity(
        _datastore.Lookup(_sessionKeyFactory.CreateKey(key)));

    public async Task<byte[]> GetAsync(string key, 
        CancellationToken token = default(CancellationToken))
    {
        var entity = await _datastore.LookupAsync(_sessionKeyFactory.CreateKey(key), 
            callSettings:Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token));
        return BytesFromEntity(entity);
    }

    public void Refresh(string key)
    {
        using (var transaction = _datastore.BeginTransaction())
        {
            var entity = transaction.Lookup(_sessionKeyFactory.CreateKey(key));
            if (UpdateExpiration(entity))
            {
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
            if (UpdateExpiration(entity)) 
            {
                await transaction.CommitAsync();
            }
        }                
    }

    public void Remove(string key) =>
        _datastore.Delete(_sessionKeyFactory.CreateKey(key));

    public Task RemoveAsync(string key, CancellationToken token = default(CancellationToken)) =>
        _datastore.DeleteAsync(_sessionKeyFactory.CreateKey(key),
            Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        _datastore.Upsert(NewEntity(key, value, options));

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default(CancellationToken)) =>
        _datastore.UpsertAsync(NewEntity(key, value, options),
            Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(token));

    bool HasExpired(Entity entity) {
        var expiration = entity[EXPIRATION]?.TimestampValue?.ToDateTime();
        return expiration.HasValue ? DateTime.UtcNow > expiration.Value : false;
    }

    /// Returns the bytes (cache payload) stored in the entity.
    byte[] BytesFromEntity(Entity entity) {
        if (entity == null || HasExpired(entity))
        {
            return null;
        }
        else
        {
            return entity[BYTES]?.BlobValue?.ToByteArray();
        }        
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
            throw new ArgumentException("Required expiration option was not set.", "options");
        }
        return entity;
    }

    bool UpdateExpiration(Entity entity)
    {
        if (entity == null || HasExpired(entity))
        {
            return false;            
        }
        var slidingExpiration = entity[SLIDING_EXPIRATION]?.DoubleValue;
        if (slidingExpiration.HasValue) 
        {
            entity[EXPIRATION] = DateTime.UtcNow.AddSeconds(slidingExpiration.Value);
            return true;
        }
        return false;        
    }
}