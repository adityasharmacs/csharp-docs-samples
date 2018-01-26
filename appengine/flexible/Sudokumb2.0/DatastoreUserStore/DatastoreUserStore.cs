using Google.Cloud.Datastore.V1;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using System.Linq;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace Sudokumb
{
    public class DatastoreUserStore<U> : IUserPasswordStore<U>, IUserRoleStore<U>, IUserStore<U> where U : IdentityUser, new()
    {
        DatastoreDb _datastore;
        KeyFactory _userKeyFactory;

        static string
            KIND = "webuser",
            NORMALIZED_EMAIL = "normalized-email",
            NORMALIZED_NAME = "normalized-name",
            USER_NAME = "user-name",
            CONCURRENCY_STAMP = "concurrency-stamp",
            PASSWORD_HASH = "password-hash";

        public DatastoreUserStore(DatastoreDb datastore)
        {
            _datastore = datastore;
            _userKeyFactory = new KeyFactory(_datastore.ProjectId, _datastore.NamespaceId, KIND);
        }

        Key KeyFromUserId(string userId) => _userKeyFactory.CreateKey(userId);

        Entity UserToEntity(U user) {
            var entity = new Entity() 
            {
                [NORMALIZED_EMAIL] = user.NormalizedEmail,
                [NORMALIZED_NAME] = user.NormalizedUserName,
                [USER_NAME] = user.UserName,
                [CONCURRENCY_STAMP] = user.ConcurrencyStamp,
                [PASSWORD_HASH] = user.PasswordHash,
                Key = KeyFromUserId(user.Id)
            };
            entity[CONCURRENCY_STAMP].ExcludeFromIndexes = true;
            entity[PASSWORD_HASH].ExcludeFromIndexes = true;
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
                NormalizedUserName = (string)entity[NORMALIZED_NAME],
                NormalizedEmail = (string)entity[NORMALIZED_EMAIL],
                UserName = (string)entity[USER_NAME],
                PasswordHash = (string)entity[PASSWORD_HASH],
                ConcurrencyStamp = (string)entity[CONCURRENCY_STAMP]
            };
            return user;
        }


        public async Task<IdentityResult> CreateAsync(U user,
            CancellationToken cancellationToken)
        {                        
            return await Rpc.WrapExceptionsAsync(() => 
                _datastore.InsertAsync(UserToEntity(user), CallSettings.FromCancellationToken(cancellationToken)));
        }

        public async Task<IdentityResult> DeleteAsync(U user,
            CancellationToken cancellationToken)
        {
            return await Rpc.WrapExceptionsAsync(() => 
                _datastore.DeleteAsync(KeyFromUserId(user.Id), CallSettings.FromCancellationToken(cancellationToken)));                        
        }

        public async Task<U> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            return EntityToUser(await _datastore.LookupAsync(KeyFromUserId(userId),
                callSettings:CallSettings.FromCancellationToken(cancellationToken)));            
        }

        public async Task<U> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            var result = await _datastore.RunQueryAsync(new Query(KIND) {
                Filter = Filter.Equal(NORMALIZED_NAME, normalizedUserName)
            });
            return EntityToUser(result.Entities.FirstOrDefault());
        }

        public Task<string> GetNormalizedUserNameAsync(U user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task<string> GetUserIdAsync(U user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id);
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
            return await Rpc.WrapExceptionsAsync(() => 
                _datastore.UpsertAsync(UserToEntity(user), CallSettings.FromCancellationToken(cancellationToken)));
        }

        public Task AddToRoleAsync(U user, string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task RemoveFromRoleAsync(U user, string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> GetRolesAsync(U user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsInRoleAsync(U user, string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IList<U>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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
    }
}
