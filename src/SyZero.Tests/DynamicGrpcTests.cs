using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using SyZero.Application.Attributes;
using SyZero.Application.Service;
using SyZero.Client;
using SyZero.DynamicGrpc;
using SyZero.DynamicGrpc.Attributes;
using Xunit;

namespace SyZero.Tests;

public class DynamicGrpcTests
{
    [Fact]
    public void DynamicGrpcServiceTypeProvider_GetServiceName_PrefersLongestMatchingPostfix()
    {
        var provider = new DynamicGrpcServiceTypeProvider(new DynamicGrpcOptions());

        Assert.Equal("Sample", provider.GetServiceName("SampleGrpcService"));
        Assert.Equal("GetValue", provider.GetMethodName("GetValueAsync"));
    }

    [Fact]
    public void DynamicGrpcServiceTypeProvider_IsGrpcService_ExcludesNonGrpcService()
    {
        DynamicGrpcServiceTypeProvider.ClearCache();
        var provider = new DynamicGrpcServiceTypeProvider(new DynamicGrpcOptions());

        Assert.True(provider.IsGrpcService(typeof(IncludedGrpcAppService).GetTypeInfo()));
        Assert.False(provider.IsGrpcService(typeof(InterfaceExcludedGrpcAppService).GetTypeInfo()));
        Assert.False(provider.IsGrpcService(typeof(FallbackGrpcAppService).GetTypeInfo()));
        Assert.False(provider.IsGrpcService(typeof(NonDynamicExcludedGrpcAppService).GetTypeInfo()));
    }

    [Fact]
    public void AddDynamicGrpc_RegistersOnlyEligibleServices()
    {
        DynamicGrpcServiceTypeProvider.ClearCache();
        var services = new ServiceCollection();

        services.AddDynamicGrpc(new DynamicGrpcOptions());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IIncludedGrpcAppService>());
        Assert.NotNull(scope.ServiceProvider.GetService<IncludedGrpcAppService>());
        Assert.Null(scope.ServiceProvider.GetService<IInterfaceExcludedGrpcAppService>());
        Assert.Null(scope.ServiceProvider.GetService<InterfaceExcludedGrpcAppService>());
        Assert.Null(scope.ServiceProvider.GetService<IFallbackGrpcAppService>());
        Assert.Null(scope.ServiceProvider.GetService<FallbackGrpcAppService>());
        Assert.Null(scope.ServiceProvider.GetService<INonDynamicExcludedGrpcAppService>());
        Assert.Null(scope.ServiceProvider.GetService<NonDynamicExcludedGrpcAppService>());
    }

    [Fact]
    public void AddDynamicGrpc_WithAssemblyOverload_RestrictsDiscoveryToSpecifiedAssemblies()
    {
        DynamicGrpcServiceTypeProvider.ClearCache();
        var services = new ServiceCollection();

        services.AddDynamicGrpc(typeof(DynamicGrpcServiceTypeProvider).Assembly);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Null(scope.ServiceProvider.GetService<IIncludedGrpcAppService>());
        Assert.Null(scope.ServiceProvider.GetService<IncludedGrpcAppService>());
    }

    [Fact]
    public void AddDynamicGrpc_BindsConfigurationAndAppliesOverrides()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DynamicGrpc:MaxReceiveMessageSize"] = "1024",
                ["DynamicGrpc:EnableDetailedErrors"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDynamicGrpc(options => options.MaxSendMessageSize = 2048, configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DynamicGrpcOptions>();

        Assert.Equal(1024, options.MaxReceiveMessageSize);
        Assert.Equal(2048, options.MaxSendMessageSize);
        Assert.True(options.EnableDetailedErrors);
    }
}

[DynamicApi]
public interface IIncludedGrpcAppService : IDynamicApi
{
    Task<string> PingAsync(string value);
}

public class IncludedGrpcAppService : IIncludedGrpcAppService
{
    public Task<string> PingAsync(string value)
    {
        return Task.FromResult(value);
    }
}

[DynamicApi]
[NonGrpcService]
public interface IInterfaceExcludedGrpcAppService : IDynamicApi
{
    Task<string> PingAsync(string value);
}

public class InterfaceExcludedGrpcAppService : IInterfaceExcludedGrpcAppService
{
    public Task<string> PingAsync(string value)
    {
        return Task.FromResult(value);
    }
}

[DynamicApi]
public interface IFallbackGrpcAppService : IDynamicApi
{
    Task<string> PingAsync(string value);
}

public class FallbackGrpcAppService : IFallbackGrpcAppService, IFallback
{
    public Task<string> PingAsync(string value)
    {
        return Task.FromResult(value);
    }
}

[DynamicApi]
[NonDynamicApi]
public interface INonDynamicExcludedGrpcAppService : IDynamicApi
{
    Task<string> PingAsync(string value);
}

public class NonDynamicExcludedGrpcAppService : INonDynamicExcludedGrpcAppService
{
    public Task<string> PingAsync(string value)
    {
        return Task.FromResult(value);
    }
}
