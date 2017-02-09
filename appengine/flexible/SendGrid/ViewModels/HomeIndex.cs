using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;

namespace SendGrid.ViewModels
{
    public class HomeIndex
    {
        public bool MissingApiKey { get; set; } = false;
        public string Recipient { get; set; } = "";
        public HttpResponseMessage sendGridResponse { get; set; }
    }
}
