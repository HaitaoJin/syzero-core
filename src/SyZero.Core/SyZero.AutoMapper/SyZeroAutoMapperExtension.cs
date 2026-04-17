using System.Reflection;
using AutoMapper;
using AutoMapper.EquivalencyExpression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            ArgumentNullException.ThrowIfNull(services);

            return AddSyZeroAutoMapper(services, configAction: null, Array.Empty<Assembly>());
        }

        /// <summary>
        /// 添加 SyZero AutoMapper 服务（指定程序集）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="assemblies">要扫描的程序集</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSyZeroAutoMapper(this IServiceCollection services, params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);

            return AddSyZeroAutoMapper(services, configAction: null, assemblies);
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
            Action<IMapperConfigurationExpression>? configAction,
            params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);

            var resolvedAssemblies = ResolveAssemblies(assemblies);

            services.AddAutoMapper(cfg =>
            {
                cfg.AddCollectionMappers();
                configAction?.Invoke(cfg);
            }, resolvedAssemblies);

            services.Replace(ServiceDescriptor.Singleton<SyZero.ObjectMapper.IObjectMapper, SyZero.AutoMapper.ObjectMapper>());

            return services;
        }

        private static Assembly[] ResolveAssemblies(Assembly[]? assemblies)
        {
            var resolvedAssemblies = assemblies?
                .OfType<Assembly>()
                .Distinct()
                .ToArray();

            return resolvedAssemblies is { Length: > 0 }
                ? resolvedAssemblies
                : ReflectionHelper.GetAssemblies().Distinct().ToArray();
        }
    }
}
