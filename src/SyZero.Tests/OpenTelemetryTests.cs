using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Reflection;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class OpenTelemetryTests
{
    [Fact]
    public void AddSyZeroOpenTelemetry_UsesSafeDefaults_WhenConfigIsMissing()
    {
        var previousConfiguration = AppConfig.Configuration;

        try
        {
            AppConfig.Configuration = new ConfigurationBuilder().Build();
            ResetAppConfigCache("serverOptions");

            var services = new ServiceCollection();
            services.AddSyZeroOpenTelemetry();

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<SyZeroOpenTelemetryOptions>();

            Assert.Equal(Assembly.GetEntryAssembly()?.GetName().Name, options.ServiceName);
            Assert.Null(options.OtlpUrl);
            Assert.Equal(new[] { "/api" }, options.AspNetCorePathPrefixes);
            Assert.Empty(options.HttpClientPathPrefixes);
            Assert.NotNull(provider.GetService<TracerProvider>());
            Assert.NotNull(provider.GetService<MeterProvider>());
        }
        finally
        {
            AppConfig.Configuration = previousConfiguration;
            ResetAppConfigCache("serverOptions");
        }
    }

    [Fact]
    public void AddSyZeroOpenTelemetry_BindsConfiguration_AndAppliesOverrides()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SyZero:Name"] = "config-service",
                ["OpenTelemetry:OtlpUrl"] = "http://localhost:4318",
                ["OpenTelemetry:OtlpProtocol"] = "HttpProtobuf",
                ["OpenTelemetry:EnableMetrics"] = "false",
                ["OpenTelemetry:ActivitySources:0"] = "Orders.Trace",
                ["OpenTelemetry:Meters:0"] = "Orders.Metrics",
                ["OpenTelemetry:AspNetCorePathPrefixes:0"] = "api",
                ["OpenTelemetry:HttpClientPathPrefixes:0"] = "external"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSyZeroOpenTelemetry(options =>
        {
            options.ServiceName = "override-service";
            options.EnableMetrics = true;
        }, configuration);

        using var provider = services.BuildServiceProvider();
        var registeredOptions = provider.GetRequiredService<SyZeroOpenTelemetryOptions>();

        Assert.Equal("override-service", registeredOptions.ServiceName);
        Assert.Equal("http://localhost:4318", registeredOptions.OtlpUrl);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, registeredOptions.OtlpProtocol);
        Assert.True(registeredOptions.EnableMetrics);
        Assert.Equal(new[] { "Orders.Trace" }, registeredOptions.ActivitySources);
        Assert.Equal(new[] { "Orders.Metrics" }, registeredOptions.Meters);
        Assert.Equal(new[] { "/api" }, registeredOptions.AspNetCorePathPrefixes);
        Assert.Equal(new[] { "/external" }, registeredOptions.HttpClientPathPrefixes);
    }

    [Fact]
    public void AddSyZeroOpenTelemetry_Throws_WhenOtlpUrlIsInvalid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:OtlpUrl"] = "not-a-uri"
            })
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddSyZeroOpenTelemetry(configuration));
    }

    private static void ResetAppConfigCache(string fieldName)
    {
        var field = typeof(AppConfig).GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AppConfig).FullName, fieldName);
        field.SetValue(null, null);
    }
}
