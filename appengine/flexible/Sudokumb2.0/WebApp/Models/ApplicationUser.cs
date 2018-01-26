using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Sudokumb;

namespace WebApp.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser<long>, IUserWithRoles
    {
        IList<string> _roles = new List<string>();
        public IList<string> Roles { get => _roles; set => _roles = value; }
    }
}
