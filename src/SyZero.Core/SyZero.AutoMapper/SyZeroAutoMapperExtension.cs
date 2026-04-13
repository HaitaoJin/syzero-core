using System.Reflection;
using AutoMapper;
using AutoMapper.EquivalencyExpression;
using Microsoft.Extensions.DependencyInjection;

namespace SyZero
{
    /// <summary>
    /// SyZero AutoMapper 扩展方法
    /// </summary>
    public static class SyZeroAutoMapperExtension
    {
        /// <summary>
        /// 添加 SyZero AutoMapper 服务（自动扫描所有程序集）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSyZeroAutoMapper(this IServiceCollection services)
        {
            return services.AddSyZeroAutoMapper(ReflectionHelper.GetAssemblies().ToArray());
        }

        /// <summary>
        /// 添加 SyZero AutoMapper 服务（指定程序集）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="assemblies">要扫描的程序集</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSyZeroAutoMapper(this IServiceCollection services, params Assembly[] assemblies)
        {
            services.AddAutoMapper(cfg =>
            {
                cfg.AddCollectionMappers();
            }, assemblies);

            services.AddScoped<SyZero.ObjectMapper.IObjectMapper, SyZero.AutoMapper.ObjectMapper>();

            return services;
        }

        /// <summary>
        /// 添加 SyZero AutoMapper 服务（自定义配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configAction">AutoMapper 配置委托</param>
        /// <param name="assemblies">要扫描的程序集</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSyZeroAutoMapper(
            this IServiceCollection services,
            Action<IMapperConfigurationExpression> configAction,
            params Assembly[] assemblies)
        {
            services.AddAutoMapper(cfg =>
            {
                cfg.AddCollectionMappers();
                configAction?.Invoke(cfg);
            }, assemblies);

            services.AddScoped<SyZero.ObjectMapper.IObjectMapper, SyZero.AutoMapper.ObjectMapper>();

            return services;
        }
    }
}
