using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MongoDB.Driver;
using SyZero.Domain.Entities;
using SyZero.Domain.Repository;
using SyZero.MongoDB;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class MongoDbTests
{
    [Fact]
    public async Task MongoRepository_ExplicitInterfaceMembers_DelegateToRepositoryImplementation()
    {
        var dataset = new List<TestEntity>
        {
            new() { Id = 1, Name = "alpha" },
            new() { Id = 2, Name = "beta" }
        };

        var repository = (IBaseRepository<TestEntity, long>)CreateRepository(dataset, out var collectionMock);
        var entity = new TestEntity { Id = 3, Name = "gamma" };

        Assert.Same(entity, repository.Add(entity));
        Assert.Same(entity, await repository.AddAsync(entity));
        Assert.Equal(2, repository.AddList(dataset.AsQueryable()));
        Assert.Equal(2, await repository.AddListAsync(dataset.AsQueryable()));
        Assert.Equal(2, repository.Count(_ => true));
        Assert.Equal(3, await repository.CountAsync(_ => true));
        Assert.Equal(2, repository.GetList(_ => true).Count());
        Assert.Equal(2, (await repository.GetListAsync(_ => true)).Count());
        Assert.Equal(1, repository.GetModel(1).Id);
        Assert.Equal(1, (await repository.GetModelAsync(1))!.Id);
        Assert.Equal(1, repository.GetModel(_ => true).Id);
        Assert.Equal(1, (await repository.GetModelAsync(_ => true))!.Id);

        collectionMock.Verify(collection => collection.InsertOne(entity, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        collectionMock.Verify(collection => collection.InsertOneAsync(entity, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AddSyZeroMongoDb_WithoutArguments_RegistersContextAndRepository()
    {
        var services = new ServiceCollection();
        AppConfig.Configuration = CreateConfiguration("MongoDB");

        services.AddSyZeroMongoDB();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMongoContext>());
        Assert.NotNull(provider.GetService<IRepository<TestEntity>>());
    }

    [Fact]
    public void AddSyZeroMongoDb_WithConfiguration_RegistersContextAndRepository()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("CustomMongo");

        services.AddSyZeroMongoDB(configuration, "CustomMongo");

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMongoContext>());
        Assert.NotNull(provider.GetService<IRepository<TestEntity>>());
    }

    [Fact]
    public void AddSyZeroMongoDb_WithOptionsAction_RegistersContextAndRepository()
    {
        var services = new ServiceCollection();
        AppConfig.Configuration = new ConfigurationBuilder().Build();

        services.AddSyZeroMongoDB(options =>
        {
            options.DataBase = "syzero";
            options.Services = new List<MongoServers>
            {
                new() { Host = "localhost", Port = 27017 }
            };
        });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMongoContext>());
        Assert.NotNull(provider.GetService<IRepository<TestEntity>>());
    }

    private static IConfiguration CreateConfiguration(string sectionName)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{sectionName}:DataBase"] = "syzero",
                [$"{sectionName}:Services:0:Host"] = "localhost",
                [$"{sectionName}:Services:0:Port"] = "27017"
            })
            .Build();
    }

    private static MongoRepository<TestEntity> CreateRepository(
        List<TestEntity> dataset,
        out Mock<IMongoCollection<TestEntity>> collectionMock)
    {
        collectionMock = new Mock<IMongoCollection<TestEntity>>();

        collectionMock.Setup(collection => collection.InsertOne(It.IsAny<TestEntity>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()));
        collectionMock.Setup(collection => collection.InsertOneAsync(It.IsAny<TestEntity>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        collectionMock.Setup(collection => collection.InsertMany(It.IsAny<IEnumerable<TestEntity>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()));
        collectionMock.Setup(collection => collection.InsertManyAsync(It.IsAny<IEnumerable<TestEntity>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        collectionMock.Setup(collection => collection.CountDocuments(It.IsAny<FilterDefinition<TestEntity>>(), It.IsAny<CountOptions>(), It.IsAny<CancellationToken>()))
            .Returns(2);
        collectionMock.Setup(collection => collection.CountDocumentsAsync(It.IsAny<FilterDefinition<TestEntity>>(), It.IsAny<CountOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        collectionMock.Setup(collection => collection.FindSync(It.IsAny<FilterDefinition<TestEntity>>(), It.IsAny<FindOptions<TestEntity, TestEntity>>(), It.IsAny<CancellationToken>()))
            .Returns(() => CreateCursor(dataset).Object);
        collectionMock.Setup(collection => collection.FindAsync(It.IsAny<FilterDefinition<TestEntity>>(), It.IsAny<FindOptions<TestEntity, TestEntity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateCursor(dataset).Object);

        var contextMock = new Mock<IMongoContext>();
        contextMock.Setup(context => context.Set<TestEntity>()).Returns(collectionMock.Object);
        return new MongoRepository<TestEntity>(contextMock.Object);
    }

    private static Mock<IAsyncCursor<TestEntity>> CreateCursor(IReadOnlyCollection<TestEntity> items)
    {
        var cursorMock = new Mock<IAsyncCursor<TestEntity>>();
        cursorMock.SetupSequence(cursor => cursor.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(true)
            .Returns(false);
        cursorMock.SetupSequence(cursor => cursor.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock.SetupGet(cursor => cursor.Current).Returns(items);
        return cursorMock;
    }

    public sealed class TestEntity : IEntity
    {
        public long Id { get; set; }

        public string? Name { get; set; }
    }
}
