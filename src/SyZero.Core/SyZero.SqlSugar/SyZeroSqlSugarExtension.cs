using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlSugar;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using SyZero.Configurations;
using SyZero.Domain.Entities;
using SyZero.Domain.Repository;
using SyZero.Extension;
using SyZero.SqlSugar;
using SyZero.SqlSugar.DbContext;
using SyZero.SqlSugar.Repositories;

namespace SyZero
{
    public static class SyZeroSqlSugarExtension
    {
        /// <summary>
        /// 注册SqlSugar
        /// </summary>
        public static IServiceCollection AddSyZeroSqlSugar<TContext>(this IServiceCollection services)
            where TContext : SyZeroDbContext
        {
            var connectionOptions = AppConfig.ConnectionOptions ?? throw new InvalidOperationException("未找到 ConnectionString 配置。");

            services.AddSingleton<ConnectionConfig>(_ =>
            {
                var slaveConnections = connectionOptions.Slave ?? Enumerable.Empty<SlaveConnectionOptions>();
                return new ConnectionConfig
                {
                    ConnectionString = connectionOptions.Master,
                    DbType = (global::SqlSugar.DbType)connectionOptions.Type,
                    IsAutoCloseConnection = true,
                    InitKeyType = InitKeyType.Attribute,
                    SlaveConnectionConfigs = slaveConnections.Select(slave => new SlaveConnectionConfig
                    {
                        HitRate = slave.HitRate,
                        ConnectionString = slave.ConnectionString
                    }).ToList(),
                    ConfigureExternalServices = new ConfigureExternalServices
                    {
                        EntityService = (property, column) =>
                        {
                            var attributes = property.GetCustomAttributes(true);
                            if (attributes.Any(it => it is KeyAttribute))
                            {
                                column.IsPrimarykey = true;
                            }

                            if (attributes.OfType<DatabaseGeneratedAttribute>().FirstOrDefault() is DatabaseGeneratedAttribute databaseGeneratedAttribute)
                            {
                                column.IsIdentity = databaseGeneratedAttribute.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;
                            }

                            if (attributes.OfType<ColumnAttribute>().FirstOrDefault() is ColumnAttribute columnAttribute)
                            {
                                column.DbColumnName = columnAttribute.Name;
                            }

                            if (attributes.Any(it => it is NotMappedAttribute))
                            {
                                column.IsIgnore = true;
                            }

                            if (property.PropertyType.IsGenericType &&
                                property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                column.IsNullable = true;
                            }
                            else if (property.PropertyType == typeof(string) &&
                                     property.GetCustomAttribute<RequiredAttribute>() == null)
                            {
                                column.IsNullable = true;
                            }
                        },
                        EntityNameService = (type, entity) =>
                        {
                            if (type.GetCustomAttributes(true).OfType<TableAttribute>().FirstOrDefault() is TableAttribute tableAttribute)
                            {
                                entity.DbTableName = tableAttribute.Name;
                            }
                        }
                    }
                };
            });

            // 注册上下文，确保同一作用域内仓储和工作单元共享同一个 DbContext 实例
            services.AddScoped<TContext>();
            services.AddScoped<ISyZeroDbContext>(serviceProvider => serviceProvider.GetRequiredService<TContext>());

            // 注册仓储泛型
            services.AddClassesAsImplementedInterface(typeof(IRepository<>), ServiceLifetime.Scoped);
            services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));

            // 注册持久化
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }

        /// <summary>
        /// 注册SqlSugar（使用默认的SyZeroDbContext）
        /// </summary>
        public static IServiceCollection AddSyZeroSqlSugar(this IServiceCollection services)
        {
            services.AddSyZeroSqlSugar<SyZeroDbContext>();
            return services;
        }

        /// <summary>
        /// 初始化表
        /// </summary>
        public static IHost InitTables(this IHost app)
        {
            Console.WriteLine("检查数据库,初始化表...");
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ISyZeroDbContext>()
                .CodeFirst.SetStringDefaultLength(2000)
                .InitTables(ReflectionHelper.GetTypes()
                    .Where(m => typeof(IEntity).IsAssignableFrom(m) && m != typeof(IEntity) && m != typeof(Entity))
                    .ToArray());
            return app;
        }
    }
}
