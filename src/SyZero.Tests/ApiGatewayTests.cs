using Consul;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Ocelot.Logging;
using Ocelot.Provider.Consul.Interfaces;
using SyZero.ApiGateway;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class ApiGatewayTests
{
    [Fact]
    public void AddSyZeroApiGateway_RegistersGatewayServices_AndBindsConfiguredCorsOrigins()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("https://app.example.com, https://admin.example.com");

        services.AddSyZeroApiGateway(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SyZeroApiGatewayOptions>();
        var corsOptions = provider.GetRequiredService<IOptions<CorsOptions>>();
        var policy = corsOptions.Value.GetPolicy(options.CorsPolicyName);

        Assert.NotNull(provider.GetService<IHttpContextAccessor>());
        Assert.NotNull(provider.GetService<IConsulClientFactory>());
        Assert.NotNull(policy);
        Assert.Equal(new[] { "https://app.example.com", "https://admin.example.com" }, options.AllowedOrigins);
        Assert.Equal(options.AllowedOrigins, policy!.Origins);
        Assert.True(policy.SupportsCredentials);
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(SyZeroConsulServiceBuilder));
    }

    [Fact]
    public void AddSyZeroApiGateway_WithoutConfiguredOrigins_FallsBackToWildcardCors()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(null);

        services.AddSyZeroApiGateway(options =>
        {
            options.EnableSwagger = false;
        }, configuration);

        using var provider = services.BuildServiceProvider();
        var gatewayOptions = provider.GetRequiredService<SyZeroApiGatewayOptions>();
        var corsOptions = provider.GetRequiredService<IOptions<CorsOptions>>();
        var policy = corsOptions.Value.GetPolicy(gatewayOptions.CorsPolicyName);

        Assert.NotNull(policy);
        Assert.Empty(gatewayOptions.AllowedOrigins);
        Assert.Contains(policy!.Origins, origin => origin == CorsConstants.AnyOrigin);
        Assert.False(policy.SupportsCredentials);
    }

    [Fact]
    public void SyZeroConsulServiceBuilder_UsesServiceAddressAsDownstreamHost()
    {
        var builder = new TestableConsulServiceBuilder();
        var entry = new ServiceEntry
        {
            Service = new AgentService
            {
                Address = "10.10.0.25",
                Service = "gateway"
            }
        };

        var host = builder.ResolveDownstreamHost(entry, new Node());

        Assert.Equal("10.10.0.25", host);
    }

    private static IConfiguration CreateConfiguration(string? origins)
    {
        var values = new Dictionary<string, string?>
        {
            ["SyZero:Name"] = "SyZero.Gateway",
            ["GlobalConfiguration:ServiceDiscoveryProvider:Host"] = "127.0.0.1",
            ["GlobalConfiguration:ServiceDiscoveryProvider:Port"] = "8500",
            ["GlobalConfiguration:ServiceDiscoveryProvider:Type"] = "Consul",
            ["GlobalConfiguration:ServiceDiscoveryProvider:ConfigurationKey"] = "SyZero.Gateway"
        };

        if (origins != null)
        {
            values["SyZero:CorsOrigins"] = origins;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestableConsulServiceBuilder : SyZeroConsulServiceBuilder
    {
        public TestableConsulServiceBuilder()
            : base(
                new HttpContextAccessor(),
                new Mock<IConsulClientFactory>(MockBehavior.Strict).Object,
                new Mock<IOcelotLoggerFactory>(MockBehavior.Strict).Object)
        {
        }

        public string ResolveDownstreamHost(ServiceEntry entry, Node node)
        {
            return GetDownstreamHost(entry, node);
        }
    }

}
