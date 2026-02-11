using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Reflection;
using SyZero.Dependency;
using SyZero.Runtime.Session;
using SyZero.Serialization;
using SyZero.Service;
using SyZero.Util;

namespace SyZero
{
    public static class SyZeroExtension
    {
        private static string _registeredServiceId;

        public static IHost UseSyZero(this IHost app)
        {
            #region 设置全局服务提供者
            SyZeroUtil.ServiceProvider = app.Services;
            #endregion

            #region 服务注册
            RegisterServiceAsync(app.Services);
            #endregion

            #region 应用停止时注销服务
            var lifetime = app.Services.GetService<IHostApplicationLifetime>();
            if (lifetime != null && !string.IsNullOrEmpty(_registeredServiceId))
            {
                lifetime.ApplicationStopping.Register(() =>
                {
                    DeregisterServiceAsync(app.Services);
                });
            }
            #endregion

            return app;
        }

        /// <summary>
        /// 注册服务到服务管理中心
        /// </summary>
        private static void RegisterServiceAsync(IServiceProvider serviceProvider)
        {
            try
            {
                var serverOptions = AppConfig.ServerOptions;
                if (serverOptions == null || string.IsNullOrEmpty(serverOptions.Name))
                {
                    System.Console.WriteLine("SyZero: 未配置服务信息，跳过服务注册");
                    return;
                }

                var serviceManagement = serviceProvider.GetService<IServiceManagement>();
                if (serviceManagement == null)
                {
                    System.Console.WriteLine("SyZero: 未找到服务管理实现，跳过服务注册");
                    return;
                }

                var serviceId = $"{serverOptions.Name}-{serverOptions.Ip}-{serverOptions.Port}";
                var serviceInfo = new ServiceInfo
                {
                    ServiceID = serviceId,
                    ServiceName = serverOptions.Name,
                    ServiceAddress = serverOptions.Ip,
                    ServicePort = serverOptions.Port,
                    ServiceProtocol = serverOptions.Protocol,
                    Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0",
                    IsHealthy = true,
                    Enabled = true,
                    Weight = 1.0,
                    RegisterTime = DateTime.Now,
                    LastHeartbeat = DateTime.Now,
                    HealthCheckUrl = $"{serverOptions.Protocol.ToString().ToLower()}://{serverOptions.Ip}:{serverOptions.Port}/health",
                    Tags = new System.Collections.Generic.List<string>(),
                    Metadata = new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["startTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                    }
                };

                serviceManagement.RegisterService(serviceInfo).GetAwaiter().GetResult();
                _registeredServiceId = serviceId;
                System.Console.WriteLine($"SyZero: 服务 [{serverOptions.Name}] 注册成功 ({serverOptions.Ip}:{serverOptions.Port})");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"SyZero: 服务注册失败 - {ex.Message}");
            }
        }

        /// <summary>
        /// 注销服务
        /// </summary>
        private static void DeregisterServiceAsync(IServiceProvider serviceProvider)
        {
            try
            {
                if (string.IsNullOrEmpty(_registeredServiceId))
                {
                    return;
                }

                var serviceManagement = serviceProvider.GetService<IServiceManagement>();
                if (serviceManagement == null)
                {
                    return;
                }

                serviceManagement.DeregisterService(_registeredServiceId).GetAwaiter().GetResult();
                System.Console.WriteLine($"SyZero: 服务 [{_registeredServiceId}] 已注销");
                _registeredServiceId = null;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"SyZero: 服务注销失败 - {ex.Message}");
            }
        }

        public static IHostApplicationBuilder AddSyZero(this IHostApplicationBuilder builder)
        {
            AppConfig.Configuration = builder.Configuration;
            builder.Services.AddSyZero();
            return builder;
        }

        /// <summary>
        /// SyZeroModule
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddSyZero(this IServiceCollection services)
        {
            System.Console.WriteLine(@" _____  __    __  ______  _____   _____    _____  
/  ___/ \ \  / / |___  / | ____| |  _  \  /  _  \ 
| |___   \ \/ /     / /  | |__   | |_| |  | | | | 
\___  \   \  /     / /   |  __|  |  _  /  | | | | 
 ___| |   / /     / /__  | |___  | | \ \  | |_| | 
/_____/  /_/     /_____| |_____| |_|  \_\ \_____/ ");
            // 获取当前程序集
            System.Console.WriteLine("");
            System.Console.WriteLine("版本号: " + Assembly.GetExecutingAssembly().GetName().Version);
            System.Console.WriteLine("启动中......");

            //注入SySession
            services.AddScoped<ISySession, SySession>();

            //// 获取所有实现了IScopedDependency接口的类型
            //var scopedDependency = ReflectionHelper.GetTypes()
            //                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IScopedDependency).IsAssignableFrom(t));
            //foreach (var type in scopedDependency)
            //{
            //    services.AddScoped(type.GetInterfaces().Last(), type);
            //}

            //// 获取所有实现了ISingletonDependency接口的类型
            //var singletonDependencyTypes = ReflectionHelper.GetTypes()
            //                    .Where(t => t.IsClass && !t.IsAbstract && typeof(ISingletonDependency).IsAssignableFrom(t));
            //foreach (var type in singletonDependencyTypes)
            //{
            //    services.AddSingleton(type.GetInterfaces().Last(), type);
            //}

            //// 获取所有实现了ITransientDependency接口的类型
            //var transientDependency = ReflectionHelper.GetTypes()
            //                    .Where(t => t.IsClass && !t.IsAbstract && typeof(ITransientDependency).IsAssignableFrom(t));
            //foreach (var type in transientDependency)
            //{
            //    services.AddScoped(type.GetInterfaces().Last(), type);
            //}
            services.AddClassesAsImplementedInterface(typeof(IScopedDependency), ServiceLifetime.Scoped);
            services.AddClassesAsImplementedInterface(typeof(ISingletonDependency), ServiceLifetime.Singleton);
            services.AddClassesAsImplementedInterface(typeof(ITransientDependency), ServiceLifetime.Transient);

            // 获取所有继承SyZeroServiceBase类的类型
            var syZeroServiceBaseTypes = ReflectionHelper.GetTypes()
                                .Where(t => t.IsClass && !t.IsAbstract && typeof(SyZeroServiceBase).IsAssignableFrom(t));
            foreach (var type in syZeroServiceBaseTypes)
            {
                services.AddScoped(type);
                
                var interfaceType = type.GetInterfaces().FirstOrDefault(i => i.Name == "I" + type.Name);
                if (interfaceType != null)
                {
                    services.AddScoped(interfaceType, provider => provider.GetService(type));
                }
            }

            return services;
        }
    }
}
