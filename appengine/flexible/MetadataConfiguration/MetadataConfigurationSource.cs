using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;

namespace GoogleCloudSamples
{
    public class MetadataConfigurationSource : IConfigurationSource
    {
        IConfigurationProvider IConfigurationSource.Build(IConfigurationBuilder builder)
        {
            return new MetadataConfigurationProvider();
        }
    }

    public class MetadataConfigurationProvider : ConfigurationProvider
    {
        readonly HttpClient _http;

        public MetadataConfigurationProvider()
        {
            _http = new HttpClient()
            {
                BaseAddress = new Uri("http://metadata.google.internal/computeMetadata/v1/")
            };
            _http.DefaultRequestHeaders.Add("Metadata-Flavor", "Google");
        }

        public override void Load()
        {
            try
            {
                Dictionary<string, string> attributes =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    _http.GetAsync("project/attributes/").Result.Content.ReadAsStringAsync().Result);
                // Instance attributes clobber project attributes.
                Dictionary<string, string> instanceAttributes =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    _http.GetAsync("instance/attributes/").Result.Content.ReadAsStringAsync().Result);
                foreach (var instanceAttribute in instanceAttributes)
                {
                    attributes[instanceAttribute.Key] = instanceAttribute.Value;
                }
                Data = attributes;
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Failed to load attributes from Google metadata. "
                    + "I assume I'm not running in Google Cloud.");
            }
        }
    }
}
