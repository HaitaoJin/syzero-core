using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.Server;
using SyZero.Application.Service;
using SyZero.DynamicGrpc.Helpers;

namespace SyZero.DynamicGrpc
{
    /// <summary>
    /// Dynamic gRPC 服务扩展方法
    /// </summary>
    public static class DynamicGrpcServiceExtensions
    {
        /// <summary>
        /// 缓存 MapGrpcService 方法
        /// </summary>
        private static MethodInfo _mapGrpcServiceMethod;

        /// <summary>
        /// 添加 Dynamic gRPC 服务到依赖注入容器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">配置选项</param>
        /// <returns>服务集合</returns>
        /// <exception cref="ArgumentNullException">options 为 null 时抛出</exception>
        public static IServiceCollection AddDynamicGrpc(this IServiceCollection services, DynamicGrpcOptions options)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();

            // 注册选项
            services.AddSingleton(options);

            // 注册服务类型提供程序
            services.AddSingleton<DynamicGrpcServiceTypeProvider>();

            // 配置 Code-First gRPC (protobuf-net.Grpc)
            services.AddCodeFirstGrpc(config =>
            {
                if (options.MaxReceiveMessageSize.HasValue)
                {
                    config.MaxReceiveMessageSize = options.MaxReceiveMessageSize.Value;
                }
                if (options.MaxSendMessageSize.HasValue)
                {
                    config.MaxSendMessageSize = options.MaxSendMessageSize.Value;
                }
                config.EnableDetailedErrors = options.EnableDetailedErrors;
            });

            // 自动注册所有动态 gRPC 服务
            RegisterDynamicGrpcServices(services, options);

            return services;
        }

        /// <summary>
        /// 添加指定程序集中的 Dynamic gRPC 服务到依赖注入容器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="assembly">主程序集</param>
        /// <param name="additionalAssemblies">额外程序集</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddDynamicGrpc(this IServiceCollection services, Assembly assembly, params Assembly[] additionalAssemblies)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var options = new DynamicGrpcOptions();
            options.AddAssemblyOptions(assembly);

            foreach (var additionalAssembly in additionalAssemblies ?? Array.Empty<Assembly>())
            {
                if (additionalAssembly != null)
                {
                    options.AddAssemblyOptions(additionalAssembly);
                }
            }

            return AddDynamicGrpc(services, options);
        }

        /// <summary>
        /// 添加 Dynamic gRPC 服务到依赖注入容器（从配置读取）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.GetSection</param>
        /// <param name="sectionName">配置节名称，默认为 "DynamicGrpc"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddDynamicGrpc(this IServiceCollection services, IConfiguration configuration = null, string sectionName = DynamicGrpcOptions.SectionName)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var config = configuration ?? AppConfig.Configuration
                ?? throw new InvalidOperationException("未提供 IConfiguration，且 AppConfig.Configuration 尚未初始化。");
            var options = new DynamicGrpcOptions();
            config.GetSection(sectionName).Bind(options);
            return AddDynamicGrpc(services, options);
        }

        /// <summary>
        /// 添加 Dynamic gRPC 服务到依赖注入容器（从配置读取，并支持额外配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="optionsAction">额外配置委托（在配置文件配置之后执行）</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称，默认为 "DynamicGrpc"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddDynamicGrpc(this IServiceCollection services, Action<DynamicGrpcOptions> optionsAction, IConfiguration configuration = null, string sectionName = DynamicGrpcOptions.SectionName)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var config = configuration ?? AppConfig.Configuration
                ?? throw new InvalidOperationException("未提供 IConfiguration，且 AppConfig.Configuration 尚未初始化。");
            var options = new DynamicGrpcOptions();
            config.GetSection(sectionName).Bind(options);
            optionsAction?.Invoke(options);
            return AddDynamicGrpc(services, options);
        }

        /// <summary>
        /// 映射所有动态 gRPC 服务端点
        /// </summary>
        /// <param name="endpoints">端点路由构建器</param>
        /// <returns>端点路由构建器</returns>
        public static IEndpointRouteBuilder MapDynamicGrpcServices(this IEndpointRouteBuilder endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            var options = endpoints.ServiceProvider.GetRequiredService<DynamicGrpcOptions>();
            var typeProvider = endpoints.ServiceProvider.GetRequiredService<DynamicGrpcServiceTypeProvider>();
            var serviceTypes = DynamicGrpcServiceDiscovery.GetServiceTypes(options, typeProvider);

            foreach (var serviceType in serviceTypes)
            {
                MapGrpcServiceByType(endpoints, serviceType);
            }

            return endpoints;
        }

        /// <summary>
        /// 映射所有动态 gRPC 服务端点（带配置）
        /// </summary>
        /// <param name="endpoints">端点路由构建器</param>
        /// <param name="configureOptions">gRPC 服务端点配置</param>
        /// <returns>端点路由构建器</returns>
        public static IEndpointRouteBuilder MapDynamicGrpcServices(
            this IEndpointRouteBuilder endpoints,
            Action<object> configureOptions)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            var options = endpoints.ServiceProvider.GetRequiredService<DynamicGrpcOptions>();
            var typeProvider = endpoints.ServiceProvider.GetRequiredService<DynamicGrpcServiceTypeProvider>();
            var serviceTypes = DynamicGrpcServiceDiscovery.GetServiceTypes(options, typeProvider);

            foreach (var serviceType in serviceTypes)
            {
                var builder = MapGrpcServiceByType(endpoints, serviceType);
                configureOptions?.Invoke(builder);
            }

            return endpoints;
        }

        /// <summary>
        /// 通过反射映射 gRPC 服务 (使用 protobuf-net.Grpc)
        /// </summary>
        private static object MapGrpcServiceByType(IEndpointRouteBuilder endpoints, Type serviceType)
        {
            // 获取或缓存 MapGrpcService 方法 (来自 ProtoBuf.Grpc.Server.ServicesExtensions)
            if (_mapGrpcServiceMethod == null)
            {
                _mapGrpcServiceMethod = typeof(ServicesExtensions)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m =>
                        m.Name == "MapGrpcService" &&
                        m.IsGenericMethod &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(IEndpointRouteBuilder));
            }

            if (_mapGrpcServiceMethod != null)
            {
                var genericMethod = _mapGrpcServiceMethod.MakeGenericMethod(serviceType);
                return genericMethod.Invoke(null, new object[] { endpoints });
            }

            throw new InvalidOperationException("未能找到 protobuf-net.Grpc 的 MapGrpcService 扩展方法。");
        }

        /// <summary>
        /// 注册所有动态 gRPC 服务到 DI 容器
        /// </summary>
        private static void RegisterDynamicGrpcServices(IServiceCollection services, DynamicGrpcOptions options)
        {
            var typeProvider = services.GetSingletonInstanceOrNull<DynamicGrpcServiceTypeProvider>()
                ?? new DynamicGrpcServiceTypeProvider(options);
            var serviceTypes = DynamicGrpcServiceDiscovery.GetServiceTypes(options, typeProvider);

            foreach (var serviceType in serviceTypes)
            {
                // 获取服务实现的所有接口
                var interfaces = serviceType.GetInterfaces()
                    .Where(i => typeof(IDynamicApi).IsAssignableFrom(i) &&
                                i != typeof(IDynamicApi) &&
                                TypeHelper.IsValidGrpcServiceInterface(i));

                foreach (var serviceInterface in interfaces)
                {
                    // 注册服务到 DI 容器
                    if (!services.Any(s => s.ServiceType == serviceInterface))
                    {
                        services.AddScoped(serviceInterface, serviceType);
                    }
                }

                // 同时注册具体类型
                if (!services.Any(s => s.ServiceType == serviceType))
                {
                    services.AddScoped(serviceType);
                }
            }
        }

    }
}
