using Google.Cloud.Datastore.V1;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using System.Linq;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using static Google.Cloud.Datastore.V1.Key.Types;
using Grpc.Core;

namespace Sudokumb
{
    public class DatastoreUserStore<U> : IUserPasswordStore<U>, IUserRoleStore<U>, IUserStore<U>
        where U : IdentityUser<string>, IDatastoreUser, new()
    {
        readonly DatastoreDb _datastore;
        readonly KeyFactory _userKeyFactory;
        readonly KeyFactory _nnindexKeyFactory;

        const string
            USER_KIND = "webuser",
            NORMALIZED_EMAIL = "normalized-email",            
            NORMALIZED_NAME = "normalized-name",            
            USER_NAME = "user-name",            
            CONCURRENCY_STAMP = "concurrency-stamp",            
            PASSWORD_HASH = "password-hash",            
            ROLES = "roles",
            NORMALIZED_NAME_INDEX_KIND = "webuser-nnindex",
            USER_KEY = "key";

        public DatastoreUserStore(DatastoreDb datastore)
        {
            _datastore = datastore;
            _userKeyFactory = new KeyFactory(_datastore.ProjectId,
                _datastore.NamespaceId, USER_KIND);
            _userKeyFactory = new KeyFactory(_datastore.ProjectId,
                _datastore.NamespaceId, NORMALIZED_NAME_INDEX_KIND);
        }

        Key KeyFromUserId(string userId) => _userKeyFactory.CreateKey(userId);

        Entity UserToEntity(U user)
        {
            var entity = new Entity()
            {
                [NORMALIZED_EMAIL] = user.NormalizedEmail,
                [NORMALIZED_NAME] = user.NormalizedUserName,
                [USER_NAME] = user.UserName,
                [CONCURRENCY_STAMP] = user.ConcurrencyStamp,
                [PASSWORD_HASH] = user.PasswordHash,
                [ROLES] = user.Roles.ToArray(),
                Key = KeyFromUserId(user.Id)
            };
            entity[CONCURRENCY_STAMP].ExcludeFromIndexes = true;
            entity[PASSWORD_HASH].ExcludeFromIndexes = true;
            // Normalized name has its own separate index.
            entity[NORMALIZED_NAME].ExcludeFromIndexes = true;
            return entity;
        }

        U EntityToUser(Entity entity)
        {
            if (null == entity)
            {
                return null;
            }
            U user = new U()
            {
                Id = entity.Key.Path.First().Name,
                NormalizedUserName = (string)entity[NORMALIZED_NAME],
                NormalizedEmail = (string)entity[NORMALIZED_EMAIL],
                UserName = (string)entity[USER_NAME],
                PasswordHash = (string)entity[PASSWORD_HASH],
                ConcurrencyStamp = (string)entity[CONCURRENCY_STAMP],
                Roles = (null == entity[ROLES] ?
                    new List<string>() : ((string[])entity[ROLES]).ToList())
            };
            return user;
        }

        public Task<IdentityResult> CreateAsync(U user,
            CancellationToken cancellationToken)
        {
            var entity = UserToEntity(user);
            entity.Key = _userKeyFactory.CreateKey(Guid.NewGuid().ToString());
            Entity indexEntity = new Entity()
            {
                Key = _nnindexKeyFactory.CreateKey(user.NormalizedUserName),
                [USER_KEY] = entity.Key
            };
            indexEntity[USER_KEY].ExcludeFromIndexes = true;
            return InTransactionAsync(
                cancellationToken, async (transaction, callSettings) =>
            {
                transaction.Insert(new [] { entity, indexEntity });
                await transaction.CommitAsync(callSettings);
            });
        }

        public Task<IdentityResult> DeleteAsync(U user,
            CancellationToken cancellationToken)
        {
            return InTransactionAsync(
                cancellationToken, async (transaction, callSettings) =>
            {
                transaction.Delete(new [] { KeyFromUserId(user.Id), 
                    _nnindexKeyFactory.CreateKey(user.NormalizedUserName) });
                await transaction.CommitAsync(callSettings);
            });
        }

        public async Task<U> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            return EntityToUser(await _datastore.LookupAsync(KeyFromUserId(userId),
                callSettings: CallSettings.FromCancellationToken(cancellationToken)));
        }

        public async Task<U> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            try
            {
                CallSettings callSettings =
                    CallSettings.FromCancellationToken(cancellationToken);
                using (var transaction = await _datastore.BeginTransactionAsync(
                    callSettings))
                {
                    var indexEntity = await transaction.LookupAsync(
                        _nnindexKeyFactory.CreateKey(normalizedUserName),
                        callSettings);
                    if (null == indexEntity) 
                    {
                        return null;
                    }
                    return EntityToUser(await transaction.LookupAsync(
                        (Key)indexEntity[USER_KEY], callSettings));
                }
            }
            catch (Grpc.Core.RpcException e)
            when (e.Status.StatusCode == StatusCode.NotFound)
            {
                return null;
            }            
        }

        public Task<string> GetNormalizedUserNameAsync(U user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task<string> GetUserIdAsync(U user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id.ToString());
        }

        public Task<string> GetUserNameAsync(U user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }

        public Task SetNormalizedUserNameAsync(U user, string normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(U user, string userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }
        void IDisposable.Dispose()
        {
        }

        public async Task<IdentityResult> UpdateAsync(U user, CancellationToken cancellationToken)
        {
            if (user.WasNormalizedNameModified)
            {
                return await Rpc.TranslateExceptionsAsync(() =>
                    _datastore.UpsertAsync(UserToEntity(user), 
                    CallSettings.FromCancellationToken(cancellationToken)));
            }
            return await InTransactionAsync(cancellationToken, async (transaction, callSettings) =>
            {
                // WIP
            });
        }

        public Task AddToRoleAsync(U user, string roleName, CancellationToken cancellationToken)
        {
            user.Roles.Add(roleName);
            return Task.CompletedTask;
        }

        public Task RemoveFromRoleAsync(U user, string roleName, CancellationToken cancellationToken)
        {
            user.Roles.Remove(roleName);
            return Task.CompletedTask;
        }

        public Task<IList<string>> GetRolesAsync(U user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Roles);
        }

        public Task<bool> IsInRoleAsync(U user, string roleName, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Roles.Contains(roleName));
        }

        public async Task<IList<U>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            var result = await _datastore.RunQueryAsync(new Query(USER_KIND)
            {
                Filter = Filter.Equal(ROLES, roleName)
            });
            return result.Entities.Select(e => EntityToUser(e)).ToList();
        }

        public Task SetPasswordHashAsync(U user, string passwordHash, CancellationToken cancellationToken)
        {
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string> GetPasswordHashAsync(U user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(U user, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(user.PasswordHash));
        }

        async Task<IdentityResult> InTransactionAsync(
            
            CancellationToken cancellationToken,
            Func<DatastoreTransaction, CallSettings, Task> f)
        {
            try
            {
                CallSettings callSettings =
                    CallSettings.FromCancellationToken(cancellationToken);
                using (var transaction = await _datastore.BeginTransactionAsync(
                    callSettings))
                {
                    await f(transaction, callSettings);
                }
                return IdentityResult.Success;
            }
            catch (Grpc.Core.RpcException e)
            {
                return IdentityResult.Failed(new IdentityError()
                {
                    Code = e.Status.Detail,
                    Description = e.Message
                });
            }            
        }
    }
}
