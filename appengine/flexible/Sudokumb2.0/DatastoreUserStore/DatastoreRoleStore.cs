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
    public class DatastoreRoleStore<R> : IRoleStore<R> where R : IdentityRole, new()
    {
        DatastoreDb _datastore;
        KeyFactory _roleKeyFactory;


        static string
            KIND = "webuserrole",
            NORMALIZED_NAME = "normalized-name",
            ROLE_NAME = "name",
            CONCURRENCY_STAMP = "concurrency-stamp";

        public DatastoreRoleStore(DatastoreDb datastore)
        {
            _datastore = datastore;
            _roleKeyFactory = new KeyFactory(_datastore.ProjectId, _datastore.NamespaceId, KIND);
        }

        Key KeyFromRoleId(string roleId) => _roleKeyFactory.CreateKey(roleId);

        Entity RoleToEntity(R role) 
        {
            var entity = new Entity() 
            {
                [NORMALIZED_NAME] = role.NormalizedName,
                [ROLE_NAME] = role.Name,
                [CONCURRENCY_STAMP] = role.ConcurrencyStamp,
                Key = KeyFromRoleId(role.Id)
            };
            entity[CONCURRENCY_STAMP].ExcludeFromIndexes = true;
            return entity;
        }

        R EntityToRole(Entity entity)
        {
            if (null == entity)
            {
                return null;
            }
            R role = new R()
            {
                NormalizedName = (string)entity[NORMALIZED_NAME],
                Name = (string)entity[ROLE_NAME],
                ConcurrencyStamp = (string)entity[CONCURRENCY_STAMP]
            };
            return role;
        }
        public async Task<IdentityResult> CreateAsync(R role, CancellationToken cancellationToken)
        {
            return await Rpc.WrapExceptionsAsync(() => 
                _datastore.InsertAsync(RoleToEntity(role), CallSettings.FromCancellationToken(cancellationToken)));
        }

        public async Task<IdentityResult> DeleteAsync(R role, CancellationToken cancellationToken)
        {
            return await Rpc.WrapExceptionsAsync(() => 
                _datastore.DeleteAsync(KeyFromRoleId(role.Id), CallSettings.FromCancellationToken(cancellationToken)));                        
        }

        public void Dispose()
        {
        }

        public async Task<R> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            return EntityToRole(await _datastore.LookupAsync(KeyFromRoleId(roleId),
                callSettings:CallSettings.FromCancellationToken(cancellationToken)));            
        }

        public async Task<R> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            var result = await _datastore.RunQueryAsync(new Query(KIND) {
                Filter = Filter.Equal(NORMALIZED_NAME, normalizedRoleName)
            });
            return EntityToRole(result.Entities.FirstOrDefault());
        }

        public Task<string> GetNormalizedRoleNameAsync(R role, CancellationToken cancellationToken)
        {
            return Task.FromResult(role.NormalizedName);
        }

        public Task<string> GetRoleIdAsync(R role, CancellationToken cancellationToken)
        {
            return Task.FromResult(role.Id);
        }

        public Task<string> GetRoleNameAsync(R role, CancellationToken cancellationToken)
        {
            return Task.FromResult(role.Name);
        }

        public Task SetNormalizedRoleNameAsync(R role, string normalizedName, CancellationToken cancellationToken)
        {
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetRoleNameAsync(R role, string roleName, CancellationToken cancellationToken)
        {
            role.Name = roleName;
            return Task.CompletedTask;
        }

        public async Task<IdentityResult> UpdateAsync(R role, CancellationToken cancellationToken)
        {
            return await Rpc.WrapExceptionsAsync(() => 
                _datastore.UpsertAsync(RoleToEntity(role), CallSettings.FromCancellationToken(cancellationToken)));
        }
    }
}

