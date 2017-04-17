using Microsoft.AspNetCore.DataProtection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialAuth.Services
{
    public class KmsDataProtectionProvider : IDataProtectionProvider
    {
        IDataProtector IDataProtectionProvider.CreateProtector(string purpose)
        {
            throw new NotImplementedException();
        }
    }
}
