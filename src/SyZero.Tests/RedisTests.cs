using FreeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SyZero.EventBus;
using SyZero.Redis;
using System.Runtime.CompilerServices;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class RedisTests
{
    [Fact]
    public void AddSyZeroRedis_WithoutMasterConfiguration_ThrowsHelpfulException()
    {
        AppConfig.Configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() => services.AddSyZeroRedis());

        Assert.Contains("Redis Master", exception.Message);
    }

    [Fact]
    public void AddSyZeroRedis_WithConfiguration_RegistersRedisServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Type"] = nameof(RedisType.MasterSlave),
                ["Redis:Master"] = "127.0.0.1:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSyZeroRedis(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RedisOptions));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RedisClient));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(SyZero.Cache.ICache));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(SyZero.Util.ILockUtil));
    }

    [Fact]
    public void AddRedisEventBus_ResolvesSameInstanceForInterfaceAndConcrete()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateUninitializedRedisClient());
        services.AddRedisEventBus(new RedisEventBusOptions());

        using var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IEventBus>();
        var concreteBus = provider.GetRequiredService<RedisEventBus>();

        Assert.Same(eventBus, concreteBus);
    }

    [Fact]
    public void AddRedisServiceManagement_ResolvesSameInstanceForInterfaceAndConcrete()
    {
        AppConfig.Configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddSingleton(CreateUninitializedRedisClient());
        services.AddRedisServiceManagement(options =>
        {
            options.EnableHealthCheck = false;
            options.AutoCleanExpiredServices = false;
            options.EnableLeaderElection = false;
            options.EnablePubSub = false;
        });

        using var provider = services.BuildServiceProvider();
        var serviceManagement = provider.GetRequiredService<SyZero.Service.IServiceManagement>();
        var concreteServiceManagement = provider.GetRequiredService<RedisServiceManagement>();

        Assert.Same(serviceManagement, concreteServiceManagement);
    }

    [Fact]
    public void RedisOptions_Validate_RejectsSentinelWithoutNodes()
    {
        var options = new RedisOptions
        {
            Type = RedisType.Sentinel,
            Master = "mymaster"
        };

        var exception = Assert.Throws<ArgumentException>(() => options.Validate());

        Assert.Contains("Sentinel", exception.Message);
    }

    [Fact]
    public void RedisServiceManagement_Constructor_ValidatesOptions()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RedisServiceManagement(
            CreateUninitializedRedisClient(),
            new RedisServiceManagementOptions
            {
                HealthCheckIntervalSeconds = 0
            }));

        Assert.Contains("HealthCheckIntervalSeconds", exception.Message);
    }

    [Fact]
    public void RedisEventBus_SubscribeDynamic_RejectsBlankEventName()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var bus = new RedisEventBus(CreateUninitializedRedisClient(), provider, new RedisEventBusOptions());

        var exception = Assert.Throws<ArgumentException>(() => bus.SubscribeDynamic<TestDynamicHandler>(" "));

        Assert.Contains("Event name is required", exception.Message);
    }

    [Fact]
    public void RedisServiceManagement_SelectByWeight_RejectsEmptyServices()
    {
        var method = typeof(RedisServiceManagement).GetMethod("SelectByWeight", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(RedisServiceManagement).FullName, "SelectByWeight");

        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, new object[] { new List<SyZero.Service.ServiceInfo>() }));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    private static RedisClient CreateUninitializedRedisClient()
    {
        return (RedisClient)RuntimeHelpers.GetUninitializedObject(typeof(RedisClient));
    }

    private sealed class TestDynamicHandler : IDynamicEventHandler
    {
        public Task HandleAsync(string eventName, dynamic eventData)
        {
            return Task.CompletedTask;
        }
    }
}
