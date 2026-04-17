using SyZero.Cache;
using SyZero.Redis;
using SyZero.Runtime.Security;
using SyZero.Service;
using SyZero.Service.DBServiceManagement;
using SyZero.Service.DBServiceManagement.Entity;
using SyZero.Service.DBServiceManagement.Repository;
using SyZero.Service.LocalServiceManagement;
using Xunit;

namespace SyZero.Tests;

public class ServiceManagementTests
{
    [Fact]
    public async Task DBServiceManagement_GetServiceInstance_IsSafeUnderConcurrency()
    {
        var repository = new InMemoryServiceRegistryRepository(new[]
        {
            CreateEntity("svc", "1", 1),
            CreateEntity("svc", "2", 3)
        });

        using var serviceManagement = new DBServiceManagement(
            repository,
            new InMemoryCache(),
            new DBServiceManagementOptions
            {
                CacheExpirationSeconds = 0,
                EnableHealthCheck = false,
                AutoCleanExpiredServices = false,
                EnableLeaderElection = false
            });

        var results = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => serviceManagement.GetServiceInstance("svc")));

        Assert.All(results, result => Assert.Contains(result.ServiceID, new[] { "1", "2" }));
    }

    [Fact]
    public async Task DBServiceManagement_RegisterService_NotifiesSubscribers()
    {
        var completionSource = new TaskCompletionSource<List<ServiceInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new InMemoryServiceRegistryRepository();

        using var serviceManagement = new DBServiceManagement(
            repository,
            new InMemoryCache(),
            new DBServiceManagementOptions
            {
                CacheExpirationSeconds = 0,
                EnableHealthCheck = false,
                AutoCleanExpiredServices = false,
                EnableLeaderElection = false
            });

        await serviceManagement.Subscribe("svc", services => completionSource.TrySetResult(services));
        await serviceManagement.RegisterService(new ServiceInfo
        {
            ServiceID = "node-1",
            ServiceName = "svc",
            ServiceAddress = "127.0.0.1",
            ServicePort = 8080,
            ServiceProtocol = ProtocolType.HTTP,
            Weight = 1
        });

        var callbackServices = await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(callbackServices);
        Assert.Equal("node-1", callbackServices[0].ServiceID);
    }

    [Fact]
    public async Task RedisServiceManagement_SelectByWeight_IsSafeUnderConcurrency()
    {
        var services = new List<ServiceInfo>
        {
            new() { ServiceID = "1", ServiceName = "svc", Weight = 1 },
            new() { ServiceID = "2", ServiceName = "svc", Weight = 2 }
        };
        var method = typeof(RedisServiceManagement).GetMethod("SelectByWeight", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(RedisServiceManagement).FullName, "SelectByWeight");

        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() =>
            (ServiceInfo)(method.Invoke(null, new object[] { services }) ?? throw new InvalidOperationException("SelectByWeight returned null.")))));

        Assert.All(results, result => Assert.Contains(result.ServiceID, new[] { "1", "2" }));
    }

    [Fact]
    public async Task LocalServiceManagement_GetServiceInstance_IsSafeUnderConcurrency()
    {
        using var serviceManagement = new LocalServiceManagement(new LocalServiceManagementOptions
        {
            EnableFilePersistence = false,
            EnableFileWatcher = false,
            EnableHealthCheck = false,
            AutoCleanExpiredServices = false,
            EnableLeaderElection = false
        });

        await serviceManagement.RegisterService(new ServiceInfo
        {
            ServiceID = "1",
            ServiceName = "svc",
            Weight = 1,
            Enabled = true,
            IsHealthy = true
        });
        await serviceManagement.RegisterService(new ServiceInfo
        {
            ServiceID = "2",
            ServiceName = "svc",
            Weight = 3,
            Enabled = true,
            IsHealthy = true
        });

        var results = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => serviceManagement.GetServiceInstance("svc")));

        Assert.All(results, result => Assert.Contains(result.ServiceID, new[] { "1", "2" }));
    }

    [Fact]
    public async Task LocalServiceManagement_GetService_ReturnsSnapshots()
    {
        using var serviceManagement = new LocalServiceManagement(new LocalServiceManagementOptions
        {
            EnableFilePersistence = false,
            EnableFileWatcher = false,
            EnableHealthCheck = false,
            AutoCleanExpiredServices = false,
            EnableLeaderElection = false
        });

        await serviceManagement.RegisterService(new ServiceInfo
        {
            ServiceID = "node-1",
            ServiceName = "svc",
            ServiceAddress = "127.0.0.1",
            Tags = new List<string> { "v1" },
            Metadata = new Dictionary<string, string> { ["env"] = "test" }
        });

        var firstRead = await serviceManagement.GetService("svc");
        firstRead[0].ServiceAddress = "mutated";
        firstRead[0].Tags.Add("changed");
        firstRead[0].Metadata["env"] = "changed";

        var secondRead = await serviceManagement.GetService("svc");

        Assert.Equal("127.0.0.1", secondRead[0].ServiceAddress);
        Assert.Equal(new[] { "v1" }, secondRead[0].Tags);
        Assert.Equal("test", secondRead[0].Metadata["env"]);
    }

    private static ServiceRegistryEntity CreateEntity(string serviceName, string serviceId, double weight)
    {
        return new ServiceRegistryEntity
        {
            ServiceID = serviceId,
            ServiceName = serviceName,
            ServiceAddress = "127.0.0.1",
            ServicePort = 8080,
            ServiceProtocol = ProtocolType.HTTP.ToString(),
            Enabled = true,
            IsHealthy = true,
            Weight = weight,
            RegisterTime = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };
    }

    private sealed class InMemoryServiceRegistryRepository : IServiceRegistryRepository
    {
        private readonly List<ServiceRegistryEntity> _entities;
        private readonly object _syncRoot = new();

        public InMemoryServiceRegistryRepository(IEnumerable<ServiceRegistryEntity>? entities = null)
        {
            _entities = entities?.ToList() ?? new List<ServiceRegistryEntity>();
        }

        public Task<List<ServiceRegistryEntity>> GetByServiceNameAsync(string serviceName)
        {
            lock (_syncRoot)
            {
                return Task.FromResult(_entities.Where(entity => entity.ServiceName == serviceName).Select(Clone).ToList());
            }
        }

        public Task<List<ServiceRegistryEntity>> GetHealthyByServiceNameAsync(string serviceName, int expireSeconds)
        {
            lock (_syncRoot)
            {
                var now = DateTime.UtcNow;
                var entities = _entities
                    .Where(entity => entity.ServiceName == serviceName
                                     && entity.Enabled
                                     && entity.IsHealthy
                                     && (now - entity.LastHeartbeat).TotalSeconds <= expireSeconds)
                    .Select(Clone)
                    .ToList();
                return Task.FromResult(entities);
            }
        }

        public Task<ServiceRegistryEntity?> GetByServiceIdAsync(string serviceId)
        {
            lock (_syncRoot)
            {
                return Task.FromResult(_entities.Where(entity => entity.ServiceID == serviceId).Select(Clone).FirstOrDefault());
            }
        }

        public Task<List<string>> GetAllServiceNamesAsync()
        {
            lock (_syncRoot)
            {
                return Task.FromResult(_entities.Select(entity => entity.ServiceName).Distinct().ToList());
            }
        }

        public Task RegisterAsync(ServiceRegistryEntity entity)
        {
            lock (_syncRoot)
            {
                _entities.RemoveAll(existing => existing.ServiceID == entity.ServiceID);
                _entities.Add(Clone(entity));
                return Task.CompletedTask;
            }
        }

        public Task DeregisterAsync(string serviceId)
        {
            lock (_syncRoot)
            {
                _entities.RemoveAll(entity => entity.ServiceID == serviceId);
                return Task.CompletedTask;
            }
        }

        public Task UpdateHeartbeatAsync(string serviceId)
        {
            lock (_syncRoot)
            {
                var entity = _entities.FirstOrDefault(item => item.ServiceID == serviceId);
                if (entity != null)
                {
                    entity.LastHeartbeat = DateTime.UtcNow;
                }

                return Task.CompletedTask;
            }
        }

        public Task UpdateHealthStatusAsync(string serviceId, bool isHealthy)
        {
            lock (_syncRoot)
            {
                var entity = _entities.FirstOrDefault(item => item.ServiceID == serviceId);
                if (entity != null)
                {
                    entity.IsHealthy = isHealthy;
                }

                return Task.CompletedTask;
            }
        }

        public Task CleanExpiredServicesAsync(int expireSeconds)
        {
            lock (_syncRoot)
            {
                var threshold = DateTime.UtcNow.AddSeconds(-expireSeconds);
                _entities.RemoveAll(entity => entity.LastHeartbeat < threshold);
                return Task.CompletedTask;
            }
        }

        private static ServiceRegistryEntity Clone(ServiceRegistryEntity entity)
        {
            return new ServiceRegistryEntity
            {
                Id = entity.Id,
                ServiceID = entity.ServiceID,
                ServiceName = entity.ServiceName,
                ServiceAddress = entity.ServiceAddress,
                ServicePort = entity.ServicePort,
                ServiceProtocol = entity.ServiceProtocol,
                Version = entity.Version,
                Group = entity.Group,
                Tags = entity.Tags,
                Metadata = entity.Metadata,
                IsHealthy = entity.IsHealthy,
                Enabled = entity.Enabled,
                Weight = entity.Weight,
                RegisterTime = entity.RegisterTime,
                LastHeartbeat = entity.LastHeartbeat,
                HealthCheckUrl = entity.HealthCheckUrl,
                Region = entity.Region,
                Zone = entity.Zone
            };
        }
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
