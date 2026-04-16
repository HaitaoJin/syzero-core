using FreeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        /// <typeparam name="TContext"></typeparam>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddSyZeroRedis(this IServiceCollection services)
        {
            RedisOptions options = AppConfig.GetSection<RedisOptions>("Redis");
            services.AddSingleton<RedisClient>(c =>
            {
                RedisClient redis = null;
                switch (options.Type)
                {
                    case RedisType.MasterSlave:
                        var slave = new List<ConnectionStringBuilder>();
                        slave.AddRange(options.Slave.Select(p => ConnectionStringBuilder.Parse(p)).ToList());
                        redis = new RedisClient(options.Master, slave.ToArray());
                        break;
                    case RedisType.Sentinel:
                        redis = new RedisClient(
                             options.Master,
                             options.Sentinel.ToArray(),
                             true
                              );
                        break;
                    case RedisType.Cluster:
                        var clusters = new List<ConnectionStringBuilder>();
                        clusters.Add(options.Master);
                        clusters.AddRange(options.Slave.Select(p => ConnectionStringBuilder.Parse(p)).ToList());
                        redis = new RedisClient(clusters.ToArray());
                        break;
                    default:
                        System.Console.WriteLine("Redis:配置错误！！！！");
                        break;
                }
                return redis;
            });

            services.AddSingleton<ICache, Redis.Cache>();
            services.AddSingleton<ILockUtil, LockUtil>();
            return services;
        }

        /// <summary>
        /// 注册 Redis 服务管理
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configureOptions">配置选项</param>
        /// <returns></returns>
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
            services.AddSingleton<IServiceManagement, RedisServiceManagement>();

            return services;
        }

        /// <summary>
        /// 注册 Redis 服务管理（使用配置节）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="sectionName">配置节名称</param>
        /// <returns></returns>
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
            services.AddSingleton<IServiceManagement, RedisServiceManagement>();

            return services;
        }

        /// <summary>
        /// 注册 Redis 事件总线
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">配置选项</param>
        /// <returns></returns>
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
            services.AddSingleton<IEventBus, RedisEventBus>();
            services.AddSingleton<RedisEventBus>();

            return services;
        }

        /// <summary>
        /// 注册 Redis 事件总线（从配置读取）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称</param>
        /// <returns></returns>
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
        /// <param name="services">服务集合</param>
        /// <param name="optionsAction">额外配置委托</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称</param>
        /// <returns></returns>
        public static IServiceCollection AddRedisEventBus(this IServiceCollection services, Action<RedisEventBusOptions> optionsAction, IConfiguration configuration = null, string sectionName = RedisEventBusOptions.SectionName)
        {
            var config = configuration ?? AppConfig.Configuration;
            var options = new RedisEventBusOptions();
            config?.GetSection(sectionName)?.Bind(options);
            optionsAction?.Invoke(options);
            return services.AddRedisEventBus(options);
        }
    }
}
