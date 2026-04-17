using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Dependency;
using SyZero.Runtime.Security;

namespace SyZero.Service.LocalServiceManagement
{
    /// <summary>
    /// 本地服务管理实现（基于内存和本地文件）
    /// 适用于开发测试环境或单机部署场景
    /// </summary>
    public class LocalServiceManagement : IServiceManagement, IDisposable
    {
        private readonly ConcurrentDictionary<string, List<ServiceInfo>> _services = new ConcurrentDictionary<string, List<ServiceInfo>>();
        private readonly ConcurrentDictionary<string, Action<List<ServiceInfo>>> _subscriptions = new ConcurrentDictionary<string, Action<List<ServiceInfo>>>();
        private readonly ConcurrentDictionary<string, bool> _healthStatus = new ConcurrentDictionary<string, bool>();
        private static readonly ThreadLocal<Random> RandomProvider = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));
        private readonly LocalServiceManagementOptions _options;
        private readonly string _dataFilePath;
        private readonly string _leaderLockFilePath;
        private readonly string _instanceId;
        private readonly object _stateLock = new object();
        private readonly object _fileLock = new object();
        private readonly HttpClient _httpClient;
        private FileSystemWatcher _fileWatcher;
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

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">配置选项</param>
        public LocalServiceManagement(LocalServiceManagementOptions options = null)
        {
            _options = options ?? new LocalServiceManagementOptions();
            _dataFilePath = _options.GetDataFilePath();
            _instanceId = Guid.NewGuid().ToString("N");
            
            // Leader 锁文件路径
            var directory = Path.GetDirectoryName(_dataFilePath);
            _leaderLockFilePath = Path.Combine(directory ?? ".", "syzero_leader.lock");
            
            // 初始化 HttpClient 用于健康检查
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds)
            };
            
            if (_options.EnableFilePersistence)
            {
                LoadFromFile();
                
                if (_options.EnableFileWatcher)
                {
                    InitFileWatcher();
                }
            }

            // 如果启用 Leader 选举，先尝试获取 Leader
            if (_options.EnableLeaderElection)
            {
                TryAcquireLeadership();
                
                // 启动 Leader 续期定时器
                _leaderRenewTimer = new Timer(
                    _ => TryAcquireLeadership(),
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

        #region 服务查询

        /// <summary>
        /// 尝试获取 Leader 权限
        /// </summary>
        private void TryAcquireLeadership()
        {
            lock (_fileLock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_leaderLockFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // 检查是否存在有效的锁
                    if (File.Exists(_leaderLockFilePath))
                    {
                        try
                        {
                            var lockJson = File.ReadAllText(_leaderLockFilePath, Encoding.UTF8);
                            var lockInfo = JsonSerializer.Deserialize<LeaderLockInfo>(lockJson);

                            if (lockInfo != null)
                            {
                                // 如果是自己持有的锁，续期
                                if (lockInfo.InstanceId == _instanceId)
                                {
                                    lockInfo.ExpireTime = DateTime.UtcNow.AddSeconds(_options.LeaderLockExpireSeconds);
                                    lockInfo.RenewTime = DateTime.UtcNow;
                                    SaveLeaderLock(lockInfo);
                                    _isLeader = true;
                                    return;
                                }

                                // 检查锁是否过期
                                if (lockInfo.ExpireTime > DateTime.UtcNow)
                                {
                                    // 锁未过期，当前实例不是 Leader
                                    _isLeader = false;
                                    return;
                                }

                                // 锁已过期，可以抢占
                                Console.WriteLine($"SyZero.Local: Leader 锁已过期 (原 Leader: {lockInfo.InstanceId})，尝试获取...");
                            }
                        }
                        catch
                        {
                            // 锁文件损坏，删除后重新创建
                            File.Delete(_leaderLockFilePath);
                        }
                    }

                    // 创建新的锁
                    var newLock = new LeaderLockInfo
                    {
                        InstanceId = _instanceId,
                        AcquireTime = DateTime.UtcNow,
                        RenewTime = DateTime.UtcNow,
                        ExpireTime = DateTime.UtcNow.AddSeconds(_options.LeaderLockExpireSeconds)
                    };
                    SaveLeaderLock(newLock);
                    _isLeader = true;
                    Console.WriteLine($"SyZero.Local: 当前实例 [{_instanceId}] 成为 Leader");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.Local: 获取 Leader 权限失败: {ex.Message}");
                    _isLeader = false;
                }
            }
        }

        /// <summary>
        /// 保存 Leader 锁信息
        /// </summary>
        private void SaveLeaderLock(LeaderLockInfo lockInfo)
        {
            var json = JsonSerializer.Serialize(lockInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_leaderLockFilePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 释放 Leader 权限
        /// </summary>
        private void ReleaseLeadership()
        {
            if (!_isLeader) return;

            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(_leaderLockFilePath))
                    {
                        var lockJson = File.ReadAllText(_leaderLockFilePath, Encoding.UTF8);
                        var lockInfo = JsonSerializer.Deserialize<LeaderLockInfo>(lockJson);

                        // 只释放自己持有的锁
                        if (lockInfo != null && lockInfo.InstanceId == _instanceId)
                        {
                            File.Delete(_leaderLockFilePath);
                            Console.WriteLine($"SyZero.Local: 实例 [{_instanceId}] 释放 Leader 权限");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.Local: 释放 Leader 权限失败: {ex.Message}");
                }
                finally
                {
                    _isLeader = false;
                }
            }
        }

        #endregion

        #region 服务查询（原有）

        public Task<List<ServiceInfo>> GetService(string serviceName)
        {
            lock (_stateLock)
            {
                if (_services.TryGetValue(serviceName, out var services))
                {
                    return Task.FromResult(CloneServices(services));
                }
            }

            return Task.FromResult(new List<ServiceInfo>());
        }

        public Task<List<ServiceInfo>> GetHealthyServices(string serviceName)
        {
            lock (_stateLock)
            {
                if (_services.TryGetValue(serviceName, out var services))
                {
                    var healthyServices = services.Where(s =>
                        s.Enabled &&
                        s.IsHealthy &&
                        (!_healthStatus.TryGetValue(s.ServiceID, out var isHealthy) || isHealthy))
                        .Select(CloneServiceInfo)
                        .ToList();
                    return Task.FromResult(healthyServices);
                }
            }

            return Task.FromResult(new List<ServiceInfo>());
        }

        public async Task<ServiceInfo> GetServiceInstance(string serviceName)
        {
            var services = await GetHealthyServices(serviceName);
            if (services == null || services.Count == 0)
            {
                throw new Exception($"SyZero.Local:未找到可用的{serviceName}服务实例!");
            }
            // 加权随机负载均衡
            return SelectByWeight(services);
        }

        private static ServiceInfo SelectByWeight(List<ServiceInfo> services)
        {
            var totalWeight = services.Sum(s => s.Weight);
            if (totalWeight <= 0)
            {
                // 如果没有权重，使用简单随机
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

        public Task<List<string>> GetAllServices()
        {
            lock (_stateLock)
            {
                return Task.FromResult(_services.Keys.ToList());
            }
        }

        #endregion

        #region 服务注册/注销

        public Task RegisterService(ServiceInfo serviceInfo)
        {
            if (string.IsNullOrEmpty(serviceInfo.ServiceID))
            {
                serviceInfo.ServiceID = Guid.NewGuid().ToString("N");
            }

            // 设置默认值
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

            lock (_stateLock)
            {
                _services.AddOrUpdate(
                    serviceInfo.ServiceName,
                    _ => new List<ServiceInfo> { CloneServiceInfo(serviceInfo) },
                    (_, existingList) =>
                    {
                        var existing = existingList.FirstOrDefault(s => s.ServiceID == serviceInfo.ServiceID);
                        if (existing != null)
                        {
                            existingList.Remove(existing);
                        }

                        existingList.Add(CloneServiceInfo(serviceInfo));
                        return existingList;
                    });

                _healthStatus[serviceInfo.ServiceID] = serviceInfo.IsHealthy;
            }

            // 持久化到文件
            SaveToFile();

            // 通知订阅者
            NotifySubscribers(serviceInfo.ServiceName);

            return Task.CompletedTask;
        }

        public Task DeregisterService(string serviceId)
        {
            string changedServiceName = null;

            lock (_stateLock)
            {
                foreach (var kvp in _services)
                {
                    var service = kvp.Value.FirstOrDefault(s => s.ServiceID == serviceId);
                    if (service == null)
                    {
                        continue;
                    }

                    kvp.Value.Remove(service);
                    _healthStatus.TryRemove(serviceId, out _);

                    if (kvp.Value.Count == 0)
                    {
                        _services.TryRemove(kvp.Key, out _);
                    }

                    changedServiceName = kvp.Key;
                    break;
                }
            }

            if (changedServiceName == null)
            {
                return Task.CompletedTask;
            }

            SaveToFile();
            NotifySubscribers(changedServiceName);

            return Task.CompletedTask;
        }

        #endregion

        #region 健康检查

        public async Task<bool> IsServiceHealthy(string serviceName)
        {
            var healthyServices = await GetHealthyServices(serviceName);
            return healthyServices != null && healthyServices.Count > 0;
        }

        /// <summary>
        /// 设置服务实例的健康状态
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <param name="isHealthy">是否健康</param>
        public void SetServiceHealth(string serviceId, bool isHealthy)
        {
            string changedServiceName = null;
            lock (_stateLock)
            {
                _healthStatus[serviceId] = isHealthy;

                foreach (var kvp in _services)
                {
                    if (kvp.Value.Any(s => s.ServiceID == serviceId))
                    {
                        changedServiceName = kvp.Key;
                        break;
                    }
                }
            }

            if (changedServiceName != null)
            {
                NotifySubscribers(changedServiceName);
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

            var changedServices = new HashSet<string>();
            List<(string ServiceName, ServiceInfo Service)> servicesToCheck;

            lock (_stateLock)
            {
                servicesToCheck = _services
                    .SelectMany(kvp => kvp.Value.Select(service => (kvp.Key, CloneServiceInfo(service))))
                    .ToList();
            }

            foreach (var (serviceName, serviceSnapshot) in servicesToCheck)
            {
                var currentHealth = await CheckServiceHealthAsync(serviceSnapshot);

                lock (_stateLock)
                {
                    if (!_services.TryGetValue(serviceName, out var services))
                    {
                        continue;
                    }

                    var service = services.FirstOrDefault(item => item.ServiceID == serviceSnapshot.ServiceID);
                    if (service == null)
                    {
                        continue;
                    }

                    var previousHealth = service.IsHealthy;
                    _healthStatus[service.ServiceID] = currentHealth;

                    if (previousHealth != currentHealth)
                    {
                        service.IsHealthy = currentHealth;
                        changedServices.Add(serviceName);
                    }
                }
            }

            // 通知变更的服务
            foreach (var serviceName in changedServices)
            {
                NotifySubscribers(serviceName);
            }

            // 持久化到文件
            if (changedServices.Count > 0)
            {
                SaveToFile();
            }
        }

        /// <summary>
        /// 检查单个服务实例的健康状态
        /// </summary>
        private async Task<bool> CheckServiceHealthAsync(ServiceInfo service)
        {
            if (string.IsNullOrEmpty(service.HealthCheckUrl))
            {
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

        /// <summary>
        /// 更新服务心跳
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        public Task HeartbeatAsync(string serviceId)
        {
            lock (_stateLock)
            {
                foreach (var kvp in _services)
                {
                    var service = kvp.Value.FirstOrDefault(s => s.ServiceID == serviceId);
                    if (service == null)
                    {
                        continue;
                    }

                    service.LastHeartbeat = DateTime.UtcNow;
                    service.IsHealthy = true;
                    _healthStatus[serviceId] = true;
                    break;
                }
            }

            SaveToFile();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 清理过期服务
        /// </summary>
        private Task CleanExpiredServicesAsync()
        {
            // 如果启用了 Leader 选举且当前不是 Leader，则跳过清理
            if (_options.EnableLeaderElection && !_isLeader)
            {
                return Task.CompletedTask;
            }

            try
            {
                var changedServices = new HashSet<string>();
                var now = DateTime.UtcNow;

                lock (_stateLock)
                {
                    foreach (var kvp in _services.ToList())
                    {
                        var expiredServices = kvp.Value
                            .Where(s => s.LastHeartbeat.HasValue &&
                                        (now - s.LastHeartbeat.Value).TotalSeconds > _options.ServiceCleanSeconds)
                            .ToList();

                        foreach (var service in expiredServices)
                        {
                            kvp.Value.Remove(service);
                            _healthStatus.TryRemove(service.ServiceID, out _);
                            changedServices.Add(kvp.Key);
                            Console.WriteLine($"SyZero.Local: 自动清理过期服务 [{service.ServiceName}] ID={service.ServiceID}");
                        }

                        if (kvp.Value.Count == 0)
                        {
                            _services.TryRemove(kvp.Key, out _);
                        }
                    }
                }

                if (changedServices.Count > 0)
                {
                    SaveToFile();
                    foreach (var serviceName in changedServices)
                    {
                        NotifySubscribers(serviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.Local: 清理过期服务失败: {ex.Message}");
            }

            return Task.CompletedTask;
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

        private void NotifySubscribers(string serviceName)
        {
            if (_subscriptions.TryGetValue(serviceName, out var callback))
            {
                List<ServiceInfo> services;
                lock (_stateLock)
                {
                    services = _services.TryGetValue(serviceName, out var currentServices)
                        ? CloneServices(currentServices)
                        : new List<ServiceInfo>();
                }

                callback?.Invoke(services);
            }
        }

        #endregion

        #region 文件持久化

        private void SaveToFile()
        {
            if (!_options.EnableFilePersistence)
            {
                return;
            }

            lock (_fileLock)
            {
                try
                {
                    var data = new LocalServiceData
                    {
                        Services = SnapshotServices(),
                        HealthStatus = SnapshotHealthStatus(),
                        LastModified = DateTime.UtcNow
                    };

                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    var directory = Path.GetDirectoryName(_dataFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(_dataFilePath, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    // 记录错误但不抛出异常，避免影响主流程
                    Console.WriteLine($"SyZero.Local:保存服务数据到文件失败: {ex.Message}");
                }
            }
        }

        private void LoadFromFile()
        {
            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(_dataFilePath))
                    {
                        var json = File.ReadAllText(_dataFilePath, Encoding.UTF8);
                        var data = JsonSerializer.Deserialize<LocalServiceData>(json);

                        if (data != null)
                        {
                            lock (_stateLock)
                            {
                                _services.Clear();
                                foreach (var kvp in data.Services ?? new Dictionary<string, List<ServiceInfo>>())
                                {
                                    _services[kvp.Key] = CloneServices(kvp.Value ?? new List<ServiceInfo>());
                                }

                                _healthStatus.Clear();
                                foreach (var kvp in data.HealthStatus ?? new Dictionary<string, bool>())
                                {
                                    _healthStatus[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.Local:从文件加载服务数据失败: {ex.Message}");
                }
            }
        }

        private void InitFileWatcher()
        {
            try
            {
                var directory = Path.GetDirectoryName(_dataFilePath);
                var fileName = Path.GetFileName(_dataFilePath);

                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    _fileWatcher = new FileSystemWatcher(directory, fileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite
                    };

                    _fileWatcher.Changed += (sender, args) =>
                    {
                        // 延迟加载，避免文件占用
                        Thread.Sleep(100);
                        LoadFromFile();

                        // 通知所有订阅者
                        foreach (var serviceName in _subscriptions.Keys)
                        {
                            NotifySubscribers(serviceName);
                        }
                    };

                    _fileWatcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.Local:初始化文件监听器失败: {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 清除所有服务数据
        /// </summary>
        public void Clear()
        {
            lock (_stateLock)
            {
                _services.Clear();
                _healthStatus.Clear();
            }

            SaveToFile();

            // 通知所有订阅者
            foreach (var serviceName in _subscriptions.Keys.ToList())
            {
                NotifySubscribers(serviceName);
            }
        }

        /// <summary>
        /// 获取数据文件路径
        /// </summary>
        public string GetDataFilePath() => _dataFilePath;

        private static Random GetRandom()
        {
            return RandomProvider.Value ?? new Random(Guid.NewGuid().GetHashCode());
        }

        private Dictionary<string, List<ServiceInfo>> SnapshotServices()
        {
            lock (_stateLock)
            {
                return _services.ToDictionary(kvp => kvp.Key, kvp => CloneServices(kvp.Value));
            }
        }

        private Dictionary<string, bool> SnapshotHealthStatus()
        {
            lock (_stateLock)
            {
                return _healthStatus.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        private static List<ServiceInfo> CloneServices(IEnumerable<ServiceInfo> services)
        {
            return services.Select(CloneServiceInfo).ToList();
        }

        private static ServiceInfo CloneServiceInfo(ServiceInfo service)
        {
            return new ServiceInfo
            {
                ServiceID = service.ServiceID,
                ServiceName = service.ServiceName,
                ServiceAddress = service.ServiceAddress,
                ServicePort = service.ServicePort,
                ServiceProtocol = service.ServiceProtocol,
                Version = service.Version,
                Group = service.Group,
                Tags = service.Tags != null ? new List<string>(service.Tags) : new List<string>(),
                Metadata = service.Metadata != null ? new Dictionary<string, string>(service.Metadata) : new Dictionary<string, string>(),
                IsHealthy = service.IsHealthy,
                Enabled = service.Enabled,
                Weight = service.Weight,
                RegisterTime = service.RegisterTime,
                LastHeartbeat = service.LastHeartbeat,
                HealthCheckUrl = service.HealthCheckUrl,
                HealthCheckIntervalSeconds = service.HealthCheckIntervalSeconds,
                HealthCheckTimeoutSeconds = service.HealthCheckTimeoutSeconds,
                Region = service.Region,
                Zone = service.Zone
            };
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
                    Console.WriteLine($"SyZero.Local: {operationName}失败: {ex.Message}");
                }
            });
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
            _fileWatcher?.Dispose();
            _httpClient?.Dispose();
        }

        #endregion

        /// <summary>
        /// 本地服务数据结构（用于序列化）
        /// </summary>
        private class LocalServiceData
        {
            public Dictionary<string, List<ServiceInfo>> Services { get; set; } = new Dictionary<string, List<ServiceInfo>>();
            public Dictionary<string, bool> HealthStatus { get; set; } = new Dictionary<string, bool>();
            public DateTime LastModified { get; set; }
        }

        /// <summary>
        /// Leader 锁信息
        /// </summary>
        private class LeaderLockInfo
        {
            /// <summary>
            /// 持有锁的实例ID
            /// </summary>
            public string InstanceId { get; set; }

            /// <summary>
            /// 获取锁的时间
            /// </summary>
            public DateTime AcquireTime { get; set; }

            /// <summary>
            /// 最后续期时间
            /// </summary>
            public DateTime RenewTime { get; set; }

            /// <summary>
            /// 锁过期时间
            /// </summary>
            public DateTime ExpireTime { get; set; }
        }
    }
}
