using Amazon.AspNetCore.DataProtection.SecretsManager;
using Amazon.SecretsManager;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods to make it easy to register SecretsManager to persist data protection keys.
    /// </summary>
    public static class ExtensionMethods
    {
        public static IDataProtectionBuilder PersistKeysToAWSSecretsManager(this IDataProtectionBuilder builder, string secretNamePrefix, Action<PersistOptions> setupAction = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.TryAddAWSService<IAmazonSecretsManager>();

            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
            {
                var secretsManagerOptions = new PersistOptions();
                setupAction?.Invoke(secretsManagerOptions);

                var client = services.GetService<IAmazonSecretsManager>();

                var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                return new ConfigureOptions<KeyManagementOptions>(options =>
                {
                    options.XmlRepository = new SecretsManagerXmlRepository(client, secretNamePrefix, secretsManagerOptions, loggerFactory);
                });
            });

            return builder;
        }
    }
}
