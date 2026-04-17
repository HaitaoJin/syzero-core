using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace SyZero.ApiGateway
{
    public sealed class SyZeroApiGatewayOptions
    {
        public const string DefaultCorsPolicyName = "SyZero.ApiGateway";

        public string CorsPolicyName { get; set; } = DefaultCorsPolicyName;

        public bool EnableSwagger { get; set; } = true;

        public bool EnableSwaggerGen { get; set; } = true;

        public string SwaggerDocumentName { get; set; } = "v1";

        public string SwaggerTitle { get; set; } = "SyZero API Gateway";

        public string SwaggerGeneratorPath { get; set; } = "/swagger/docs";

        public bool EnableConsul { get; set; } = true;

        public bool UseConsulServiceAddress { get; set; } = true;

        public bool EnableCacheManager { get; set; } = true;

        public bool EnablePolly { get; set; } = true;

        public bool EnableConfigStoredInConsul { get; set; } = true;

        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

        internal static SyZeroApiGatewayOptions CreateDefault(IConfiguration configuration)
        {
            var serverName = configuration["SyZero:Name"];
            return new SyZeroApiGatewayOptions
            {
                SwaggerTitle = string.IsNullOrWhiteSpace(serverName) ? "SyZero API Gateway" : serverName,
                AllowedOrigins = ResolveAllowedOrigins(configuration)
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(CorsPolicyName))
            {
                throw new ArgumentException("CorsPolicyName cannot be empty.", nameof(CorsPolicyName));
            }

            if (string.IsNullOrWhiteSpace(SwaggerDocumentName))
            {
                throw new ArgumentException("SwaggerDocumentName cannot be empty.", nameof(SwaggerDocumentName));
            }

            if (string.IsNullOrWhiteSpace(SwaggerTitle))
            {
                throw new ArgumentException("SwaggerTitle cannot be empty.", nameof(SwaggerTitle));
            }

            if (string.IsNullOrWhiteSpace(SwaggerGeneratorPath))
            {
                throw new ArgumentException("SwaggerGeneratorPath cannot be empty.", nameof(SwaggerGeneratorPath));
            }
        }

        private static string[] ResolveAllowedOrigins(IConfiguration configuration)
        {
            var origins = configuration["SyZero:CorsOrigins"];
            if (string.IsNullOrWhiteSpace(origins))
            {
                return Array.Empty<string>();
            }

            return origins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
