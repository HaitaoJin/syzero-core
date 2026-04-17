using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using SyZero.Feign;

namespace SyZero
{
    /// <summary>
    /// Feign 服务扩展方法
    /// </summary>
    public static class SyZeroFeignExtension
    {
        /// <summary>
        /// 添加 Feign 到依赖注入容器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">配置选项</param>
        /// <returns>服务集合</returns>
        /// <exception cref="ArgumentNullException">options 为 null 时抛出</exception>
        public static IServiceCollection AddSyZeroFeign(this IServiceCollection services, FeignOptions options)
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

            FeignServiceRegistrar.Register(services, options);

            return services;
        }

        /// <summary>
        /// 添加 Feign 到依赖注入容器（从配置读取）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称，默认为 "Feign"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSyZeroFeign(this IServiceCollection services, IConfiguration configuration = null, string sectionName = FeignOptions.SectionName)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var config = configuration ?? AppConfig.Configuration ?? throw new InvalidOperationException("未找到 Feign 配置源。");
            var options = new FeignOptions();
            config.GetSection(sectionName).Bind(options);
            return AddSyZeroFeign(services, options);
        }

        /// <summary>
        /// 添加 Feign 到依赖注入容器（从配置读取，并支持额外配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="optionsAction">额外配置委托（在配置文件配置之后执行）</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称，默认为 "Feign"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSyZeroFeign(this IServiceCollection services, Action<FeignOptions> optionsAction, IConfiguration configuration = null, string sectionName = FeignOptions.SectionName)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var config = configuration ?? AppConfig.Configuration ?? throw new InvalidOperationException("未找到 Feign 配置源。");
            var options = new FeignOptions();
            config.GetSection(sectionName).Bind(options);
            optionsAction?.Invoke(options);
            return AddSyZeroFeign(services, options);
        }
    }
}
