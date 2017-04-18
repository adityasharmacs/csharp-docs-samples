using Google.Apis.CloudKMS.v1;
using Google.Apis.CloudKMS.v1.Data;
using System;
using System.Linq;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;

namespace GoogleCloudSamples
{
    public class KmsDataProtectionProviderOptions
    {
        public string ProjectId { get; set; }
        public string Location { get; set; } = "global";
        public string KeyRing { get; set; }
    }

    public class KmsDataProtectionProvider : IDataProtectionProvider
    {
        readonly CloudKMSService _kms;
        readonly IOptions<KmsDataProtectionProviderOptions> _options;
        public KmsDataProtectionProvider(IOptions<KmsDataProtectionProviderOptions> options)
        {
            _options = options;
            // Create a KMS service client with credentials.
            GoogleCredential credential =
                GoogleCredential.GetApplicationDefaultAsync().Result;
            // Inject the Cloud Key Management Service scope
            if (credential.IsCreateScopedRequired)
            {
                credential = credential.CreateScoped(new[]
                {
                    CloudKMSService.Scope.CloudPlatform
                });
            }
            _kms = new CloudKMSService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                GZipEnabled = false
            });
            // Create the key ring.
            var parent = string.Format("projects/{0}/locations/{1}",
                options.Value.ProjectId, options.Value.Location);
            KeyRing keyRingToCreate = new KeyRing();
            var request = new ProjectsResource.LocationsResource.KeyRingsResource.CreateRequest(
                _kms, keyRingToCreate, parent);
            request.KeyRingId = options.Value.KeyRing;
            try
            {
                request.Execute();
            }
            catch (Google.GoogleApiException e)
            when (e.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Already exists.  Ok.
            }
        }

        IDataProtector IDataProtectionProvider.CreateProtector(string purpose)
        {
            // Create the crypto key:
            var parent = string.Format(
                "projects/{0}/locations/{1}/keyRings/{2}",
                _options.Value.ProjectId, _options.Value.Location,
                _options.Value.KeyRing);
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
                _kms, cryptoKeyToCreate, parent);
            string keyId = EscapeKeyId(purpose);
            request.CryptoKeyId = keyId;
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
                    parent, keyId);
            }
            return new KmsDataProtector(_kms, keyName, (string innerPurpose) =>
                this.CreateProtector($"{purpose}.{innerPurpose}"));
        }

        static internal string EscapeKeyId(string purpose)
        {
            StringBuilder keyIdBuilder = new StringBuilder();
            char prevC = ' ';
            foreach (char c in purpose)
            {
                if (c == '.')
                {
                    keyIdBuilder.Append('-');
                }
                else if (prevC == '0' && c == 'x' ||
                    !"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_"
                    .Contains(c))
                {
                    keyIdBuilder.AppendFormat("0x{0:X4}", (int)c);
                }
                else
                {
                    keyIdBuilder.Append(c);
                }
                prevC = c;
            }
            string keyId = keyIdBuilder.ToString();
            if (keyId.Length > 63)
            {
                // For strings that are too long to be key ids, tag them with a
                // has code.
                int hash = 17;
                foreach (char c in keyId)
                {
                    hash = hash * 31 + c;
                }
                keyId = string.Format("{0}-{1:x8}", keyId.Substring(0, 54), hash);
            }
            return keyId;
        }
    }

    public class KmsDataProtector : IDataProtector
    {
        readonly CloudKMSService _kms;
        readonly string _keyName;
        readonly Func<string, IDataProtector> _dataProtectorFactory;

        internal KmsDataProtector(CloudKMSService kms, string keyName,
            Func<string, IDataProtector> dataProtectorFactory)
        {
            _kms = kms;
            _keyName = keyName;
            _dataProtectorFactory = dataProtectorFactory;
        }

        IDataProtector IDataProtectionProvider.CreateProtector(string purpose)
        {
            return _dataProtectorFactory(purpose);
        }

        byte[] IDataProtector.Protect(byte[] plaintext)
        {
            var result = _kms.Projects.Locations.KeyRings.CryptoKeys
                .Encrypt(new EncryptRequest()
            {
                Plaintext = Convert.ToBase64String(plaintext)
            }, _keyName).Execute();
            return Convert.FromBase64String(result.Ciphertext);
        }

        byte[] IDataProtector.Unprotect(byte[] protectedData)
        {
            var result = _kms.Projects.Locations.KeyRings.CryptoKeys
                .Decrypt(new DecryptRequest()
                {
                    Ciphertext = Convert.ToBase64String(protectedData)
                }, _keyName).Execute();
            return Convert.FromBase64String(result.Plaintext);
        }
    }
}
