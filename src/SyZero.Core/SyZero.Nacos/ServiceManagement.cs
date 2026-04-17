using Nacos.V2;
using Nacos.V2.Naming.Dtos;
using Nacos.V2.Naming.Event;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SyZero.Cache;
using SyZero.Runtime.Security;
using SyZero.Service;
using ServiceInfo = SyZero.Service.ServiceInfo;

namespace SyZero.Nacos
{
    public class ServiceManagement : IServiceManagement
    {
        private readonly INacosNamingService _nacosNamingService;
        private readonly ICache _cache;
        private readonly ConcurrentDictionary<string, ServiceChangeListener> _listeners = new ConcurrentDictionary<string, ServiceChangeListener>();
        private readonly ConcurrentDictionary<string, RegisteredInstanceLocator> _serviceLocators = new ConcurrentDictionary<string, RegisteredInstanceLocator>();
        private const string CachePrefix = "Nacos:";

        public ServiceManagement(INacosNamingService nacosNamingService, ICache cache)
        {
            _nacosNamingService = nacosNamingService;
            _cache = cache;
        }

        #region 服务查询

        public async Task<List<ServiceInfo>> GetService(string serviceName)
        {
            var cacheKey = GetCacheKey(serviceName);
            if (_cache.Exist(cacheKey))
            {
                return CloneServices(_cache.Get<List<ServiceInfo>>(cacheKey));
            }

            var services = await _nacosNamingService.GetAllInstances(serviceName);
            if (services == null || services.Count == 0)
            {
                throw new Exception($"SyZero.Nacos:未找到{serviceName}服务!");
            }

            var serviceInfos = TrackAndCloneServices(services.Select(MapToServiceInfo).ToList());
            _cache.Set(cacheKey, serviceInfos, 30);
            return CloneServices(serviceInfos);
        }

        public async Task<List<ServiceInfo>> GetHealthyServices(string serviceName)
        {
            var services = await _nacosNamingService.SelectInstances(serviceName, true);
            if (services == null || services.Count == 0)
            {
                return new List<ServiceInfo>();
            }

            return TrackAndCloneServices(services.Select(MapToServiceInfo).ToList());
        }

        public async Task<ServiceInfo> GetServiceInstance(string serviceName)
        {
            var service = await _nacosNamingService.SelectOneHealthyInstance(serviceName);
            if (service == null)
            {
                throw new Exception($"SyZero.Nacos:未找到可用的{serviceName}服务实例!");
            }

            return TrackAndClone(MapToServiceInfo(service));
        }

        public async Task<List<string>> GetAllServices()
        {
            var servicesInfo = await _nacosNamingService.GetServicesOfServer(1, int.MaxValue);
            return servicesInfo?.Data?.ToList() ?? new List<string>();
        }

        #endregion

        #region 服务注册/注销

        public async Task RegisterService(ServiceInfo serviceInfo)
        {
            var metadata = serviceInfo.Metadata != null
                ? new Dictionary<string, string>(serviceInfo.Metadata)
                : new Dictionary<string, string>();
            metadata["Protocol"] = serviceInfo.ServiceProtocol.ToString();
            metadata["secure"] = serviceInfo.ServiceProtocol == ProtocolType.HTTPS ? "true" : "false";
            if (!string.IsNullOrEmpty(serviceInfo.Version))
                metadata["Version"] = serviceInfo.Version;
            if (!string.IsNullOrEmpty(serviceInfo.Group))
                metadata["Group"] = serviceInfo.Group;
            if (!string.IsNullOrEmpty(serviceInfo.Region))
                metadata["Region"] = serviceInfo.Region;
            if (!string.IsNullOrEmpty(serviceInfo.Zone))
                metadata["Zone"] = serviceInfo.Zone;
            if (!string.IsNullOrEmpty(serviceInfo.HealthCheckUrl))
                metadata["HealthCheckUrl"] = serviceInfo.HealthCheckUrl;
            if (serviceInfo.HealthCheckIntervalSeconds > 0)
                metadata["HealthCheckIntervalSeconds"] = serviceInfo.HealthCheckIntervalSeconds.ToString();
            if (serviceInfo.HealthCheckTimeoutSeconds > 0)
                metadata["HealthCheckTimeoutSeconds"] = serviceInfo.HealthCheckTimeoutSeconds.ToString();
            metadata["RegisterTime"] = (serviceInfo.RegisterTime != default ? serviceInfo.RegisterTime : DateTime.UtcNow).ToString("O");

            var instance = new Instance
            {
                InstanceId = serviceInfo.ServiceID,
                ServiceName = serviceInfo.ServiceName,
                Ip = serviceInfo.ServiceAddress,
                Port = serviceInfo.ServicePort,
                Healthy = serviceInfo.IsHealthy,
                Enabled = serviceInfo.Enabled,
                Weight = serviceInfo.Weight > 0 ? serviceInfo.Weight : 1.0,
                Metadata = metadata,
                ClusterName = serviceInfo.Group
            };
            await _nacosNamingService.RegisterInstance(serviceInfo.ServiceName, instance);

            TrackService(new ServiceInfo
            {
                ServiceID = serviceInfo.ServiceID,
                ServiceName = serviceInfo.ServiceName,
                ServiceAddress = serviceInfo.ServiceAddress,
                ServicePort = serviceInfo.ServicePort,
                ServiceProtocol = serviceInfo.ServiceProtocol,
                Version = serviceInfo.Version,
                Group = serviceInfo.Group,
                Tags = serviceInfo.Tags != null ? new List<string>(serviceInfo.Tags) : new List<string>(),
                Metadata = metadata,
                IsHealthy = serviceInfo.IsHealthy,
                Enabled = serviceInfo.Enabled,
                Weight = serviceInfo.Weight > 0 ? serviceInfo.Weight : 1.0,
                RegisterTime = serviceInfo.RegisterTime != default ? serviceInfo.RegisterTime : DateTime.UtcNow,
                LastHeartbeat = serviceInfo.LastHeartbeat,
                HealthCheckUrl = serviceInfo.HealthCheckUrl,
                HealthCheckIntervalSeconds = serviceInfo.HealthCheckIntervalSeconds,
                HealthCheckTimeoutSeconds = serviceInfo.HealthCheckTimeoutSeconds,
                Region = serviceInfo.Region,
                Zone = serviceInfo.Zone
            });
            InvalidateCache(serviceInfo.ServiceName);
        }

        public async Task DeregisterService(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                throw new ArgumentException("serviceId不能为空。", nameof(serviceId));
            }

            if (TryParseCompositeServiceId(serviceId, out var serviceName, out var ip, out var port))
            {
                await _nacosNamingService.DeregisterInstance(serviceName, ip, port);
                RemoveTrackedService(serviceId, serviceName);
                return;
            }

            if (_serviceLocators.TryGetValue(serviceId, out var locator))
            {
                await _nacosNamingService.DeregisterInstance(locator.ServiceName, new Instance
                {
                    InstanceId = serviceId,
                    ServiceName = locator.ServiceName,
                    Ip = locator.Ip,
                    Port = locator.Port
                });
                RemoveTrackedService(serviceId, locator.ServiceName);
                return;
            }

            var resolved = await FindRegisteredInstance(serviceId);
            if (resolved != null)
            {
                await _nacosNamingService.DeregisterInstance(resolved.ServiceName, resolved.Instance);
                RemoveTrackedService(serviceId, resolved.ServiceName);
                return;
            }

            throw new ArgumentException($"无法根据serviceId找到对应的Nacos实例: {serviceId}", nameof(serviceId));
        }

        #endregion

        #region 健康检查

        public async Task<bool> IsServiceHealthy(string serviceName)
        {
            var healthyServices = await GetHealthyServices(serviceName);
            return healthyServices.Count > 0;
        }

        #endregion

        #region 服务订阅

        public async Task Subscribe(string serviceName, Action<List<ServiceInfo>> callback)
        {
            if (_listeners.TryRemove(serviceName, out var existingListener))
            {
                await _nacosNamingService.Unsubscribe(serviceName, existingListener);
            }

            var listener = new ServiceChangeListener(services =>
            {
                var trackedServices = TrackAndCloneServices(services);
                InvalidateCache(serviceName);
                callback?.Invoke(trackedServices);
            });

            _listeners[serviceName] = listener;
            await _nacosNamingService.Subscribe(serviceName, listener);
        }

        public async Task Unsubscribe(string serviceName)
        {
            if (_listeners.TryRemove(serviceName, out var listener))
            {
                await _nacosNamingService.Unsubscribe(serviceName, listener);
            }
        }

        #endregion

        #region 辅助方法

        private static ServiceInfo MapToServiceInfo(Instance instance)
        {
            var metadata = instance.Metadata != null
                ? new Dictionary<string, string>(instance.Metadata)
                : new Dictionary<string, string>();
            var protocol = ProtocolType.HTTP;
            if (metadata.TryGetValue("Protocol", out var protocolValue) &&
                Enum.TryParse(protocolValue, true, out ProtocolType parsedProtocol))
            {
                protocol = parsedProtocol;
            }
            else if (metadata.TryGetValue("secure", out var secure) &&
                     string.Equals(secure, "true", StringComparison.OrdinalIgnoreCase))
            {
                protocol = ProtocolType.HTTPS;
            }

            metadata.TryGetValue("Version", out var version);
            metadata.TryGetValue("Group", out var group);
            metadata.TryGetValue("Region", out var region);
            metadata.TryGetValue("Zone", out var zone);
            metadata.TryGetValue("HealthCheckUrl", out var healthCheckUrl);
            int.TryParse(metadata.TryGetValue("HealthCheckIntervalSeconds", out var intervalStr) ? intervalStr : "10", out var healthCheckInterval);
            int.TryParse(metadata.TryGetValue("HealthCheckTimeoutSeconds", out var timeoutStr) ? timeoutStr : "5", out var healthCheckTimeout);
            DateTime.TryParse(metadata.TryGetValue("RegisterTime", out var registerTimeStr) ? registerTimeStr : null, out var registerTime);

            return new ServiceInfo
            {
                ServiceID = instance.InstanceId,
                ServiceName = instance.ServiceName,
                ServiceAddress = instance.Ip,
                ServicePort = instance.Port,
                ServiceProtocol = protocol,
                Version = version,
                Group = group ?? instance.ClusterName,
                Metadata = metadata,
                IsHealthy = instance.Healthy,
                Enabled = instance.Enabled,
                Weight = instance.Weight > 0 ? instance.Weight : 1.0,
                RegisterTime = registerTime != default ? registerTime : DateTime.UtcNow,
                Region = region,
                Zone = zone,
                HealthCheckUrl = healthCheckUrl,
                HealthCheckIntervalSeconds = healthCheckInterval > 0 ? healthCheckInterval : 10,
                HealthCheckTimeoutSeconds = healthCheckTimeout > 0 ? healthCheckTimeout : 5
            };
        }

        private static string GetCacheKey(string serviceName)
        {
            return $"{CachePrefix}{serviceName}";
        }

        private void InvalidateCache(string serviceName)
        {
            _cache.Remove(GetCacheKey(serviceName));
        }

        private List<ServiceInfo> TrackAndCloneServices(List<ServiceInfo> services)
        {
            foreach (var service in services)
            {
                TrackService(service);
            }

            return CloneServices(services);
        }

        private ServiceInfo TrackAndClone(ServiceInfo service)
        {
            TrackService(service);
            return CloneService(service);
        }

        private void TrackService(ServiceInfo service)
        {
            if (string.IsNullOrWhiteSpace(service.ServiceID) ||
                string.IsNullOrWhiteSpace(service.ServiceName) ||
                string.IsNullOrWhiteSpace(service.ServiceAddress) ||
                service.ServicePort <= 0)
            {
                return;
            }

            _serviceLocators[service.ServiceID] = new RegisteredInstanceLocator(service.ServiceName, service.ServiceAddress, service.ServicePort);
        }

        private void RemoveTrackedService(string serviceId, string serviceName)
        {
            _serviceLocators.TryRemove(serviceId, out _);
            InvalidateCache(serviceName);
        }

        private async Task<ResolvedInstance> FindRegisteredInstance(string serviceId)
        {
            var serviceNames = await GetAllServices();
            foreach (var serviceName in serviceNames)
            {
                var instances = await _nacosNamingService.GetAllInstances(serviceName);
                var instance = instances?.FirstOrDefault(item => string.Equals(item.InstanceId, serviceId, StringComparison.Ordinal));
                if (instance != null)
                {
                    return new ResolvedInstance(serviceName, instance);
                }
            }

            return null;
        }

        private static bool TryParseCompositeServiceId(string serviceId, out string serviceName, out string ip, out int port)
        {
            serviceName = string.Empty;
            ip = string.Empty;
            port = 0;

            var parts = serviceId.Split('#', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[2], out port))
            {
                return false;
            }

            serviceName = parts[0];
            ip = parts[1];
            return !string.IsNullOrWhiteSpace(serviceName) && !string.IsNullOrWhiteSpace(ip);
        }

        private static List<ServiceInfo> CloneServices(IEnumerable<ServiceInfo> services)
        {
            return services?.Select(CloneService).ToList() ?? new List<ServiceInfo>();
        }

        private static ServiceInfo CloneService(ServiceInfo service)
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

        #endregion

        /// <summary>
        /// 服务变更监听器
        /// </summary>
        private class ServiceChangeListener : IEventListener
        {
            private readonly Action<List<ServiceInfo>> _callback;

            public ServiceChangeListener(Action<List<ServiceInfo>> callback)
            {
                _callback = callback;
            }

            public Task OnEvent(IEvent @event)
            {
                if (@event is InstancesChangeEvent changeEvent)
                {
                    var services = changeEvent.Hosts?.Select(instance => MapToServiceInfo(instance)).ToList() ?? new List<ServiceInfo>();
                    _callback?.Invoke(services);
                }

                return Task.CompletedTask;
            }
        }

        private sealed record RegisteredInstanceLocator(string ServiceName, string Ip, int Port);

        private sealed record ResolvedInstance(string ServiceName, Instance Instance);
    }
}
