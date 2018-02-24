using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Datastore.V1;
using Microsoft.AspNetCore.Identity;

namespace Sudokumb
{
    internal class Rpc
    {
        public static async Task<IdentityResult> TranslateExceptionsAsync(Func<Task> f)
        {
            try
            {
                await f();
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