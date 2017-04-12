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
        readonly MetadataConfigurationOptions _options;
        public MetadataConfigurationSource(MetadataConfigurationOptions options = null)
        {
            _options = options ?? new MetadataConfigurationOptions();
        }
        IConfigurationProvider IConfigurationSource.Build(IConfigurationBuilder builder)
        {
            return new MetadataConfigurationProvider(_options);
        }
    }

    public class MetadataConfigurationOptions
    {
        public bool ReplaceHyphensWithColons { get; set; } = true;
    }

    public class MetadataConfigurationProvider : ConfigurationProvider
    {
        readonly HttpClient _http;
        readonly MetadataConfigurationOptions _options;

        public MetadataConfigurationProvider(MetadataConfigurationOptions options)
        {
            _options = options;
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
                    _http.GetAsync("project/attributes/?recursive=true")
                    .Result.Content.ReadAsStringAsync().Result);
                // Instance attributes clobber project attributes.
                Dictionary<string, string> instanceAttributes =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    _http.GetAsync("instance/attributes/?recursive=true")
                    .Result.Content.ReadAsStringAsync().Result);
                foreach (var instanceAttribute in instanceAttributes)
                {
                    attributes[instanceAttribute.Key] = instanceAttribute.Value;
                }
                // Replace hyphens with colons.
                if (_options.ReplaceHyphensWithColons)
                {
                    var newData = new Dictionary<string, string>();
                    foreach (var attribute in attributes)
                    {
                        newData[attribute.Key.Replace('-', ':')] = attribute.Value;
                    }
                    Data = newData;
                }
                else
                {
                    Data = attributes;
                }
                Data["IAmRunningInGoogleCloud"] = "true";
            }
            catch (AggregateException ae)
            {
                ae.Handle((e) =>
                {
                    if (e is HttpRequestException)
                    {
                        Debug.WriteLine("Failed to load attributes from Google metadata. "
                        + "I assume I'm not running in Google Cloud.");
                        return true;
                    }
                    return false;
                });                 
            }
        }
    }
}
