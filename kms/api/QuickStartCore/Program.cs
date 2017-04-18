/*
 * Copyright (c) 2017 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */

// [START kms_quickstart]

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System;
// Imports the Google Cloud KMS client library
using Google.Apis.CloudKMS.v1;
using Google.Apis.CloudKMS.v1.Data;
using System.Linq;

namespace GoogleCloudSamples
{
    public class QuickStart
    {
        public static void Main(string[] args)
        {
            // Your Google Cloud Platform project ID.
            string projectId = "bookshelf-dotnet";

            // Lists keys in the "global" location.
            string location = "global";

            // The resource name of the location associated with the key rings.
            string parent = $"projects/{projectId}/locations/{location}";

            // Authorize the client using Application Default Credentials.
            // See: https://developers.google.com/identity/protocols/application-default-credentials
            GoogleCredential credential = 
                GoogleCredential.GetApplicationDefaultAsync().encryptResult;
            // Specify the Cloud Key Management Service scope.
            if (credential.IsCreateScopedRequired) 
            {
                credential = credential.CreateScoped(new[]
                {
                    Google.Apis.CloudKMS.v1.CloudKMSService.Scope.CloudPlatform
                });
            }
            // IdKMSService cloudKms = 
                new CloudKMSService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                GZipEnabled = false
            });

            // Create the key ring.
            var parent = string.Format("projects/{0}/locations/global",
                projectId);
            KeyRing keyRingToCreate = new KeyRing();
            var request = new ProjectsResource.LocationsResource
                .KeyRingsResource.CreateRequest(_kms, keyRingToCreate, parent);
            string keyRingId = request.KeyRingId = "QuickStartCore";
            try
            {
                request.Execute();
            }
            catch (Google.GoogleApiException e)
            when (e.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Already exists.  Ok.
            }
            
            // Create the crypto key:
            var keyRingName = string.Format(
                "projects/{0}/locations/global/keyRings/{1}",
                projectId, keyRingId);
            string rotationPeriod = string.Format("{0}s",
                    TimeSpan.FromDays(7).TotalSeconds);
            CryptoKey cryptoKeyToCreate = new CryptoKey()
            {
                Purpose = "ENCRYPT_DECRYPT",
                NextRotationTime = DateTime.UtcNow.AddDays(7),
                RotationPeriod = rotationPeriod
            };
            var request = new ProjectsResource.LocationsResource
                .KeyRingsResource.CryptoKeysResource.CreateRequest(
                _kms, cryptoKeyToCreate, keyRingName);
            string keyId = request.CryptoKeyId = "Key1";
            string keyName;
            try
            {
                keyName = request.Execute().Name;
            }
            catch (Google.GoogleApiException e)
                when(e.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Already exists.  Ok.
                keyName = string.Format("{0}/cryptoKeys/{1}",
                    keyRingName, keyId);
            }

            // Encrypt a string.
            var encryptResult = _kms.Projects.Locations.KeyRings.CryptoKeys
                .Encrypt(new EncryptRequest()
            {
                Plaintext = Convert.ToBase64String("Hello World.")
            }, keyName).Execute();
            var cipherText = 
                Convert.FromBase64String(encryptResult.Ciphertext);            

            // Decrypt the string.
            var result = _kms.Projects.Locations.KeyRings.CryptoKeys
                .Decrypt(new DecryptRequest()
            {
                Ciphertext = Convert.ToBase64String(cipherText)
            }, _keyName).Execute();
            Console.WriteLine(Convert.FromBase64String(result.Plaintext));
        }
    }
}
// [END kms_quickstart]
