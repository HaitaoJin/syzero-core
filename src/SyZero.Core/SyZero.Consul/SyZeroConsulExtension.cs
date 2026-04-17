using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NConsul;
using NConsul.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using SyZero.Consul;
using SyZero.Consul.Config;
using SyZero.Service;

namespace SyZero
{
    /// <summary>
    /// SyZero Consul 扩展方法
    /// </summary>
    public static class SyZeroConsulExtension
    {
        /// <summary>
        /// 添加 Consul 服务到依赖注入容器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">配置选项</param>
        /// <returns>服务集合</returns>
        /// <exception cref="ArgumentNullException">options 为 null 时抛出</exception>
        public static IServiceCollection AddConsul(this IServiceCollection services, ConsulServiceOptions options)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (string.IsNullOrWhiteSpace(options.ConsulAddress))
            {
                throw new ArgumentException("ConsulAddress 不能为空。", nameof(options));
            }

            services.AddSingleton(options);
            services.AddSingleton<IConsulClient>(p =>
            {
                return new ConsulClient(config =>
                {
                    config.Address = new Uri(options.ConsulAddress);
                    config.Token = options.Token;
                });
            });
            services.AddSingleton<IServiceManagement, ServiceManagement>();
            return services;
        }

        /// <summary>
        /// 添加 Consul 服务到依赖注入容器（从配置读取）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称，默认为 "Consul"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddConsul(this IServiceCollection services, IConfiguration configuration = null, string sectionName = ConsulServiceOptions.SectionName)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var config = configuration ?? AppConfig.Configuration;
            var options = new ConsulServiceOptions();
            config.GetSection(sectionName).Bind(options);
            return AddConsul(services, options);
        }

        /// <summary>
        /// 添加 Consul 服务到依赖注入容器（从配置读取，并支持额外配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="optionsAction">额外配置委托（在配置文件配置之后执行）</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称，默认为 "Consul"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddConsul(this IServiceCollection services, Action<ConsulServiceOptions> optionsAction, IConfiguration configuration = null, string sectionName = ConsulServiceOptions.SectionName)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var config = configuration ?? AppConfig.Configuration;
            var options = new ConsulServiceOptions();
            config.GetSection(sectionName).Bind(options);
            optionsAction?.Invoke(options);
            return AddConsul(services, options);
        }
    }
}
