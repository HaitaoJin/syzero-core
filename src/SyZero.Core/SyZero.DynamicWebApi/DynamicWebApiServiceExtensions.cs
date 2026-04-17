using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace SyZero.DynamicWebApi
{
    /// <summary>
    /// Dynamic WebApi 服务扩展方法
    /// </summary>
    public static class DynamicApiServiceExtensions
    {
        /// <summary>
        /// 添加 Dynamic WebApi 到依赖注入容器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">配置选项</param>
        /// <returns>服务集合</returns>
        /// <exception cref="ArgumentNullException">options 为 null 时抛出</exception>
        /// <exception cref="InvalidOperationException">在 AddMvc 之前调用时抛出</exception>
        public static IServiceCollection AddDynamicWebApi(this IServiceCollection services, DynamicWebApiOptions options)
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

            var partManager = services.GetSingletonInstanceOrNull<ApplicationPartManager>();

            if (partManager == null)
            {
                throw new InvalidOperationException("\"AddDynamicWebApi\" 必须在 \"AddMvc\" 之后调用。");
            }

            // 注册程序集
            foreach (var assembly in ReflectionHelper.GetAssemblies())
            {
                if (partManager.ApplicationParts.OfType<AssemblyPart>().Any(part => part.Assembly == assembly))
                {
                    continue;
                }

                partManager.ApplicationParts.Add(new AssemblyPart(assembly));
            }

            // 注册控制器特性提供程序
            if (!partManager.FeatureProviders.OfType<DynamicWebApiControllerFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new DynamicWebApiControllerFeatureProvider());
            }

            // 注册 MVC 约定
            services.Configure<MvcOptions>(mvcOptions =>
            {
                mvcOptions.Conventions.Add(new DynamicWebApiConvention(options));
            });

            return services;
        }

        /// <summary>
        /// 添加 Dynamic WebApi 到依赖注入容器（从配置读取）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.GetSection</param>
        /// <param name="sectionName">配置节名称，默认为 "DynamicWebApi"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddDynamicWebApi(this IServiceCollection services, IConfiguration configuration = null, string sectionName = DynamicWebApiOptions.SectionName)
        {
            var options = new DynamicWebApiOptions();
            var config = configuration ?? AppConfig.Configuration;
            config?.GetSection(sectionName).Bind(options);
            return AddDynamicWebApi(services, options);
        }

        /// <summary>
        /// 添加 Dynamic WebApi 到依赖注入容器（从配置读取，并支持额外配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="optionsAction">额外配置委托（在配置文件配置之后执行）</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称，默认为 "DynamicWebApi"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddDynamicWebApi(this IServiceCollection services, Action<DynamicWebApiOptions> optionsAction, IConfiguration configuration = null, string sectionName = DynamicWebApiOptions.SectionName)
        {
            var options = new DynamicWebApiOptions();
            var config = configuration ?? AppConfig.Configuration;
            config?.GetSection(sectionName).Bind(options);
            optionsAction?.Invoke(options);
            return AddDynamicWebApi(services, options);
        }
    }
}
