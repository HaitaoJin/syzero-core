using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using SyZero;
using SyZero.Configurations;
using SyZero.Domain.Entities;
using SyZero.Domain.Repository;
using SyZero.SqlSugar.DbContext;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class SqlSugarTests
{
    [Fact]
    public void AddSyZeroSqlSugar_UsesScopedDbContextSharedWithinScope()
    {
        using var database = CreateDatabase();
        ConfigureAppConfig(database.ConnectionString);

        var services = CreateServices();
        services.AddSyZeroSqlSugar();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        using var scope1 = provider.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<ISyZeroDbContext>();
        var repository1 = scope1.ServiceProvider.GetRequiredService<IRepository<SqlSugarTestEntity>>();
        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.Same(context1, scope1.ServiceProvider.GetRequiredService<ISyZeroDbContext>());
        Assert.Same(context1, GetFieldValue<ISyZeroDbContext>(repository1, "_dbContext"));
        Assert.Same(context1, GetFieldValue<ISyZeroDbContext>(unitOfWork1, "dataContext"));

        using var scope2 = provider.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<ISyZeroDbContext>();

        Assert.NotSame(context1, context2);
    }

    [Fact]
    public async Task SqlSugarRepository_ReturnsAffectedCounts_AndSupportsQueryableComposition()
    {
        using var database = CreateDatabase();
        using var provider = CreateProvider(database.ConnectionString);
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ISyZeroDbContext>();
        context.CodeFirst.InitTables(typeof(SqlSugarTestEntity));

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<SqlSugarTestEntity>>();

        Assert.Equal(2, repository.AddList(new[]
        {
            new SqlSugarTestEntity { Id = 1, Name = "alpha" },
            new SqlSugarTestEntity { Id = 2, Name = "beta" }
        }.AsQueryable()));

        var addedEntity = await repository.AddAsync(new SqlSugarTestEntity { Id = 3, Name = "gamma" });

        Assert.Equal(3, addedEntity.Id);
        Assert.Equal(3, repository.Count(_ => true));
        Assert.Equal(1, repository.GetList().Where(entity => entity.Name == "alpha").Count());

        var entity = repository.GetModel(1);
        Assert.NotNull(entity);
        entity!.Name = "alpha-updated";

        Assert.Equal(1, await repository.UpdateAsync(entity));
        Assert.Equal("alpha-updated", repository.GetModel(1)!.Name);
        Assert.Equal(1, await repository.DeleteAsync(2));
        Assert.Equal(1, repository.Delete(x => x.Name == "gamma"));
        Assert.Equal(1, repository.Count(_ => true));

        Assert.Equal(2, await repository.AddListAsync(new[]
        {
            new SqlSugarTestEntity { Id = 4, Name = "delta" },
            new SqlSugarTestEntity { Id = 5, Name = "epsilon" }
        }.AsQueryable()));
        Assert.Equal(3, await repository.CountAsync(_ => true));
    }

    [Fact]
    public void DisposeTransaction_DoesNotDisposeScopedDbContext()
    {
        using var database = CreateDatabase();
        using var provider = CreateProvider(database.ConnectionString);
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ISyZeroDbContext>();
        context.CodeFirst.InitTables(typeof(SqlSugarTestEntity));

        var repository = scope.ServiceProvider.GetRequiredService<IRepository<SqlSugarTestEntity>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        unitOfWork.BeginTransaction();
        repository.Add(new SqlSugarTestEntity { Id = 1, Name = "rollback" });
        unitOfWork.RollbackTransaction();

        Assert.Equal(0, repository.Count(_ => true));

        unitOfWork.DisposeTransaction();
        repository.Add(new SqlSugarTestEntity { Id = 2, Name = "still-alive" });

        Assert.Equal(1, repository.Count(_ => true));
        Assert.Equal("still-alive", repository.GetModel(2)!.Name);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        ConfigureAppConfig(connectionString);

        var services = CreateServices();
        services.AddSyZeroSqlSugar();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
    }

    private static void ConfigureAppConfig(string connectionString)
    {
        AppConfig.Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionString:Type"] = DbType.Sqlite.ToString(),
                ["ConnectionString:Master"] = connectionString
            })
            .Build();

        ResetAppConfigCache("connectionOptions");
    }

    private static void ResetAppConfigCache(string fieldName)
    {
        var field = typeof(AppConfig).GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AppConfig).FullName, fieldName);
        field.SetValue(null, null);
    }

    private static T GetFieldValue<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)(field.GetValue(instance) ?? throw new InvalidOperationException($"Field {fieldName} was null."));
    }

    private static SqliteTestDatabase CreateDatabase()
    {
        return new SqliteTestDatabase();
    }

    private sealed class SqliteTestDatabase : IDisposable
    {
        public SqliteTestDatabase()
        {
            FilePath = Path.Combine(Path.GetTempPath(), $"syzero-sqlsugar-{Guid.NewGuid():N}.db");
            ConnectionString = $"DataSource={FilePath}";
        }

        public string ConnectionString { get; }

        private string FilePath { get; }

        public void Dispose()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    File.Delete(FilePath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    public sealed class SqlSugarTestEntity : IEntity
    {
        [Key]
        public long Id { get; set; }

        public string? Name { get; set; }
    }
}
