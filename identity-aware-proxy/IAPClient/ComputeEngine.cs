/*
Copyright 2018 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

// [START generate_iap_request]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Json;
using Google.Apis.Logging;
using Google.Cloud.Metadata.V1;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace GoogleCloudSamples
{
    class ComputeEngineClient
    {
        /// <summary>
        /// Authenticates using the client id and credentials, then fetches
        /// the uri.
        /// </summary>
        /// <param name="iapClientId">The client id observed on 
        /// </param>
        /// <param name="uri">HTTP uri to fetch.</param>
        /// <returns>The http response body as a string.</returns>
        public static string InvokeRequest(string iapClientId,
            string uri)
        {
            MetadataClient metadataClient = 
                Google.Cloud.Metadata.V1.MetadataClient.Create();
            var result = metadataClient.GetAccessTokenAsync(
                iapClientId, CancellationToken.None).Result;
            string token = result.IdToken;

            // Include the OIDC token in an Authorization: Bearer header to 
            // IAP-secured resource
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            string response = httpClient.GetStringAsync(uri).Result;
            return response;
        }

    }

    internal static class MetadataClientExtensions 
    {
        private const string MetadataFlavor = "Metadata-Flavor";
        private const string GoogleMetadataHeader = "Google";
        private const string DefaultMetadataHost = "169.254.169.254";
        private const string EmulatorEnvironmentVariable = "METADATA_EMULATOR_HOST";
        private static readonly ILogger Logger = Google.ApplicationContext
            .Logger.ForType<MetadataClientImpl>();
        private static readonly Lazy<string> MetadataHost = 
        new Lazy<string>(() =>
        {
            var emulatorHost = Environment.GetEnvironmentVariable(EmulatorEnvironmentVariable);
            return string.IsNullOrEmpty(emulatorHost) ? DefaultMetadataHost : emulatorHost;
        });

        public static async Task<HttpResponseMessage> RequestMetadataAsync(
            this MetadataClient client, Uri uri, 
            CancellationToken cancellationToken,
            bool requireMetadataFlavorHeader = true)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            httpRequest.Headers.Add(MetadataFlavor, GoogleMetadataHeader);
            var response = await client.HttpClient.SendAsync(httpRequest, 
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (requireMetadataFlavorHeader)
            {
                IEnumerable<string> metadataFlavorHeaders;
                if (!response.Headers.TryGetValues(
                        MetadataFlavor, out metadataFlavorHeaders) ||
                    !metadataFlavorHeaders.Contains(GoogleMetadataHeader))
                {
                    throw new HttpRequestException("The response from the metadata server was not valid.");
                }
            }
            return response;
        }
        private static Uri BaseUri =
            new Uri($"http://{MetadataHost.Value}/computeMetadata/v1/");

        public static async Task<TokenResponse> GetAccessTokenAsync(
            this MetadataClient client, string audience,
            CancellationToken cancellationToken)
        {
            var uri = new UriBuilder(BaseUri);
            uri.Path += "instance/service-accounts/default/token";
            uri.Query = $"audience={audience}";
            var response = await client.RequestMetadataAsync(uri.Uri,
                cancellationToken, requireMetadataFlavorHeader: false)
                .ConfigureAwait(false);
            return await TokenResponse.FromHttpResponseAsync(
                response, Google.Apis.Util.SystemClock.Default, Logger)
                .ConfigureAwait(false);
        }            
    }
}
// [END generate_iap_request]