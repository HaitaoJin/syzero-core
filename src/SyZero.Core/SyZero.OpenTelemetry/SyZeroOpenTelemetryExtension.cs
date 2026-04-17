using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Linq;

namespace SyZero
{
    public static class SyZeroOpenTelemetryExtension
    {
        private static readonly string[] DefaultMetricMeters =
        {
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "Microsoft.AspNetCore.Routing",
            "System.Net.Http"
        };

        public static IServiceCollection AddSyZeroOpenTelemetry(this IServiceCollection services)
        {
            return services.AddSyZeroOpenTelemetry(configuration: null);
        }

        public static IServiceCollection AddSyZeroOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
        {
            return services.AddSyZeroOpenTelemetry(configureOptions: null, configuration);
        }

        public static IServiceCollection AddSyZeroOpenTelemetry(this IServiceCollection services, Action<SyZeroOpenTelemetryOptions> configureOptions)
        {
            return services.AddSyZeroOpenTelemetry(configureOptions, configuration: null);
        }

        public static IServiceCollection AddSyZeroOpenTelemetry(
            this IServiceCollection services,
            Action<SyZeroOpenTelemetryOptions> configureOptions,
            IConfiguration configuration = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var config = configuration ?? AppConfig.Configuration;
            var options = SyZeroOpenTelemetryOptions.CreateDefault(config);
            configureOptions?.Invoke(options);
            options.Validate();

            services.AddSingleton(options);

            var endpoint = options.GetOtlpEndpoint();
            var builder = services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(options.ServiceName, serviceVersion: options.ServiceVersion));

            if (options.EnableTracing)
            {
                builder.WithTracing(traceBuilder =>
                {
                    if (options.ActivitySources.Length > 0)
                    {
                        traceBuilder.AddSource(options.ActivitySources);
                    }

                    traceBuilder.AddAspNetCoreInstrumentation(opt =>
                    {
                        opt.Filter = context => MatchesPathPrefixes(context?.Request?.Path.Value, options.AspNetCorePathPrefixes);
                    });

                    traceBuilder.AddHttpClientInstrumentation(opt =>
                    {
                        opt.FilterHttpWebRequest = request => MatchesPathPrefixes(request?.RequestUri?.AbsolutePath, options.HttpClientPathPrefixes);
                        opt.FilterHttpRequestMessage = request => MatchesPathPrefixes(request?.RequestUri?.AbsolutePath, options.HttpClientPathPrefixes);
                    });

                    if (endpoint != null)
                    {
                        traceBuilder.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
                    }
                });
            }

            if (options.EnableMetrics)
            {
                builder.WithMetrics(metricBuilder =>
                {
                    var meterNames = DefaultMetricMeters
                        .Concat(options.Meters)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    metricBuilder.AddMeter(meterNames);

                    if (endpoint != null)
                    {
                        metricBuilder.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
                    }
                });
            }

            if (options.EnableLogging)
            {
                builder.WithLogging(logBuilder =>
                {
                    if (endpoint != null)
                    {
                        logBuilder.AddOtlpExporter(exporter => ConfigureExporter(exporter, options));
                    }
                });
            }

            return services;
        }

        private static void ConfigureExporter(OtlpExporterOptions exporter, SyZeroOpenTelemetryOptions options)
        {
            exporter.Endpoint = options.GetOtlpEndpoint();
            exporter.Protocol = options.OtlpProtocol;
        }

        private static bool MatchesPathPrefixes(string path, string[] prefixes)
        {
            if (prefixes == null || prefixes.Length == 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalizedPath = NormalizePath(path);
            foreach (var prefix in prefixes)
            {
                if (normalizedPath.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
    }
}
