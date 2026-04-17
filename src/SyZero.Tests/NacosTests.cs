using Moq;
using Nacos.V2;
using Nacos.V2.Naming.Event;
using SyZero.Cache;
using SyZero.Nacos;
using SyZero.Runtime.Security;
using ServiceInfo = SyZero.Service.ServiceInfo;
using Xunit;
using NacosInstance = Nacos.V2.Naming.Dtos.Instance;

namespace SyZero.Tests;

public class NacosTests
{
    [Fact]
    public async Task GetService_ReturnsAllInstances_AndCachesSnapshots()
    {
        var namingService = new Mock<INacosNamingService>(MockBehavior.Strict);
        namingService.Setup(service => service.GetAllInstances("svc"))
            .ReturnsAsync(new List<NacosInstance>
            {
                new()
                {
                    InstanceId = "node-1",
                    ServiceName = "svc",
                    Ip = "127.0.0.1",
                    Port = 8080,
                    Healthy = false,
                    Enabled = true,
                    Metadata = new Dictionary<string, string>
                    {
                        ["Protocol"] = "HTTP"
                    }
                }
            });

        var serviceManagement = new ServiceManagement(namingService.Object, new InMemoryCache());

        var firstRead = await serviceManagement.GetService("svc");
        firstRead[0].ServiceAddress = "mutated";
        firstRead[0].Metadata["Protocol"] = "HTTPS";

        var secondRead = await serviceManagement.GetService("svc");

        Assert.Single(secondRead);
        Assert.Equal("127.0.0.1", secondRead[0].ServiceAddress);
        Assert.Equal(ProtocolType.HTTP, secondRead[0].ServiceProtocol);
        Assert.False(secondRead[0].IsHealthy);
        namingService.Verify(service => service.GetAllInstances("svc"), Times.Once);
        namingService.Verify(service => service.SelectInstances(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task GetHealthyServices_HandlesMissingMetadata()
    {
        var namingService = new Mock<INacosNamingService>(MockBehavior.Strict);
        namingService.Setup(service => service.SelectInstances("svc", true))
            .ReturnsAsync(new List<NacosInstance>
            {
                new()
                {
                    InstanceId = "node-1",
                    ServiceName = "svc",
                    Ip = "127.0.0.1",
                    Port = 8080,
                    Healthy = true,
                    Enabled = true,
                    Metadata = null
                }
            });

        var serviceManagement = new ServiceManagement(namingService.Object, new InMemoryCache());

        var services = await serviceManagement.GetHealthyServices("svc");

        var service = Assert.Single(services);
        Assert.Equal(ProtocolType.HTTP, service.ServiceProtocol);
        Assert.Empty(service.Metadata);
        Assert.Equal(10, service.HealthCheckIntervalSeconds);
        Assert.Equal(5, service.HealthCheckTimeoutSeconds);
    }

    [Fact]
    public async Task RegisterService_AndDeregisterService_UseServiceIdRoundTrip()
    {
        var namingService = new Mock<INacosNamingService>(MockBehavior.Strict);
        namingService.Setup(service => service.RegisterInstance("svc", It.IsAny<NacosInstance>()))
            .Returns(Task.CompletedTask);
        namingService.Setup(service => service.DeregisterInstance("svc", It.Is<NacosInstance>(instance =>
            instance.InstanceId == "node-1" &&
            instance.Ip == "127.0.0.1" &&
            instance.Port == 8080)))
            .Returns(Task.CompletedTask);

        var serviceManagement = new ServiceManagement(namingService.Object, new InMemoryCache());

        await serviceManagement.RegisterService(new ServiceInfo
        {
            ServiceID = "node-1",
            ServiceName = "svc",
            ServiceAddress = "127.0.0.1",
            ServicePort = 8080,
            ServiceProtocol = ProtocolType.HTTP
        });
        await serviceManagement.DeregisterService("node-1");

        namingService.Verify(service => service.RegisterInstance("svc", It.Is<NacosInstance>(instance =>
            instance.InstanceId == "node-1" &&
            instance.Ip == "127.0.0.1" &&
            instance.Port == 8080 &&
            instance.Metadata["Protocol"] == "HTTP")), Times.Once);
        namingService.Verify(service => service.DeregisterInstance("svc", It.IsAny<NacosInstance>()), Times.Once);
    }

    [Fact]
    public async Task Subscribe_ReplacesExistingListener()
    {
        var namingService = new Mock<INacosNamingService>(MockBehavior.Strict);
        IEventListener? firstListener = null;

        namingService.Setup(service => service.Subscribe("svc", It.IsAny<IEventListener>()))
            .Callback<string, IEventListener>((_, listener) =>
            {
                firstListener ??= listener;
            })
            .Returns(Task.CompletedTask);
        namingService.Setup(service => service.Unsubscribe("svc", It.IsAny<IEventListener>()))
            .Returns(Task.CompletedTask);

        var serviceManagement = new ServiceManagement(namingService.Object, new InMemoryCache());

        await serviceManagement.Subscribe("svc", _ => { });
        await serviceManagement.Subscribe("svc", _ => { });

        namingService.Verify(service => service.Unsubscribe("svc", It.Is<IEventListener>(listener => listener == firstListener)), Times.Once);
        namingService.Verify(service => service.Subscribe("svc", It.IsAny<IEventListener>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Subscribe_MapsInstancesChangeEvent_AndClearsCachedSnapshot()
    {
        var namingService = new Mock<INacosNamingService>(MockBehavior.Strict);
        IEventListener? listener = null;
        namingService.Setup(service => service.GetAllInstances("svc"))
            .ReturnsAsync(new List<NacosInstance>
            {
                new()
                {
                    InstanceId = "node-1",
                    ServiceName = "svc",
                    Ip = "127.0.0.1",
                    Port = 8080,
                    Healthy = true,
                    Enabled = true,
                    Metadata = new Dictionary<string, string>()
                }
            });
        namingService.Setup(service => service.Subscribe("svc", It.IsAny<IEventListener>()))
            .Callback<string, IEventListener>((_, eventListener) => listener = eventListener)
            .Returns(Task.CompletedTask);

        var serviceManagement = new ServiceManagement(namingService.Object, new InMemoryCache());
        await serviceManagement.GetService("svc");

        List<ServiceInfo>? callbackServices = null;
        await serviceManagement.Subscribe("svc", services => callbackServices = services);

        await listener!.OnEvent(new InstancesChangeEvent(
            "svc",
            string.Empty,
            string.Empty,
            new List<NacosInstance>
            {
                new()
                {
                    InstanceId = "node-2",
                    ServiceName = "svc",
                    Ip = "10.0.0.2",
                    Port = 9090,
                    Healthy = true,
                    Enabled = true,
                    Metadata = new Dictionary<string, string>
                    {
                        ["Protocol"] = "HTTPS"
                    }
                }
            }));

        var refreshed = await serviceManagement.GetService("svc");

        var callbackService = Assert.Single(callbackServices!);
        Assert.Equal("node-2", callbackService.ServiceID);
        var refreshedService = Assert.Single(refreshed);
        Assert.Equal("node-1", refreshedService.ServiceID);
        namingService.Verify(service => service.GetAllInstances("svc"), Times.Exactly(2));
    }

    private sealed class InMemoryCache : ICache
    {
        private readonly Dictionary<string, object?> _store = new();

        public bool Exist(string key) => _store.ContainsKey(key);

        public string[] GetKeys(string pattern) => _store.Keys.ToArray();

        public T Get<T>(string key) => _store.TryGetValue(key, out var value) ? (T)value! : default!;

        public void Remove(string key) => _store.Remove(key);

        public Task RemoveAsync(string key)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key) => Task.CompletedTask;

        public void Set<T>(string key, T value, int exprireTime = 86400) => _store[key] = value;

        public Task SetAsync<T>(string key, T value, int exprireTime = 86400)
        {
            Set(key, value, exprireTime);
            return Task.CompletedTask;
        }
    }
}
