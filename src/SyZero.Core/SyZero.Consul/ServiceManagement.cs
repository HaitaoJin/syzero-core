using NConsul;
using NConsul.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Cache;
using SyZero.Consul.Config;
using SyZero.Runtime.Security;
using SyZero.Service;
using SyZero.Util;

namespace SyZero.Consul
{
    public class ServiceManagement : IServiceManagement
    {
        private const string CacheKeyPrefix = "Consul:";
        private readonly IConsulClient _consulClient;
        private readonly ICache _cache;
        private readonly ConcurrentDictionary<string, Action<List<ServiceInfo>>> _subscriptions = new ConcurrentDictionary<string, Action<List<ServiceInfo>>>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _watchTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        private static readonly ThreadLocal<Random> RandomProvider = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        public ServiceManagement(IConsulClient consulClient,
            ICache cache)
        {
            _consulClient = consulClient;
            _cache = cache;
        }

        #region 服务查询

        public async Task<List<ServiceInfo>> GetService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("服务名称不能为空。", nameof(serviceName));
            }

            var cacheKey = GetCacheKey(serviceName);
            if (_cache.Exist(cacheKey))
            {
                var cachedServices = _cache.Get<List<ServiceInfo>>(cacheKey);
                if (cachedServices != null)
                {
                    return CloneServices(cachedServices);
                }

                _cache.Remove(cacheKey);
            }

            var services = await _consulClient.Catalog.Service(serviceName);
            if (services.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"SyZero.Consul:Consul连接出错({services.StatusCode})!");
            }
            if (services.Response.Length == 0)
            {
                throw new Exception($"SyZero.Consul:未找到{serviceName}服务!");
            }

            var serviceInfos = services.Response.Select(MapCatalogService).ToList();
            _cache.Set(cacheKey, serviceInfos, 30);
            return CloneServices(serviceInfos);
        }

        public async Task<List<ServiceInfo>> GetHealthyServices(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("服务名称不能为空。", nameof(serviceName));
            }

            var healthChecks = await _consulClient.Health.Service(serviceName, string.Empty, true);
            if (healthChecks.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"SyZero.Consul:Consul连接出错({healthChecks.StatusCode})!");
            }
            if (healthChecks.Response.Length == 0)
            {
                return new List<ServiceInfo>();
            }

            return healthChecks.Response.Select(MapServiceEntry).ToList();
        }

        public async Task<ServiceInfo> GetServiceInstance(string serviceName)
        {
            var services = await GetHealthyServices(serviceName);
            if (services == null || services.Count == 0)
            {
                throw new Exception($"SyZero.Consul:未找到可用的{serviceName}服务实例!");
            }

            return SelectByWeight(services);
        }

        public async Task<List<string>> GetAllServices()
        {
            var services = await _consulClient.Catalog.Services();
            if (services.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"SyZero.Consul:Consul连接出错({services.StatusCode})!");
            }
            return services.Response.Keys.ToList();
        }

        #endregion

        #region 服务注册/注销

        public async Task RegisterService(ServiceInfo serviceInfo)
        {
            if (serviceInfo == null)
            {
                throw new ArgumentNullException(nameof(serviceInfo));
            }
            if (string.IsNullOrWhiteSpace(serviceInfo.ServiceName))
            {
                throw new ArgumentException("服务名称不能为空。", nameof(serviceInfo));
            }
            if (string.IsNullOrWhiteSpace(serviceInfo.ServiceAddress))
            {
                throw new ArgumentException("服务地址不能为空。", nameof(serviceInfo));
            }
            if (serviceInfo.ServicePort <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serviceInfo), "服务端口必须大于 0。");
            }

            var serviceId = string.IsNullOrWhiteSpace(serviceInfo.ServiceID)
                ? Guid.NewGuid().ToString("N")
                : serviceInfo.ServiceID;
            var meta = serviceInfo.Metadata != null
                ? new Dictionary<string, string>(serviceInfo.Metadata)
                : new Dictionary<string, string>();

            meta["Protocol"] = serviceInfo.ServiceProtocol.ToString();
            if (!string.IsNullOrEmpty(serviceInfo.Version))
                meta["Version"] = serviceInfo.Version;
            if (!string.IsNullOrEmpty(serviceInfo.Group))
                meta["Group"] = serviceInfo.Group;
            if (!string.IsNullOrEmpty(serviceInfo.Region))
                meta["Region"] = serviceInfo.Region;
            if (!string.IsNullOrEmpty(serviceInfo.Zone))
                meta["Zone"] = serviceInfo.Zone;
            meta["Weight"] = GetNormalizedWeight(serviceInfo.Weight).ToString(CultureInfo.InvariantCulture);
            meta["RegisterTime"] = (serviceInfo.RegisterTime != default ? serviceInfo.RegisterTime : DateTime.UtcNow).ToString("O", CultureInfo.InvariantCulture);

            var healthCheckInterval = serviceInfo.HealthCheckIntervalSeconds > 0 
                ? serviceInfo.HealthCheckIntervalSeconds 
                : 10;
            var healthCheckTimeout = serviceInfo.HealthCheckTimeoutSeconds > 0 
                ? serviceInfo.HealthCheckTimeoutSeconds 
                : 5;
            var healthCheckUrl = !string.IsNullOrWhiteSpace(serviceInfo.HealthCheckUrl)
                ? serviceInfo.HealthCheckUrl
                : BuildHealthCheckUrl(serviceInfo.ServiceProtocol, serviceInfo.ServiceAddress, serviceInfo.ServicePort);

            meta["HealthCheckUrl"] = healthCheckUrl;
            meta["HealthCheckIntervalSeconds"] = healthCheckInterval.ToString(CultureInfo.InvariantCulture);
            meta["HealthCheckTimeoutSeconds"] = healthCheckTimeout.ToString(CultureInfo.InvariantCulture);

            var check = new AgentServiceCheck
            {
                Interval = TimeSpan.FromSeconds(healthCheckInterval),
                Timeout = TimeSpan.FromSeconds(healthCheckTimeout),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            };

            if (serviceInfo.ServiceProtocol == ProtocolType.GRPC)
            {
                check.GRPC = healthCheckUrl;
            }
            else
            {
                check.HTTP = healthCheckUrl;
            }

            var registration = new AgentServiceRegistration
            {
                ID = serviceId,
                Name = serviceInfo.ServiceName,
                Address = serviceInfo.ServiceAddress,
                Port = serviceInfo.ServicePort,
                Tags = serviceInfo.Tags?.ToArray() ?? Array.Empty<string>(),
                Meta = meta,
                Check = check
            };
            var result = await _consulClient.Agent.ServiceRegister(registration);
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"SyZero.Consul:服务注册失败({result.StatusCode})!");
            }

            InvalidateServiceCache(serviceInfo.ServiceName);
        }

        public async Task DeregisterService(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                throw new ArgumentException("服务 ID 不能为空。", nameof(serviceId));
            }

            var result = await _consulClient.Agent.ServiceDeregister(serviceId);
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"SyZero.Consul:服务注销失败({result.StatusCode})!");
            }

            InvalidateServiceCache();
        }

        #endregion

        #region 健康检查

        public async Task<bool> IsServiceHealthy(string serviceName)
        {
            var healthyServices = await GetHealthyServices(serviceName);
            return healthyServices != null && healthyServices.Count > 0;
        }

        #endregion

        #region 服务订阅

        public Task Subscribe(string serviceName, Action<List<ServiceInfo>> callback)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("服务名称不能为空。", nameof(serviceName));
            }
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            _subscriptions[serviceName] = callback;

            if (_watchTokens.TryRemove(serviceName, out var previousToken))
            {
                previousToken.Cancel();
                previousToken.Dispose();
            }

            var cts = new CancellationTokenSource();
            _watchTokens[serviceName] = cts;

            // 启动后台监听任务
            _ = WatchServiceAsync(serviceName, cts.Token);
            
            return Task.CompletedTask;
        }

        public Task Unsubscribe(string serviceName)
        {
            _subscriptions.TryRemove(serviceName, out _);
            
            if (_watchTokens.TryRemove(serviceName, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            
            return Task.CompletedTask;
        }

        private async Task WatchServiceAsync(string serviceName, CancellationToken cancellationToken)
        {
            ulong lastIndex = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _consulClient.Health.Service(serviceName, string.Empty, true, new QueryOptions
                    {
                        WaitIndex = lastIndex,
                        WaitTime = TimeSpan.FromMinutes(5)
                    }, cancellationToken);

                    if (result.LastIndex != lastIndex)
                    {
                        lastIndex = result.LastIndex;

                        if (_subscriptions.TryGetValue(serviceName, out var callback))
                        {
                            InvalidateServiceCache(serviceName);
                            var services = result.Response.Select(MapServiceEntry).ToList();

                            callback?.Invoke(services);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private static ServiceInfo SelectByWeight(List<ServiceInfo> services)
        {
            var weightedServices = services.Where(service => service.Weight > 0).ToList();
            if (weightedServices.Count == 0)
            {
                return services[GetRandom().Next(services.Count)];
            }

            var totalWeight = weightedServices.Sum(service => service.Weight);
            var randomWeight = GetRandom().NextDouble() * totalWeight;
            var currentWeight = 0.0;
            foreach (var service in weightedServices)
            {
                currentWeight += service.Weight;
                if (randomWeight <= currentWeight)
                {
                    return service;
                }
            }

            return weightedServices.Last();
        }

        private static ServiceInfo MapCatalogService(CatalogService service)
        {
            var metadata = service.ServiceMeta != null
                ? new Dictionary<string, string>(service.ServiceMeta)
                : new Dictionary<string, string>();
            var address = GetServiceAddress(service.ServiceAddress, service.Address);

            return new ServiceInfo
            {
                ServiceID = service.ServiceID,
                ServiceName = service.ServiceName,
                ServiceAddress = address,
                ServicePort = service.ServicePort,
                ServiceProtocol = GetProtocol(metadata),
                Version = GetMetadataValue(metadata, "Version"),
                Group = GetMetadataValue(metadata, "Group"),
                Tags = service.ServiceTags?.ToList() ?? new List<string>(),
                Metadata = metadata,
                IsHealthy = true,
                Enabled = true,
                Weight = GetWeight(metadata),
                Region = GetMetadataValue(metadata, "Region"),
                Zone = GetMetadataValue(metadata, "Zone"),
                HealthCheckUrl = GetHealthCheckUrl(metadata, GetProtocol(metadata), address, service.ServicePort)
            };
        }

        private static ServiceInfo MapServiceEntry(ServiceEntry entry)
        {
            var service = entry.Service;
            var metadata = service.Meta != null
                ? new Dictionary<string, string>(service.Meta)
                : new Dictionary<string, string>();
            var address = GetServiceAddress(service.Address, entry.Node?.Address);
            var protocol = GetProtocol(metadata);

            return new ServiceInfo
            {
                ServiceID = service.ID,
                ServiceName = service.Service,
                ServiceAddress = address,
                ServicePort = service.Port,
                ServiceProtocol = protocol,
                Version = GetMetadataValue(metadata, "Version"),
                Group = GetMetadataValue(metadata, "Group"),
                Tags = service.Tags?.ToList() ?? new List<string>(),
                Metadata = metadata,
                IsHealthy = true,
                Enabled = true,
                Weight = GetWeight(metadata),
                Region = GetMetadataValue(metadata, "Region"),
                Zone = GetMetadataValue(metadata, "Zone"),
                HealthCheckUrl = GetHealthCheckUrl(metadata, protocol, address, service.Port),
                HealthCheckIntervalSeconds = GetIntValue(metadata, "HealthCheckIntervalSeconds", 10),
                HealthCheckTimeoutSeconds = GetIntValue(metadata, "HealthCheckTimeoutSeconds", 5)
            };
        }

        private static List<ServiceInfo> CloneServices(List<ServiceInfo> services)
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

        private void InvalidateServiceCache(string serviceName = null)
        {
            if (!string.IsNullOrWhiteSpace(serviceName))
            {
                _cache.Remove(GetCacheKey(serviceName));
                return;
            }

            var cacheKeys = _cache.GetKeys($"{CacheKeyPrefix}*") ?? Array.Empty<string>();
            foreach (var key in cacheKeys.Where(key => key.StartsWith(CacheKeyPrefix, StringComparison.Ordinal)))
            {
                _cache.Remove(key);
            }
        }

        private static string GetCacheKey(string serviceName)
        {
            return $"{CacheKeyPrefix}{serviceName}";
        }

        private static string GetServiceAddress(string primaryAddress, string fallbackAddress)
        {
            return string.IsNullOrWhiteSpace(primaryAddress) ? fallbackAddress : primaryAddress;
        }

        private static ProtocolType GetProtocol(IDictionary<string, string> metadata)
        {
            var protocolValue = GetMetadataValue(metadata, "Protocol");
            if (!string.IsNullOrWhiteSpace(protocolValue) && Enum.TryParse(protocolValue, true, out ProtocolType protocol))
            {
                return protocol;
            }

            return ProtocolType.HTTP;
        }

        private static string GetMetadataValue(IDictionary<string, string> metadata, string key)
        {
            if (metadata != null && metadata.TryGetValue(key, out var value))
            {
                return value;
            }

            return null;
        }

        private static double GetWeight(IDictionary<string, string> metadata)
        {
            var weightValue = GetMetadataValue(metadata, "Weight");
            if (double.TryParse(weightValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var weight))
            {
                return GetNormalizedWeight(weight);
            }

            return 1.0;
        }

        private static double GetNormalizedWeight(double weight)
        {
            return weight > 0 ? weight : 1.0;
        }

        private static int GetIntValue(IDictionary<string, string> metadata, string key, int defaultValue)
        {
            var value = GetMetadataValue(metadata, key);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : defaultValue;
        }

        private static string GetHealthCheckUrl(IDictionary<string, string> metadata, ProtocolType protocol, string serviceAddress, int servicePort)
        {
            var configuredHealthCheck = GetMetadataValue(metadata, "HealthCheckUrl");
            if (!string.IsNullOrWhiteSpace(configuredHealthCheck))
            {
                return configuredHealthCheck;
            }

            return BuildHealthCheckUrl(protocol, serviceAddress, servicePort);
        }

        private static string BuildHealthCheckUrl(ProtocolType protocol, string serviceAddress, int servicePort)
        {
            if (string.IsNullOrWhiteSpace(serviceAddress) || servicePort <= 0)
            {
                return null;
            }

            if (protocol == ProtocolType.GRPC)
            {
                return $"{serviceAddress}:{servicePort}";
            }

            var scheme = protocol == ProtocolType.HTTPS ? "https" : "http";
            return $"{scheme}://{serviceAddress}:{servicePort}/health";
        }

        private static Random GetRandom()
        {
            return RandomProvider.Value ?? new Random(Guid.NewGuid().GetHashCode());
        }

        #endregion
    }
}
