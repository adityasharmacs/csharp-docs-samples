using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RedisCache.ViewModels
{
    public class WhoForm
    {
        public string Who { get; set; }
    }

    public class WhoCount : WhoForm
    {
        public int Count { get; set; }
    }
}
