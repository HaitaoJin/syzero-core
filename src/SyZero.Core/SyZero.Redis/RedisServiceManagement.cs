using FreeRedis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Service;

namespace SyZero.Redis
{
    /// <summary>
    /// Redis 服务管理实现
    /// 适用于分布式部署场景，基于 Redis 实现服务注册、发现和健康检查
    /// </summary>
    public class RedisServiceManagement : IServiceManagement, IDisposable
    {
        private readonly RedisClient _redis;
        private readonly RedisServiceManagementOptions _options;
        private readonly ConcurrentDictionary<string, Action<List<ServiceInfo>>> _subscriptions = new ConcurrentDictionary<string, Action<List<ServiceInfo>>>();
        private static readonly ThreadLocal<Random> RandomProvider = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));
        private readonly HttpClient _httpClient;
        private readonly string _instanceId;
        private Timer _healthCheckTimer;
        private Timer _cleanupTimer;
        private Timer _leaderRenewTimer;
        private bool _isLeader = false;
        private readonly ConcurrentDictionary<string, IDisposable> _pubSubDisposables = new ConcurrentDictionary<string, IDisposable>();

        /// <summary>
        /// 当前实例是否为 Leader
        /// </summary>
        public bool IsLeader => _isLeader;

        /// <summary>
        /// 当前实例ID
        /// </summary>
        public string InstanceId => _instanceId;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="redis">Redis 客户端</param>
        /// <param name="options">配置选项</param>
        public RedisServiceManagement(RedisClient redis, RedisServiceManagementOptions options = null)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _options = options ?? new RedisServiceManagementOptions();
            _instanceId = Guid.NewGuid().ToString("N");

            // 初始化 HttpClient 用于健康检查
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds)
            };

            // 如果启用 Leader 选举，先尝试获取 Leader
            if (_options.EnableLeaderElection)
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
            try
            {
                var leaderKey = $"{_options.LeaderKeyPrefix}ServiceManagement";
                var currentLeader = _redis.Get(leaderKey);

                if (!string.IsNullOrEmpty(currentLeader))
                {
                    // 如果是自己持有的锁，续期
                    if (currentLeader == _instanceId)
                    {
                        _redis.Expire(leaderKey, _options.LeaderLockExpireSeconds);
                        _isLeader = true;
                        return;
                    }

                    // 锁被其他实例持有
                    _isLeader = false;
                    return;
                }

                // 尝试获取锁（使用 SetNx 保证原子性）
                var success = _redis.SetNx(leaderKey, _instanceId, _options.LeaderLockExpireSeconds);
                _isLeader = success;

                if (success)
                {
                    Console.WriteLine($"SyZero.Redis: 当前实例 [{_instanceId}] 成为 Leader");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.Redis: 获取 Leader 权限失败: {ex.Message}");
                _isLeader = false;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 释放 Leader 权限
        /// </summary>
        private void ReleaseLeadership()
        {
            if (!_isLeader) return;

            try
            {
                var leaderKey = $"{_options.LeaderKeyPrefix}ServiceManagement";
                var currentLeader = _redis.Get(leaderKey);

                // 只释放自己持有的锁
                if (currentLeader == _instanceId)
                {
                    _redis.Del(leaderKey);
                    Console.WriteLine($"SyZero.Redis: 实例 [{_instanceId}] 释放 Leader 权限");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.Redis: 释放 Leader 权限失败: {ex.Message}");
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
            var key = GetServiceKey(serviceName);
            var json = _redis.Get(key);

            if (string.IsNullOrEmpty(json))
            {
                return new List<ServiceInfo>();
            }

            var services = JsonSerializer.Deserialize<List<ServiceInfo>>(json) ?? new List<ServiceInfo>();
            return await Task.FromResult(services);
        }

        public async Task<List<ServiceInfo>> GetHealthyServices(string serviceName)
        {
            var services = await GetService(serviceName);
            var now = DateTime.UtcNow;

            return services.Where(s =>
                s.Enabled &&
                s.IsHealthy &&
                (!s.LastHeartbeat.HasValue ||
                 (now - s.LastHeartbeat.Value).TotalSeconds <= _options.ServiceExpireSeconds)
            ).ToList();
        }

        public async Task<ServiceInfo> GetServiceInstance(string serviceName)
        {
            var services = await GetHealthyServices(serviceName);
            if (services == null || services.Count == 0)
            {
                throw new Exception($"SyZero.Redis: 未找到可用的 {serviceName} 服务实例!");
            }
            // 加权随机负载均衡
            return SelectByWeight(services);
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

        public async Task<List<string>> GetAllServices()
        {
            var members = _redis.SMembers(_options.ServiceNamesKey);
            return await Task.FromResult(members?.ToList() ?? new List<string>());
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

            if (serviceInfo.Tags == null)
            {
                serviceInfo.Tags = new List<string>();
            }
            if (serviceInfo.Metadata == null)
            {
                serviceInfo.Metadata = new Dictionary<string, string>();
            }

            var key = GetServiceKey(serviceInfo.ServiceName);
            var services = await GetService(serviceInfo.ServiceName);

            // 移除已存在的相同 ID 的服务
            services.RemoveAll(s => s.ServiceID == serviceInfo.ServiceID);
            services.Add(serviceInfo);

            // 保存到 Redis
            var json = JsonSerializer.Serialize(services);
            _redis.Set(key, json);

            // 添加服务名到集合
            _redis.SAdd(_options.ServiceNamesKey, serviceInfo.ServiceName);

            // 发布服务变更通知
            PublishServiceChange(serviceInfo.ServiceName);

            await Task.CompletedTask;
        }

        public async Task DeregisterService(string serviceId)
        {
            var serviceNames = await GetAllServices();

            foreach (var serviceName in serviceNames)
            {
                var services = await GetService(serviceName);
                var service = services.FirstOrDefault(s => s.ServiceID == serviceId);

                if (service != null)
                {
                    services.Remove(service);

                    var key = GetServiceKey(serviceName);
                    if (services.Count > 0)
                    {
                        var json = JsonSerializer.Serialize(services);
                        _redis.Set(key, json);
                    }
                    else
                    {
                        _redis.Del(key);
                        _redis.SRem(_options.ServiceNamesKey, serviceName);
                    }

                    // 发布服务变更通知
                    PublishServiceChange(serviceName);
                    break;
                }
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
        /// 更新服务心跳
        /// </summary>
        public async Task HeartbeatAsync(string serviceId)
        {
            var serviceNames = await GetAllServices();

            foreach (var serviceName in serviceNames)
            {
                var services = await GetService(serviceName);
                var service = services.FirstOrDefault(s => s.ServiceID == serviceId);

                if (service != null)
                {
                    service.LastHeartbeat = DateTime.UtcNow;
                    service.IsHealthy = true;

                    var key = GetServiceKey(serviceName);
                    var json = JsonSerializer.Serialize(services);
                    _redis.Set(key, json);
                    break;
                }
            }
        }

        /// <summary>
        /// 执行健康检查
        /// </summary>
        private async Task PerformHealthCheckAsync()
        {
            // 如果启用了 Leader 选举且当前不是 Leader，则跳过健康检查
            if (_options.EnableLeaderElection && !_isLeader)
            {
                return;
            }

            try
            {
                var serviceNames = await GetAllServices();
                var changedServices = new HashSet<string>();

                foreach (var serviceName in serviceNames)
                {
                    var services = await GetService(serviceName);
                    var hasChange = false;

                    foreach (var service in services)
                    {
                        var previousHealth = service.IsHealthy;
                        var currentHealth = await CheckServiceHealthAsync(service);

                        if (previousHealth != currentHealth)
                        {
                            service.IsHealthy = currentHealth;
                            hasChange = true;
                        }
                    }

                    if (hasChange)
                    {
                        var key = GetServiceKey(serviceName);
                        var json = JsonSerializer.Serialize(services);
                        _redis.Set(key, json);
                        changedServices.Add(serviceName);
                    }
                }

                // 发布服务变更通知
                foreach (var serviceName in changedServices)
                {
                    PublishServiceChange(serviceName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.Redis: 健康检查失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查单个服务实例的健康状态
        /// </summary>
        private async Task<bool> CheckServiceHealthAsync(ServiceInfo service)
        {
            // 如果配置了健康检查URL，执行 HTTP 检查
            if (!string.IsNullOrEmpty(service.HealthCheckUrl))
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(
                        service.HealthCheckTimeoutSeconds > 0
                            ? service.HealthCheckTimeoutSeconds
                            : _options.HealthCheckTimeoutSeconds));

                    var response = await _httpClient.GetAsync(service.HealthCheckUrl, cts.Token);
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }

            // 没有配置健康检查URL，检查心跳时间
            if (service.LastHeartbeat.HasValue)
            {
                var expireSeconds = service.HealthCheckIntervalSeconds > 0
                    ? service.HealthCheckIntervalSeconds * 3
                    : _options.ServiceExpireSeconds;
                return (DateTime.UtcNow - service.LastHeartbeat.Value).TotalSeconds <= expireSeconds;
            }

            return service.IsHealthy;
        }

        /// <summary>
        /// 清理过期服务
        /// </summary>
        private async Task CleanExpiredServicesAsync()
        {
            // 如果启用了 Leader 选举且当前不是 Leader，则跳过清理
            if (_options.EnableLeaderElection && !_isLeader)
            {
                return;
            }

            try
            {
                var serviceNames = await GetAllServices();
                var now = DateTime.UtcNow;

                foreach (var serviceName in serviceNames)
                {
                    var services = await GetService(serviceName);
                    var expiredServices = services
                        .Where(s => s.LastHeartbeat.HasValue &&
                                   (now - s.LastHeartbeat.Value).TotalSeconds > _options.ServiceCleanSeconds)
                        .ToList();

                    if (expiredServices.Count > 0)
                    {
                        foreach (var service in expiredServices)
                        {
                            services.Remove(service);
                            Console.WriteLine($"SyZero.Redis: 自动清理过期服务 [{service.ServiceName}] ID={service.ServiceID}");
                        }

                        var key = GetServiceKey(serviceName);
                        if (services.Count > 0)
                        {
                            var json = JsonSerializer.Serialize(services);
                            _redis.Set(key, json);
                        }
                        else
                        {
                            _redis.Del(key);
                            _redis.SRem(_options.ServiceNamesKey, serviceName);
                        }

                        // 发布服务变更通知
                        PublishServiceChange(serviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.Redis: 清理过期服务失败: {ex.Message}");
            }
        }

        #endregion

        #region 服务订阅

        public Task Subscribe(string serviceName, Action<List<ServiceInfo>> callback)
        {
            _subscriptions[serviceName] = callback;

            // 如果启用了发布/订阅，订阅 Redis 频道
            if (_options.EnablePubSub)
            {
                var channel = GetPubSubChannel(serviceName);
                if (_pubSubDisposables.TryRemove(serviceName, out var existing))
                {
                    existing.Dispose();
                }

                _pubSubDisposables[serviceName] = _redis.Subscribe(channel, (ch, msg) =>
                {
                    RunBackgroundTask(async () =>
                    {
                        var services = await GetService(serviceName);
                        callback?.Invoke(services);
                    }, $"服务变更通知回调({serviceName})");
                });
            }

            return Task.CompletedTask;
        }

        public Task Unsubscribe(string serviceName)
        {
            _subscriptions.TryRemove(serviceName, out _);

            if (_options.EnablePubSub)
            {
                var channel = GetPubSubChannel(serviceName);
                _redis.UnSubscribe(channel);
                if (_pubSubDisposables.TryRemove(serviceName, out var disposable))
                {
                    disposable.Dispose();
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 发布服务变更通知
        /// </summary>
        private void PublishServiceChange(string serviceName)
        {
            if (_options.EnablePubSub)
            {
                try
                {
                    var channel = GetPubSubChannel(serviceName);
                    _redis.Publish(channel, DateTime.UtcNow.ToString("O"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.Redis: 发布服务变更通知失败: {ex.Message}");
                }
            }

            if (_subscriptions.TryGetValue(serviceName, out var callback))
            {
                RunBackgroundTask(async () =>
                {
                    var services = await GetService(serviceName);
                    callback?.Invoke(services);
                }, $"本地服务通知({serviceName})");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取服务 Key
        /// </summary>
        private string GetServiceKey(string serviceName)
        {
            return $"{_options.KeyPrefix}{serviceName}";
        }

        /// <summary>
        /// 获取发布/订阅频道
        /// </summary>
        private string GetPubSubChannel(string serviceName)
        {
            return $"{_options.PubSubChannelPrefix}{serviceName}";
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
                    Console.WriteLine($"SyZero.Redis: {operationName}失败: {ex.Message}");
                }
            });
        }

        private static Random GetRandom()
        {
            return RandomProvider.Value ?? new Random(Guid.NewGuid().GetHashCode());
        }

        /// <summary>
        /// 清除所有服务数据
        /// </summary>
        public async Task ClearAsync()
        {
            var serviceNames = await GetAllServices();
            foreach (var serviceName in serviceNames)
            {
                var key = GetServiceKey(serviceName);
                _redis.Del(key);
            }
            _redis.Del(_options.ServiceNamesKey);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 释放 Leader 权限
            if (_options.EnableLeaderElection)
            {
                ReleaseLeadership();
            }

            _leaderRenewTimer?.Dispose();
            _healthCheckTimer?.Dispose();
            _cleanupTimer?.Dispose();
            foreach (var disposable in _pubSubDisposables.Values)
            {
                disposable.Dispose();
            }
            _pubSubDisposables.Clear();
            _httpClient?.Dispose();
        }

        #endregion
    }
}
