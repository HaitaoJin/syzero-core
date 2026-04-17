using FreeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SyZero.Cache;
using SyZero.EventBus;
using SyZero.Redis;
using SyZero.Service;
using SyZero.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SyZero
{
    public static class SyZeroRedisExtension
    {
        /// <summary>
        /// 注册RedisModule
        /// </summary>
        public static IServiceCollection AddSyZeroRedis(this IServiceCollection services)
        {
            var options = AppConfig.GetSection<RedisOptions>("Redis") ?? new RedisOptions();
            return services.AddSyZeroRedis(options);
        }

        /// <summary>
        /// 使用指定配置注册 Redis
        /// </summary>
        public static IServiceCollection AddSyZeroRedis(this IServiceCollection services, IConfiguration configuration, string sectionName = "Redis")
        {
            var options = new RedisOptions();
            configuration?.GetSection(sectionName)?.Bind(options);
            return services.AddSyZeroRedis(options);
        }

        /// <summary>
        /// 使用默认配置并允许额外覆盖注册 Redis
        /// </summary>
        public static IServiceCollection AddSyZeroRedis(this IServiceCollection services, Action<RedisOptions> optionsAction)
        {
            var options = AppConfig.GetSection<RedisOptions>("Redis") ?? new RedisOptions();
            optionsAction?.Invoke(options);
            return services.AddSyZeroRedis(options);
        }

        /// <summary>
        /// 注册 Redis 服务管理
        /// </summary>
        public static IServiceCollection AddRedisServiceManagement(this IServiceCollection services, Action<RedisServiceManagementOptions> configureOptions = null)
        {
            var redisDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(RedisClient));
            if (redisDescriptor == null)
            {
                services.AddSyZeroRedis();
            }

            var options = AppConfig.GetSection<RedisServiceManagementOptions>(RedisServiceManagementOptions.SectionName)
                          ?? new RedisServiceManagementOptions();
            configureOptions?.Invoke(options);
            options.Validate();

            services.AddSingleton(options);
            services.TryAddSingleton<RedisServiceManagement>();
            services.TryAddSingleton<IServiceManagement>(sp => sp.GetRequiredService<RedisServiceManagement>());

            return services;
        }

        /// <summary>
        /// 注册 Redis 服务管理（使用配置节）
        /// </summary>
        public static IServiceCollection AddRedisServiceManagement(this IServiceCollection services, string sectionName)
        {
            var redisDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(RedisClient));
            if (redisDescriptor == null)
            {
                services.AddSyZeroRedis();
            }

            var options = AppConfig.GetSection<RedisServiceManagementOptions>(sectionName)
                          ?? new RedisServiceManagementOptions();
            options.Validate();

            services.AddSingleton(options);
            services.TryAddSingleton<RedisServiceManagement>();
            services.TryAddSingleton<IServiceManagement>(sp => sp.GetRequiredService<RedisServiceManagement>());

            return services;
        }

        /// <summary>
        /// 注册 Redis 事件总线
        /// </summary>
        public static IServiceCollection AddRedisEventBus(this IServiceCollection services, RedisEventBusOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var redisDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(RedisClient));
            if (redisDescriptor == null)
            {
                services.AddSyZeroRedis();
            }

            options.Validate();
            services.AddSingleton(options);
            services.TryAddSingleton<RedisEventBus>();
            services.TryAddSingleton<IEventBus>(sp => sp.GetRequiredService<RedisEventBus>());

            return services;
        }

        /// <summary>
        /// 注册 Redis 事件总线（从配置读取）
        /// </summary>
        public static IServiceCollection AddRedisEventBus(this IServiceCollection services, IConfiguration configuration = null, string sectionName = RedisEventBusOptions.SectionName)
        {
            var config = configuration ?? AppConfig.Configuration;
            var options = new RedisEventBusOptions();
            config?.GetSection(sectionName)?.Bind(options);
            return services.AddRedisEventBus(options);
        }

        /// <summary>
        /// 注册 Redis 事件总线（从配置读取并支持额外配置）
        /// </summary>
        public static IServiceCollection AddRedisEventBus(this IServiceCollection services, Action<RedisEventBusOptions> optionsAction, IConfiguration configuration = null, string sectionName = RedisEventBusOptions.SectionName)
        {
            var config = configuration ?? AppConfig.Configuration;
            var options = new RedisEventBusOptions();
            config?.GetSection(sectionName)?.Bind(options);
            optionsAction?.Invoke(options);
            return services.AddRedisEventBus(options);
        }

        private static IServiceCollection AddSyZeroRedis(this IServiceCollection services, RedisOptions options)
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

            services.TryAddSingleton(options);
            services.TryAddSingleton<RedisClient>(_ =>
            {
                switch (options.Type)
                {
                    case RedisType.MasterSlave:
                        var slave = options.Slave.Select(ConnectionStringBuilder.Parse).ToArray();
                        return new RedisClient(options.Master, slave);
                    case RedisType.Sentinel:
                        return new RedisClient(options.Master, options.Sentinel.ToArray(), true);
                    case RedisType.Cluster:
                        var clusters = new List<ConnectionStringBuilder>
                        {
                            options.Master
                        };
                        clusters.AddRange(options.Slave.Select(ConnectionStringBuilder.Parse));
                        return new RedisClient(clusters.ToArray());
                    default:
                        throw new ArgumentOutOfRangeException(nameof(options.Type), options.Type, "不支持的 Redis 类型");
                }
            });

            services.TryAddSingleton<ICache, Redis.Cache>();
            services.TryAddSingleton<ILockUtil, LockUtil>();
            return services;
        }
    }
}
