using System.Collections.Generic;

namespace Sudokumb
{
    public interface IUserWithRoles
    {
        IList<string> Roles { get; set; }
    }
}