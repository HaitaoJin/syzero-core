using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SyZero.Configurations;
using SyZero.Domain.Repository;
using SyZero.EntityFrameworkCore.Repositories;

namespace SyZero.EntityFrameworkCore
{
    public static class SyZeroEntityFrameworkExtension
    {
        /// <summary>
        /// 注册 EntityFramework。
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddSyZeroEntityFramework<TContext>(this IServiceCollection services)
            where TContext : DbContext
        {
            var connectionOptions = AppConfig.ConnectionOptions ?? throw new InvalidOperationException("未找到 ConnectionString 配置。");
            ValidateProviderConfiguration<TContext>(connectionOptions);
            return services.AddSyZeroEntityFramework<TContext>(optionsBuilder => ConfigureProvider<TContext>(optionsBuilder, connectionOptions));
        }

        /// <summary>
        /// 使用自定义 DbContextOptions 注册 EntityFramework。
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="optionsAction"></param>
        /// <returns></returns>
        public static IServiceCollection AddSyZeroEntityFramework<TContext>(
            this IServiceCollection services,
            Action<DbContextOptionsBuilder> optionsAction)
            where TContext : DbContext
        {
            if (optionsAction == null)
            {
                throw new ArgumentNullException(nameof(optionsAction));
            }

            services.AddDbContext<TContext>(optionsAction);
            services.TryAddScoped<DbContext>(serviceProvider => serviceProvider.GetRequiredService<TContext>());
            services.TryAddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.TryAddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }

        private static void ConfigureProvider<TContext>(
            DbContextOptionsBuilder optionsBuilder,
            SyZeroConnectionOptions connectionOptions)
            where TContext : DbContext
        {
            if (connectionOptions == null)
            {
                throw new ArgumentNullException(nameof(connectionOptions));
            }

            switch (connectionOptions.Type)
            {
                case DbType.MySql:
                    optionsBuilder.UseMySQL(connectionOptions.Master);
                    break;
                case DbType.SqlServer:
                    optionsBuilder.UseSqlServer(connectionOptions.Master);
                    break;
                case DbType.Sqlite:
                    optionsBuilder.UseSqlite(connectionOptions.Master);
                    break;
                default:
                    throw new NotSupportedException($"SyZero.EntityFrameworkCore 暂不支持数据库类型: {connectionOptions.Type}。");
            }
        }

        private static void ValidateProviderConfiguration<TContext>(SyZeroConnectionOptions connectionOptions)
            where TContext : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<TContext>();
            ConfigureProvider<TContext>(optionsBuilder, connectionOptions);
        }
    }
}
