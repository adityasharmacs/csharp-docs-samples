using System.Collections.Generic;

namespace Sudokumb
{
    public interface IDatastoreUser
    {
        IList<string> Roles { get; set; }
        bool WasNormalizedNameModified { get; set; }
    }
}