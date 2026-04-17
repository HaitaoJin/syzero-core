using Grpc.Net.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SyZero.Serialization;

namespace SyZero.Feign.Proxy
{
    /// <summary>
    /// gRPC 协议代理工厂
    /// </summary>
    public class GrpcProxyFactory : IFeignProxyFactory
    {
        /// <summary>
        /// gRPC 通道缓存（按端点地址缓存）
        /// </summary>
        private static readonly ConcurrentDictionary<string, GrpcChannel> _channelCache = new ConcurrentDictionary<string, GrpcChannel>();

        /// <summary>
        /// 支持的协议类型
        /// </summary>
        public FeignProtocol Protocol => FeignProtocol.Grpc;

        /// <summary>
        /// 创建 gRPC 代理实例
        /// </summary>
        public object CreateProxy(Type targetType, string endPoint, FeignService feignService, IJsonSerialize jsonSerialize)
        {
            try
            {
                // 获取或创建 gRPC 通道（复用通道）
                var channel = GetOrCreateChannel(endPoint, feignService);

                // 获取 gRPC 客户端类型
                var clientType = GetGrpcClientType(targetType);
                if (clientType == null)
                {
                    throw new InvalidOperationException($"未找到接口 {targetType.Name} 对应的 gRPC 客户端类型");
                }

                // 创建 gRPC 客户端实例
                var client = Activator.CreateInstance(clientType, channel);
                return client;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"gRPC 代理创建失败: EndPoint({endPoint}) {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取或创建 gRPC 通道
        /// </summary>
        private GrpcChannel GetOrCreateChannel(string endPoint, FeignService feignService)
        {
            var cacheKey = $"{endPoint}_{feignService.EnableSsl}_{feignService.MaxMessageSize}";
            
            return _channelCache.GetOrAdd(cacheKey, _ =>
            {
                var channelOptions = CreateChannelOptions(feignService);
                return GrpcChannel.ForAddress(endPoint, channelOptions);
            });
        }

        /// <summary>
        /// 创建 gRPC 通道选项
        /// </summary>
        private GrpcChannelOptions CreateChannelOptions(FeignService feignService)
        {
            var options = new GrpcChannelOptions
            {
                // 设置最大消息大小
                MaxReceiveMessageSize = feignService.MaxMessageSize > 0 ? (int?)feignService.MaxMessageSize : null,
                MaxSendMessageSize = feignService.MaxMessageSize > 0 ? (int?)feignService.MaxMessageSize : null,
            };

            // 非 SSL 时需要特殊处理
            if (!feignService.EnableSsl)
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                var handler = new System.Net.Http.SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true,
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                    };
                options.HttpHandler = handler;
            }

            return options;
        }

        /// <summary>
        /// 获取 gRPC 客户端类型
        /// </summary>
        /// <remarks>
        /// 查找规则：
        /// 1. 接口 IXxxService 对应客户端 XxxService.XxxServiceClient
        /// 2. 接口 IXxx 对应客户端 Xxx.XxxClient
        /// </remarks>
        private Type GetGrpcClientType(Type interfaceType)
        {
            var assembly = interfaceType.Assembly;
            var interfaceName = interfaceType.Name;

            // 移除接口前缀 "I"
            var serviceName = interfaceName.StartsWith("I") ? interfaceName.Substring(1) : interfaceName;

            // 尝试查找客户端类型
            // 格式1: ServiceName.ServiceNameClient (标准 gRPC 生成格式)
            var clientTypeName1 = $"{interfaceType.Namespace}.{serviceName}+{serviceName}Client";
            var clientType = assembly.GetType(clientTypeName1);
            if (clientType != null) return clientType;

            // 格式2: ServiceNameClient
            var clientTypeName2 = $"{interfaceType.Namespace}.{serviceName}Client";
            clientType = assembly.GetType(clientTypeName2);
            if (clientType != null) return clientType;

            // 格式3: 遍历程序集查找匹配的客户端类型
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type.Name == $"{serviceName}Client" || type.Name.EndsWith($".{serviceName}Client"))
                {
                    return type;
                }
            }

            // 在所有已加载的程序集中查找
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in GetLoadableTypes(asm))
                    {
                        if (type.Name == $"{serviceName}Client" && type.IsClass && !type.IsAbstract)
                        {
                            return type;
                        }
                    }
                }
                catch
                {
                    // 忽略无法加载的程序集
                }
            }

            return null;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }

        /// <summary>
        /// 清除通道缓存
        /// </summary>
        public static void ClearChannelCache()
        {
            foreach (var channel in _channelCache.Values)
            {
                try
                {
                    channel.Dispose();
                }
                catch
                {
                    // 忽略释放异常
                }
            }
            _channelCache.Clear();
        }
    }
}
