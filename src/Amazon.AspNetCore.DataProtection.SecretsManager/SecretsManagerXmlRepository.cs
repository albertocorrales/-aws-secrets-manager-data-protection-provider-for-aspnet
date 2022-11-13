using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Amazon.AspNetCore.DataProtection.SecretsManager
{
    public class SecretsManagerXmlRepository : IXmlRepository, IDisposable
    {
        public const string TagDataProtectionKeyPrefix = "DataProtectionKeyPrefix";

        private readonly IAmazonSecretsManager _secretsManagerClient;
        private readonly string _secretNamePrefix;
        private readonly PersistOptions _options;
        private readonly ILogger<SecretsManagerXmlRepository> _logger;

        public SecretsManagerXmlRepository(IAmazonSecretsManager secretsManagerClient, string secretNamePrefix, PersistOptions options = null, ILoggerFactory loggerFactory = null)
        {
            _secretsManagerClient = secretsManagerClient ?? throw new ArgumentNullException(nameof(secretsManagerClient));
            _secretNamePrefix = secretNamePrefix ?? throw new ArgumentNullException(nameof(secretNamePrefix));
            _options = options ?? new PersistOptions();

            if (loggerFactory != null)
            {
                _logger = loggerFactory?.CreateLogger<SecretsManagerXmlRepository>();
            }
            else
            {
                _logger = NullLoggerFactory.Instance.CreateLogger<SecretsManagerXmlRepository>();
            }

            _secretNamePrefix = _secretNamePrefix.Trim('/') + '/';

            _logger.LogInformation($"Using Secrets Manager to persist DataProtection keys with secret name name prefix {_secretNamePrefix}");
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            return Task.Run(GetAllElementsAsync).GetAwaiter().GetResult();
        }

        private async Task<IReadOnlyCollection<XElement>> GetAllElementsAsync()
        {
            var request = new ListSecretsRequest
            {
                Filters = new List<Filter>
                {
                    new Filter()
                    {
                        Key = FilterNameStringType.TagValue,
                        Values = new List<string> { _secretNamePrefix }
                    }

                },
            };
            ListSecretsResponse response = null;

            var results = new List<XElement>();
            do
            {
                request.NextToken = response?.NextToken;
                try
                {
                    response = await _secretsManagerClient.ListSecretsAsync(request).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error calling Secrets Manager to get secrets starting with {_secretNamePrefix}: {e.Message}");
                    throw;
                }

                foreach (var secret in response.SecretList)
                {
                    try
                    {
                        var GetSecretValueRequest = new GetSecretValueRequest
                        {
                            SecretId = secret.Name
                        };
                        var secretValueResponse = await _secretsManagerClient.GetSecretValueAsync(GetSecretValueRequest).ConfigureAwait(false);

                        var xml = XElement.Parse(secretValueResponse.SecretString);
                        results.Add(xml);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"Error parsing key {secret.Name}, key will be skipped: {e.Message}");
                    }
                }

            } while (!string.IsNullOrEmpty(response.NextToken));

            _logger.LogInformation($"Loaded {results.Count} DataProtection keys");
            return results;
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            Task.Run(() => StoreElementAsync(element, friendlyName)).Wait();
        }

        private async Task StoreElementAsync(XElement element, string friendlyName)
        {
            var secretName = _secretNamePrefix +
                            (friendlyName ??
                            element.Attribute("id")?.Value ??
                            Guid.NewGuid().ToString());

            var elementValue = element.ToString();

            try
            {
                var request = new CreateSecretRequest
                {
                    Name = secretName,
                    SecretString = elementValue,
                    Tags = new List<Tag> { 
                        new Tag { Key = TagDataProtectionKeyPrefix, Value = _secretNamePrefix } 
                    }
                };

                if (!string.IsNullOrEmpty(_options.KMSKeyId))
                {
                    request.KmsKeyId = _options.KMSKeyId;
                }

                if (!string.IsNullOrEmpty(_options.ReplicationRegion))
                {
                    request.AddReplicaRegions = new List<ReplicaRegionType> { 
                        new ReplicaRegionType() 
                        { 
                            Region = _options.ReplicationRegion,
                            KmsKeyId = _options.ReplicaRegionKMSKeyId
                        } 
                    };
                }

                await _secretsManagerClient.CreateSecretAsync(request).ConfigureAwait(false);

                _logger.LogInformation($"Saved DataProtection key to SecretsManager with secret name {secretName}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Error saving DataProtection key to SecretsManager with secret name {secretName}: {e.Message}");
                throw;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _secretsManagerClient?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}