using Nacos.Microsoft.Extensions.Configuration;
using System;

namespace Microsoft.Extensions.Configuration
{
    public static class NacosConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddNacos(this IConfigurationBuilder builder, IConfiguration configuration = null, string sectionName = "NacosConfig")
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var config = configuration ?? SyZero.AppConfig.Configuration;
            if (config == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return builder.AddNacosV2Configuration(config.GetSection(sectionName));
        }

        public static IConfigurationBuilder AddNacos(this IConfigurationBuilder builder, Action<NacosV2ConfigurationSource> action)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return builder.AddNacosV2Configuration(action);
        }

        public static IConfigurationBuilder AddNacosConfiguration(this IConfigurationBuilder builder, IConfiguration configuration = null, string sectionName = "NacosConfig")
        {
            return builder.AddNacos(configuration, sectionName);
        }

        public static IConfigurationBuilder AddNacosConfiguration(this IConfigurationBuilder builder, Action<NacosV2ConfigurationSource> action)
        {
            return builder.AddNacos(action);
        }
    }
}
