using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SyZero;
using SyZero.Configurations;
using SyZero.Domain.Entities;
using SyZero.Domain.Repository;
using SyZero.EntityFrameworkCore;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class EntityFrameworkCoreTests
{
    [Fact]
    public void AddSyZeroEntityFramework_UsesScopedDbContextSharedWithinScope()
    {
        using var database = CreateDatabase();
        ConfigureAppConfig(database.ConnectionString);

        var services = CreateServices();
        services.AddSyZeroEntityFramework<TestDbContext>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        EnsureCreated(provider);

        using var scope1 = provider.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<TestDbContext>();
        var repository1 = scope1.ServiceProvider.GetRequiredService<IRepository<EfTestEntity>>();
        var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.Same(context1, scope1.ServiceProvider.GetRequiredService<TestDbContext>());
        Assert.Same(context1, scope1.ServiceProvider.GetRequiredService<DbContext>());
        Assert.Same(context1, GetFieldValue<DbContext>(repository1, "_dbContext"));
        Assert.Same(context1, GetFieldValue<DbContext>(unitOfWork1, "dataContext"));

        using var scope2 = provider.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<TestDbContext>();

        Assert.NotSame(context1, context2);
    }

    [Fact]
    public async Task EfRepository_ReturnsAffectedCounts_AndSupportsQueryableComposition()
    {
        using var database = CreateDatabase();
        using var provider = CreateProvider(database.ConnectionString);
        EnsureCreated(provider);

        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<EfTestEntity>>();

        Assert.Equal(2, repository.AddList(new[]
        {
            new EfTestEntity { Id = 1, Name = "alpha" },
            new EfTestEntity { Id = 2, Name = "beta" }
        }.AsQueryable()));

        var addedEntity = await repository.AddAsync(new EfTestEntity { Id = 3, Name = "gamma" });

        Assert.Equal(3, addedEntity.Id);
        Assert.Equal(3, repository.Count(_ => true));
        Assert.Equal(1, repository.GetList().Count(entity => entity.Name == "alpha"));
        Assert.Equal(2, (await repository.GetPagedAsync(1, 2, entity => entity.Id)).Count());
        Assert.Equal(2, (await repository.GetListAsync(entity => entity.Id <= 2)).Count());

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
            new EfTestEntity { Id = 4, Name = "delta" },
            new EfTestEntity { Id = 5, Name = "epsilon" }
        }.AsQueryable()));

        var entitiesToUpdate = repository.GetList()
            .Where(item => item.Id >= 4)
            .ToList();
        entitiesToUpdate.ForEach(item => item.Name += "-updated");

        Assert.Equal(2, await repository.UpdateAsync(entitiesToUpdate.AsQueryable()));
        Assert.Equal(2, await repository.DeleteAsync(item => item.Id >= 4 && item.Name != null && item.Name.Contains("updated")));
        Assert.Equal(1, await repository.CountAsync(_ => true));
        Assert.Equal(0, repository.AddList(Array.Empty<EfTestEntity>().AsQueryable()));
        Assert.Equal(0, await repository.UpdateAsync(Array.Empty<EfTestEntity>().AsQueryable()));
    }

    [Fact]
    public async Task UnitOfWork_CommitsAndRollsBackTrackedChanges()
    {
        using var database = CreateDatabase();
        using var provider = CreateProvider(database.ConnectionString);
        EnsureCreated(provider);

        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<EfTestEntity>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        unitOfWork.BeginTransaction();
        repository.Add(new EfTestEntity { Id = 1, Name = "committed" });
        unitOfWork.CommitTransaction();
        unitOfWork.DisposeTransaction();

        Assert.Equal(1, repository.Count(_ => true));

        unitOfWork.BeginTransaction();
        repository.Add(new EfTestEntity { Id = 2, Name = "rolled-back" });
        unitOfWork.RollbackTransaction();
        unitOfWork.DisposeTransaction();

        Assert.Equal(1, repository.Count(_ => true));

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await repository.AddAsync(new EfTestEntity { Id = 3, Name = "async-commit" });
        });

        Assert.Equal(2, repository.Count(_ => true));
        Assert.NotNull(await repository.GetModelAsync(3));
    }

    [Fact]
    public void AddSyZeroEntityFramework_ThrowsForUnsupportedDbType()
    {
        AppConfig.Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionString:Type"] = DbType.PostgreSQL.ToString(),
                ["ConnectionString:Master"] = "Host=localhost;"
            })
            .Build();
        ResetAppConfigCache("connectionOptions");

        var services = CreateServices();

        var exception = Assert.Throws<NotSupportedException>(() => services.AddSyZeroEntityFramework<TestDbContext>());

        Assert.Contains(DbType.PostgreSQL.ToString(), exception.Message);
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
        services.AddSyZeroEntityFramework<TestDbContext>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
    }

    private static void EnsureCreated(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        context.Database.EnsureCreated();
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
        var type = instance.GetType();
        while (type != null)
        {
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                return (T)(field.GetValue(instance) ?? throw new InvalidOperationException($"Field {fieldName} was null."));
            }

            type = type.BaseType;
        }

        throw new MissingFieldException(instance.GetType().FullName, fieldName);
    }

    private static SqliteTestDatabase CreateDatabase()
    {
        return new SqliteTestDatabase();
    }

    private sealed class SqliteTestDatabase : IDisposable
    {
        public SqliteTestDatabase()
        {
            FilePath = Path.Combine(Path.GetTempPath(), $"syzero-ef-{Guid.NewGuid():N}.db");
            ConnectionString = $"Data Source={FilePath}";
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

    public sealed class TestDbContext : SyZeroDbContext<TestDbContext>
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        public DbSet<EfTestEntity> Entities => Set<EfTestEntity>();
    }

    public sealed class EfTestEntity : IEntity
    {
        [Key]
        public long Id { get; set; }

        public string? Name { get; set; }
    }
}
