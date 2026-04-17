using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NConsul;
using NConsul.Interfaces;
using SyZero.Cache;
using SyZero.Consul;
using SyZero.Consul.Config;
using SyZero.Runtime.Security;
using SyZero.Service;
using Xunit;

namespace SyZero.Tests;

public class ConsulTests
{
    [Fact]
    public void AddConsul_RegistersServiceManagementAndConsulClient()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Consul:ConsulAddress"] = "http://127.0.0.1:8500",
                ["Consul:Token"] = "token"
            })
            .Build();

        services.AddSingleton<ICache>(new DictionaryCache());
        services.AddConsul(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IConsulClient>());
        Assert.IsType<ServiceManagement>(provider.GetRequiredService<IServiceManagement>());
    }

    [Fact]
    public async Task ConsulServiceManagement_GetService_ReturnsCachedSnapshots()
    {
        var catalog = new Mock<ICatalogEndpoint>(MockBehavior.Strict);
        catalog.Setup(endpoint => endpoint.Service("svc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<CatalogService[]>
            {
                StatusCode = HttpStatusCode.OK,
                Response =
                [
                    new CatalogService
                    {
                        ServiceID = "node-1",
                        ServiceName = "svc",
                        Address = "10.0.0.2",
                        ServiceAddress = "127.0.0.1",
                        ServicePort = 8080,
                        ServiceTags = ["v1"],
                        ServiceMeta = new Dictionary<string, string>
                        {
                            ["Version"] = "1.0.0"
                        }
                    }
                ]
            });

        var serviceManagement = CreateServiceManagement(catalog: catalog.Object);

        var firstRead = await serviceManagement.GetService("svc");
        firstRead[0].ServiceAddress = "mutated";
        firstRead[0].Tags.Add("changed");
        firstRead[0].Metadata["Version"] = "2.0.0";

        var secondRead = await serviceManagement.GetService("svc");

        Assert.Equal("127.0.0.1", secondRead[0].ServiceAddress);
        Assert.Equal(new[] { "v1" }, secondRead[0].Tags);
        Assert.Equal("1.0.0", secondRead[0].Metadata["Version"]);
        catalog.Verify(endpoint => endpoint.Service("svc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsulServiceManagement_GetHealthyServices_UsesNodeAddress_AndHandlesMissingMetadata()
    {
        var health = new Mock<IHealthEndpoint>(MockBehavior.Strict);
        health.Setup(endpoint => endpoint.Service("svc", string.Empty, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<ServiceEntry[]>
            {
                StatusCode = HttpStatusCode.OK,
                Response =
                [
                    new ServiceEntry
                    {
                        Node = new Node
                        {
                            Address = "10.10.0.25"
                        },
                        Service = new AgentService
                        {
                            ID = "node-1",
                            Service = "svc",
                            Address = string.Empty,
                            Port = 8080,
                            Meta = null!,
                            Tags = null!
                        }
                    }
                ]
            });

        var serviceManagement = CreateServiceManagement(health: health.Object);

        var services = await serviceManagement.GetHealthyServices("svc");
        var service = Assert.Single(services);

        Assert.Equal("10.10.0.25", service.ServiceAddress);
        Assert.Equal(ProtocolType.HTTP, service.ServiceProtocol);
        Assert.Equal(1.0, service.Weight);
        Assert.Equal("http://10.10.0.25:8080/health", service.HealthCheckUrl);
        Assert.Empty(service.Tags);
        Assert.Empty(service.Metadata);
    }

    [Fact]
    public async Task ConsulServiceManagement_RegisterService_UsesGrpcCheck_AndInvalidatesCache()
    {
        AgentServiceRegistration? capturedRegistration = null;
        var agent = new Mock<IAgentEndpoint>(MockBehavior.Strict);
        agent.Setup(endpoint => endpoint.ServiceRegister(It.IsAny<AgentServiceRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<AgentServiceRegistration, CancellationToken>((registration, _) => capturedRegistration = registration)
            .ReturnsAsync(new WriteResult
            {
                StatusCode = HttpStatusCode.OK
            });

        var cache = new DictionaryCache();
        cache.Set("Consul:svc", new List<ServiceInfo>
        {
            new()
            {
                ServiceID = "stale",
                ServiceName = "svc"
            }
        });

        var serviceManagement = CreateServiceManagement(agent: agent.Object, cache: cache);
        var metadata = new Dictionary<string, string>
        {
            ["custom"] = "value"
        };

        await serviceManagement.RegisterService(new ServiceInfo
        {
            ServiceName = "svc",
            ServiceAddress = "127.0.0.1",
            ServicePort = 5001,
            ServiceProtocol = ProtocolType.GRPC,
            Weight = 0,
            Metadata = metadata
        });

        Assert.False(cache.Exist("Consul:svc"));
        Assert.NotNull(capturedRegistration);
        Assert.False(string.IsNullOrWhiteSpace(capturedRegistration!.ID));
        Assert.Equal("127.0.0.1:5001", capturedRegistration.Check.GRPC);
        Assert.Null(capturedRegistration.Check.HTTP);
        Assert.Equal("value", capturedRegistration.Meta["custom"]);
        Assert.Equal("GRPC", capturedRegistration.Meta["Protocol"]);
        Assert.Equal("1", capturedRegistration.Meta["Weight"]);
        Assert.DoesNotContain("Protocol", metadata.Keys);
    }

    [Fact]
    public async Task ConsulServiceManagement_DeregisterService_ClearsAllConsulCaches()
    {
        var agent = new Mock<IAgentEndpoint>(MockBehavior.Strict);
        agent.Setup(endpoint => endpoint.ServiceDeregister("node-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WriteResult
            {
                StatusCode = HttpStatusCode.OK
            });

        var cache = new DictionaryCache();
        cache.Set("Consul:svc-a", new List<ServiceInfo>());
        cache.Set("Consul:svc-b", new List<ServiceInfo>());
        cache.Set("Other:svc-c", new List<ServiceInfo>());

        var serviceManagement = CreateServiceManagement(agent: agent.Object, cache: cache);

        await serviceManagement.DeregisterService("node-1");

        Assert.False(cache.Exist("Consul:svc-a"));
        Assert.False(cache.Exist("Consul:svc-b"));
        Assert.True(cache.Exist("Other:svc-c"));
    }

    [Fact]
    public void ConsulServiceManagement_SelectByWeight_PrefersPositiveWeights()
    {
        var method = typeof(ServiceManagement).GetMethod("SelectByWeight", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(ServiceManagement).FullName, "SelectByWeight");
        var services = new List<ServiceInfo>
        {
            new() { ServiceID = "zero", ServiceName = "svc", Weight = 0 },
            new() { ServiceID = "positive", ServiceName = "svc", Weight = 5 }
        };

        var results = Enumerable.Range(0, 50)
            .Select(_ => (ServiceInfo)(method.Invoke(null, [services]) ?? throw new InvalidOperationException("SelectByWeight returned null.")))
            .ToList();

        Assert.All(results, result => Assert.Equal("positive", result.ServiceID));
    }

    [Fact]
    public async Task ConsulConfigurationParser_GetConfig_ReturnsEmptyDictionary_WhenOptionalKeyMissing()
    {
        var source = new TestConsulConfigurationSource("svc/config")
        {
            Optional = true
        };
        var parser = new ConsulConfigurationParser(source, (_, _, _) => Task.FromResult(new QueryResult<KVPair>
        {
            StatusCode = HttpStatusCode.NotFound,
            Response = null!
        }));

        var result = await parser.GetConfig(false, source);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ConsulConfigurationParser_GetConfig_Throws_WhenRequiredKeyMissingOnInitialLoad()
    {
        var source = new TestConsulConfigurationSource("svc/config")
        {
            Optional = false
        };
        var parser = new ConsulConfigurationParser(source, (_, _, _) => Task.FromResult(new QueryResult<KVPair>
        {
            StatusCode = HttpStatusCode.NotFound,
            Response = null!
        }));

        var exception = await Assert.ThrowsAsync<FormatException>(() => parser.GetConfig(false, source));

        Assert.Contains("Error_InvalidService", exception.Message);
    }

    private static ServiceManagement CreateServiceManagement(
        ICatalogEndpoint? catalog = null,
        IHealthEndpoint? health = null,
        IAgentEndpoint? agent = null,
        ICache? cache = null)
    {
        var consulClient = new Mock<IConsulClient>(MockBehavior.Strict);
        consulClient.Setup(client => client.Catalog).Returns(catalog ?? new Mock<ICatalogEndpoint>(MockBehavior.Strict).Object);
        consulClient.Setup(client => client.Health).Returns(health ?? new Mock<IHealthEndpoint>(MockBehavior.Strict).Object);
        consulClient.Setup(client => client.Agent).Returns(agent ?? new Mock<IAgentEndpoint>(MockBehavior.Strict).Object);

        return new ServiceManagement(consulClient.Object, cache ?? new DictionaryCache());
    }

    private sealed class TestConsulConfigurationSource : IConsulConfigurationSource
    {
        public TestConsulConfigurationSource(string serviceKey)
        {
            ServiceKey = serviceKey;
        }

        public CancellationToken CancellationToken { get; } = CancellationToken.None;

        public Action<ConsulClientConfiguration>? ConsulClientConfiguration { get; set; }

        public Action<HttpClient>? ConsulHttpClient { get; set; }

        public Action<HttpClientHandler>? ConsulHttpClientHandler { get; set; }

        public string ServiceKey { get; }

        public bool Optional { get; set; }

        public QueryOptions? QueryOptions { get; set; }

        public int ReloadDelay { get; set; }

        public bool ReloadOnChange { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class DictionaryCache : ICache
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
