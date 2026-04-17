using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nacos.AspNetCore.V2;
using System;
using SyZero.Nacos;
using SyZero.Service;

namespace SyZero
{
    /// <summary>
    /// Nacos配置扩展
    /// </summary>
    public static class SyZeroNacosExtension
    {
        /// <summary>
        /// 从配置注册 Nacos，并注入 SyZero 的服务管理实现。
        /// </summary>
        public static IServiceCollection AddNacos(this IServiceCollection services, IConfiguration configuration = null, string section = "Nacos")
        {
            services.AddSingleton<IServiceManagement, ServiceManagement>();
            return services.AddNacosAspNet(configuration ?? AppConfig.Configuration, section);
        }

        /// <summary>
        /// 先从配置绑定，再应用额外委托配置。
        /// </summary>
        public static IServiceCollection AddNacos(this IServiceCollection services, Action<NacosAspNetOptions> optionsAction, IConfiguration configuration = null, string section = "Nacos")
        {
            if (optionsAction == null)
            {
                throw new ArgumentNullException(nameof(optionsAction));
            }

            services.AddSingleton<IServiceManagement, ServiceManagement>();

            var config = configuration ?? AppConfig.Configuration;
            if (config != null)
            {
                return services.AddNacosAspNet(options =>
                {
                    config.GetSection(section).Bind(options);
                    optionsAction(options);
                });
            }

            return services.AddNacosAspNet(optionsAction);
        }
    }
}
