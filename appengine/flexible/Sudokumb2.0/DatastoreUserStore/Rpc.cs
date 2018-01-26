using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Sudokumb
{
    internal class Rpc {

        public static async Task<IdentityResult> WrapExceptionsAsync(Func<Task> f)
        {
            try
            {
                await f();
                return IdentityResult.Success;
            }
            catch (Grpc.Core.RpcException e)
            {
                return IdentityResult.Failed(new IdentityError() {
                    Code = e.Status.Detail,
                    Description = e.Message
                });
            }
        }
    }
}