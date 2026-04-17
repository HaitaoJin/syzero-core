using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Cache;
using SyZero.Domain.Entities;
using SyZero.Domain.Repository;
using SyZero.Runtime.Security;
using SyZero.Service.DBServiceManagement.Entity;
using SyZero.Service.DBServiceManagement.Repository;

namespace SyZero.Service.DBServiceManagement
{
    /// <summary>
    /// 数据库服务管理实现
    /// 适用于需要持久化服务注册信息到数据库的场景
    /// </summary>
    public class DBServiceManagement : IServiceManagement, IDisposable
    {
        private readonly IServiceRegistryRepository _repository;
        private readonly ILeaderElectionRepository _leaderRepository;
        private readonly ICache _cache;
        private readonly DBServiceManagementOptions _options;
        private readonly ConcurrentDictionary<string, Action<List<ServiceInfo>>> _subscriptions = new ConcurrentDictionary<string, Action<List<ServiceInfo>>>();
        private static readonly ThreadLocal<Random> RandomProvider = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));
        private readonly HttpClient _httpClient;
        private readonly string _instanceId;
        private Timer _healthCheckTimer;
        private Timer _cleanupTimer;
        private Timer _leaderRenewTimer;
        private bool _isLeader = false;

        /// <summary>
        /// 当前实例是否为 Leader
        /// </summary>
        public bool IsLeader => _isLeader;

        /// <summary>
        /// 当前实例ID
        /// </summary>
        public string InstanceId => _instanceId;

        public DBServiceManagement(
            IServiceRegistryRepository repository,
            ICache cache,
            DBServiceManagementOptions options,
            ILeaderElectionRepository leaderRepository = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options ?? new DBServiceManagementOptions();
            _leaderRepository = leaderRepository;
            _instanceId = Guid.NewGuid().ToString("N");

            // 初始化 HttpClient 用于健康检查
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds)
            };

            // 如果启用 Leader 选举，先尝试获取 Leader
            if (_options.EnableLeaderElection && _leaderRepository != null)
            {
                RunBackgroundTask(TryAcquireLeadershipAsync, "初始化 Leader 选举");

                // 启动 Leader 续期定时器
                _leaderRenewTimer = new Timer(
                    _ => RunBackgroundTask(TryAcquireLeadershipAsync, "Leader 续期"),
                    null,
                    TimeSpan.FromSeconds(_options.LeaderLockRenewIntervalSeconds),
                    TimeSpan.FromSeconds(_options.LeaderLockRenewIntervalSeconds));
            }
            else
            {
                // 不启用选举时，所有实例都是 Leader
                _isLeader = true;
            }

            // 启动健康检查定时器
            if (_options.EnableHealthCheck)
            {
                _healthCheckTimer = new Timer(
                    _ => RunBackgroundTask(PerformHealthCheckAsync, "健康检查"),
                    null,
                    TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds),
                    TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));
            }

            // 启动清理定时器
            if (_options.AutoCleanExpiredServices)
            {
                _cleanupTimer = new Timer(
                    _ => RunBackgroundTask(CleanExpiredServicesAsync, "清理过期服务"),
                    null,
                    TimeSpan.FromSeconds(_options.AutoCleanIntervalSeconds),
                    TimeSpan.FromSeconds(_options.AutoCleanIntervalSeconds));
            }
        }

        #region Leader 选举

        /// <summary>
        /// 尝试获取 Leader 权限
        /// </summary>
        private async Task TryAcquireLeadershipAsync()
        {
            if (_leaderRepository == null) return;

            try
            {
                const string leaderKey = "ServiceManagement";
                var currentLock = await _leaderRepository.GetLeaderLockAsync(leaderKey);

                if (currentLock != null)
                {
                    // 如果是自己持有的锁，续期
                    if (currentLock.InstanceId == _instanceId)
                    {
                        await _leaderRepository.RenewLeaderLockAsync(leaderKey, _instanceId, _options.LeaderLockExpireSeconds);
                        _isLeader = true;
                        return;
                    }

                    // 检查锁是否过期
                    if (currentLock.ExpireTime > DateTime.UtcNow)
                    {
                        // 锁未过期，当前实例不是 Leader
                        _isLeader = false;
                        return;
                    }

                    // 锁已过期，可以抢占
                    Console.WriteLine($"SyZero.DB: Leader 锁已过期 (原 Leader: {currentLock.InstanceId})，尝试获取...");
                }

                // 尝试获取锁
                var success = await _leaderRepository.TryAcquireLeaderLockAsync(leaderKey, _instanceId, _options.LeaderLockExpireSeconds);
                _isLeader = success;

                if (success)
                {
                    Console.WriteLine($"SyZero.DB: 当前实例 [{_instanceId}] 成为 Leader");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.DB: 获取 Leader 权限失败: {ex.Message}");
                _isLeader = false;
            }
        }

        /// <summary>
        /// 释放 Leader 权限
        /// </summary>
        private async Task ReleaseLeadershipAsync()
        {
            if (!_isLeader || _leaderRepository == null) return;

            try
            {
                const string leaderKey = "ServiceManagement";
                await _leaderRepository.ReleaseLeaderLockAsync(leaderKey, _instanceId);
                Console.WriteLine($"SyZero.DB: 实例 [{_instanceId}] 释放 Leader 权限");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.DB: 释放 Leader 权限失败: {ex.Message}");
            }
            finally
            {
                _isLeader = false;
            }
        }

        #endregion

        #region 服务查询

        public async Task<List<ServiceInfo>> GetService(string serviceName)
        {
            var cacheKey = $"DB:Service:{serviceName}";
            if (_cache.Exist(cacheKey))
            {
                return _cache.Get<List<ServiceInfo>>(cacheKey);
            }

            var entities = await _repository.GetByServiceNameAsync(serviceName);
            var services = entities.Select(MapToServiceInfo).ToList();
            
            if (_options.CacheExpirationSeconds > 0)
            {
                _cache.Set(cacheKey, services, _options.CacheExpirationSeconds);
            }
            
            return services;
        }

        public async Task<List<ServiceInfo>> GetHealthyServices(string serviceName)
        {
            var cacheKey = $"DB:HealthyService:{serviceName}";
            if (_cache.Exist(cacheKey))
            {
                return _cache.Get<List<ServiceInfo>>(cacheKey);
            }

            var entities = await _repository.GetHealthyByServiceNameAsync(serviceName, _options.ServiceExpireSeconds);
            var services = entities.Select(MapToServiceInfo).ToList();
            
            if (_options.CacheExpirationSeconds > 0)
            {
                _cache.Set(cacheKey, services, _options.CacheExpirationSeconds);
            }
            
            return services;
        }

        public async Task<ServiceInfo> GetServiceInstance(string serviceName)
        {
            var services = await GetHealthyServices(serviceName);
            if (services == null || services.Count == 0)
            {
                throw new Exception($"SyZero.DB:未找到可用的{serviceName}服务实例!");
            }
            // 加权随机负载均衡
            return SelectByWeight(services);
        }

        public async Task<List<string>> GetAllServices()
        {
            return await _repository.GetAllServiceNamesAsync();
        }

        private static ServiceInfo SelectByWeight(List<ServiceInfo> services)
        {
            var totalWeight = services.Sum(s => s.Weight);
            if (totalWeight <= 0)
            {
                return services[GetRandom().Next(services.Count)];
            }

            var randomWeight = GetRandom().NextDouble() * totalWeight;
            var currentWeight = 0.0;
            foreach (var service in services)
            {
                currentWeight += service.Weight;
                if (randomWeight <= currentWeight)
                {
                    return service;
                }
            }
            return services.Last();
        }

        #endregion

        #region 服务注册/注销

        public async Task RegisterService(ServiceInfo serviceInfo)
        {
            if (string.IsNullOrEmpty(serviceInfo.ServiceID))
            {
                serviceInfo.ServiceID = Guid.NewGuid().ToString("N");
            }
            if (serviceInfo.RegisterTime == default)
            {
                serviceInfo.RegisterTime = DateTime.UtcNow;
            }
            serviceInfo.LastHeartbeat = DateTime.UtcNow;

            var entity = MapToEntity(serviceInfo);
            await _repository.RegisterAsync(entity);
            
            // 清除缓存
            ClearServiceCache(serviceInfo.ServiceName);
            
            // 通知订阅者
            await NotifySubscribersAsync(serviceInfo.ServiceName);
        }

        public async Task DeregisterService(string serviceId)
        {
            var entity = await _repository.GetByServiceIdAsync(serviceId);
            if (entity != null)
            {
                await _repository.DeregisterAsync(serviceId);
                
                // 清除缓存
                ClearServiceCache(entity.ServiceName);
                
                // 通知订阅者
                await NotifySubscribersAsync(entity.ServiceName);
            }
        }

        #endregion

        #region 健康检查

        public async Task<bool> IsServiceHealthy(string serviceName)
        {
            var healthyServices = await GetHealthyServices(serviceName);
            return healthyServices != null && healthyServices.Count > 0;
        }

        /// <summary>
        /// 更新心跳
        /// </summary>
        public async Task HeartbeatAsync(string serviceId)
        {
            await _repository.UpdateHeartbeatAsync(serviceId);
            
            // 清除缓存
            var entity = await _repository.GetByServiceIdAsync(serviceId);
            if (entity != null)
            {
                ClearServiceCache(entity.ServiceName);
            }
        }

        #endregion

        #region 服务订阅

        public Task Subscribe(string serviceName, Action<List<ServiceInfo>> callback)
        {
            _subscriptions[serviceName] = callback;
            return Task.CompletedTask;
        }

        public Task Unsubscribe(string serviceName)
        {
            _subscriptions.TryRemove(serviceName, out _);
            return Task.CompletedTask;
        }

        private async Task NotifySubscribersAsync(string serviceName)
        {
            if (_subscriptions.TryGetValue(serviceName, out var callback))
            {
                var services = await GetHealthyServices(serviceName);
                callback?.Invoke(services);
            }
        }

        /// <summary>
        /// 执行健康检查
        /// </summary>
        private async Task PerformHealthCheckAsync()
        {
            // 如果启用了 Leader 选举且当前不是 Leader，则跳过健康检查
            if (_options.EnableLeaderElection && _leaderRepository != null && !_isLeader)
            {
                return;
            }

            try
            {
                var allServiceNames = await _repository.GetAllServiceNamesAsync();
                var changedServices = new HashSet<string>();

                foreach (var serviceName in allServiceNames)
                {
                    var entities = await _repository.GetByServiceNameAsync(serviceName);
                    foreach (var entity in entities)
                    {
                        var previousHealth = entity.IsHealthy;
                        var currentHealth = await CheckServiceHealthAsync(entity);

                        if (previousHealth != currentHealth)
                        {
                            await _repository.UpdateHealthStatusAsync(entity.ServiceID, currentHealth);
                            changedServices.Add(serviceName);
                        }
                    }
                }

                // 清除缓存并通知变更的服务
                foreach (var serviceName in changedServices)
                {
                    ClearServiceCache(serviceName);
                    await NotifySubscribersAsync(serviceName);
                }
            }
            catch
            {
                // 忽略健康检查错误
            }
        }

        /// <summary>
        /// 检查单个服务实例的健康状态
        /// </summary>
        private async Task<bool> CheckServiceHealthAsync(ServiceRegistryEntity entity)
        {
            // 如果配置了健康检查URL，以 HTTP 检查结果为准
            if (!string.IsNullOrEmpty(entity.HealthCheckUrl))
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds));
                    var response = await _httpClient.GetAsync(entity.HealthCheckUrl, cts.Token);
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }

            // 没有配置健康检查URL，检查心跳是否过期
            var heartbeatExpired = (DateTime.UtcNow - entity.LastHeartbeat).TotalSeconds > _options.ServiceExpireSeconds;
            return !heartbeatExpired;
        }

        private async Task CleanExpiredServicesAsync()
        {
            // 如果启用了 Leader 选举且当前不是 Leader，则跳过清理
            if (_options.EnableLeaderElection && _leaderRepository != null && !_isLeader)
            {
                return;
            }

            try
            {
                await _repository.CleanExpiredServicesAsync(_options.ServiceCleanSeconds);
            }
            catch
            {
                // 忽略清理错误
            }
        }

        #endregion

        #region 辅助方法

        private void ClearServiceCache(string serviceName)
        {
            _cache.Remove($"DB:Service:{serviceName}");
            _cache.Remove($"DB:HealthyService:{serviceName}");
        }

        private void RunBackgroundTask(Func<Task> taskFactory, string operationName)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await taskFactory();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.DB: {operationName}失败: {ex.Message}");
                }
            });
        }

        private static Random GetRandom()
        {
            return RandomProvider.Value ?? new Random(Guid.NewGuid().GetHashCode());
        }

        private ServiceInfo MapToServiceInfo(ServiceRegistryEntity entity)
        {
            return new ServiceInfo
            {
                ServiceID = entity.ServiceID,
                ServiceName = entity.ServiceName,
                ServiceAddress = entity.ServiceAddress,
                ServicePort = entity.ServicePort,
                ServiceProtocol = Enum.TryParse<ProtocolType>(entity.ServiceProtocol, out var protocol) ? protocol : ProtocolType.HTTP,
                Version = entity.Version,
                Group = entity.Group,
                Tags = string.IsNullOrEmpty(entity.Tags) ? new List<string>() : entity.Tags.Split(',').ToList(),
                Metadata = DeserializeMetadata(entity.Metadata),
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

        private ServiceRegistryEntity MapToEntity(ServiceInfo serviceInfo)
        {
            return new ServiceRegistryEntity
            {
                ServiceID = serviceInfo.ServiceID,
                ServiceName = serviceInfo.ServiceName,
                ServiceAddress = serviceInfo.ServiceAddress,
                ServicePort = serviceInfo.ServicePort,
                ServiceProtocol = serviceInfo.ServiceProtocol.ToString(),
                Version = serviceInfo.Version,
                Group = serviceInfo.Group,
                Tags = serviceInfo.Tags != null ? string.Join(",", serviceInfo.Tags) : null,
                Metadata = SerializeMetadata(serviceInfo.Metadata),
                IsHealthy = serviceInfo.IsHealthy,
                Enabled = serviceInfo.Enabled,
                Weight = serviceInfo.Weight,
                RegisterTime = serviceInfo.RegisterTime,
                LastHeartbeat = serviceInfo.LastHeartbeat ?? DateTime.UtcNow,
                HealthCheckUrl = serviceInfo.HealthCheckUrl,
                Region = serviceInfo.Region,
                Zone = serviceInfo.Zone
            };
        }

        private string SerializeMetadata(Dictionary<string, string> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return null;
            return System.Text.Json.JsonSerializer.Serialize(metadata);
        }

        private Dictionary<string, string> DeserializeMetadata(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, string>();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public void Dispose()
        {
            // 释放 Leader 权限
            if (_options.EnableLeaderElection && _leaderRepository != null)
            {
                ReleaseLeadershipAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }

            _leaderRenewTimer?.Dispose();
            _healthCheckTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _httpClient?.Dispose();
        }

        #endregion
    }
}
