using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using System;
using System.Linq;
using System.Reflection;

namespace SyZero
{
    public class SyZeroOpenTelemetryOptions
    {
        public const string SectionName = "OpenTelemetry";

        public string ServiceName { get; set; }

        public string ServiceVersion { get; set; }

        public string OtlpUrl { get; set; }

        public OtlpExportProtocol OtlpProtocol { get; set; } = OtlpExportProtocol.Grpc;

        public bool EnableTracing { get; set; } = true;

        public bool EnableMetrics { get; set; } = true;

        public bool EnableLogging { get; set; } = true;

        public string[] ActivitySources { get; set; } = Array.Empty<string>();

        public string[] Meters { get; set; } = Array.Empty<string>();

        public string[] AspNetCorePathPrefixes { get; set; } = new[] { "/api" };

        public string[] HttpClientPathPrefixes { get; set; } = Array.Empty<string>();

        public static SyZeroOpenTelemetryOptions CreateDefault(IConfiguration configuration = null)
        {
            var options = new SyZeroOpenTelemetryOptions();
            configuration?.GetSection(SectionName)?.Bind(options);

            if (string.IsNullOrWhiteSpace(options.ServiceName))
            {
                options.ServiceName = ResolveServiceName();
            }

            if (string.IsNullOrWhiteSpace(options.ServiceVersion))
            {
                options.ServiceVersion = ResolveServiceVersion();
            }
            options.ActivitySources = NormalizeNames(options.ActivitySources);
            options.Meters = NormalizeNames(options.Meters);
            options.AspNetCorePathPrefixes = NormalizePathPrefixes(options.AspNetCorePathPrefixes);
            options.HttpClientPathPrefixes = NormalizePathPrefixes(options.HttpClientPathPrefixes);

            return options;
        }

        public void Validate()
        {
            if (!this.EnableTracing && !this.EnableMetrics && !this.EnableLogging)
            {
                throw new InvalidOperationException("At least one OpenTelemetry signal must be enabled.");
            }

            if (string.IsNullOrWhiteSpace(this.ServiceName))
            {
                throw new InvalidOperationException("OpenTelemetry service name could not be resolved.");
            }

            if (!string.IsNullOrWhiteSpace(this.OtlpUrl) && !Uri.TryCreate(this.OtlpUrl, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException("OpenTelemetry:OtlpUrl must be an absolute URI.");
            }
        }

        internal Uri GetOtlpEndpoint()
        {
            return string.IsNullOrWhiteSpace(this.OtlpUrl) ? null : new Uri(this.OtlpUrl, UriKind.Absolute);
        }

        private static string[] NormalizeNames(string[] values)
        {
            return values?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
        }

        private static string[] NormalizePathPrefixes(string[] values)
        {
            return values?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
        }

        private static string NormalizePath(string path)
        {
            var normalized = path.Trim();
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }

            return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
        }

        private static string ResolveServiceName()
        {
            if (!string.IsNullOrWhiteSpace(AppConfig.ServerOptions?.Name))
            {
                return AppConfig.ServerOptions.Name;
            }

            return Assembly.GetEntryAssembly()?.GetName().Name ?? "syzero-service";
        }

        private static string ResolveServiceVersion()
        {
            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
        }
    }
}
