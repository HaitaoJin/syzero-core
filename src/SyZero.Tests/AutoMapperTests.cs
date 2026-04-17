using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using SyZero.ObjectMapper;
using Xunit;

namespace SyZero.Tests;

public class AutoMapperTests
{
    [Fact]
    public void AddSyZeroAutoMapper_RegistersMapperAndMapsUsingProvidedAssembly()
    {
        var services = new ServiceCollection();
        services.AddSyZeroAutoMapper(typeof(TestProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        var objectMapper = provider.GetRequiredService<IObjectMapper>();

        var destination = objectMapper.Map<Destination>(new Source
        {
            Id = 7,
            Name = "alice",
            Values = ["a", "b"]
        });

        Assert.IsType<SyZero.AutoMapper.ObjectMapper>(objectMapper);
        Assert.Equal(7, destination.Id);
        Assert.Equal("alice", destination.Name);
        Assert.Equal(["a", "b"], destination.Values);
    }

    [Fact]
    public void AddSyZeroAutoMapper_WithConfigActionAndNoAssemblies_AutoScansProfiles()
    {
        var services = new ServiceCollection();
        services.AddSyZeroAutoMapper(cfg => cfg.AllowNullCollections = true);

        using var provider = services.BuildServiceProvider();
        var objectMapper = provider.GetRequiredService<IObjectMapper>();

        var destination = objectMapper.Map<Destination>(new Source
        {
            Id = 9,
            Name = "bob",
            Values = null
        });

        Assert.Equal(9, destination.Id);
        Assert.Equal("bob", destination.Name);
        Assert.Null(destination.Values);
    }

    [Fact]
    public void AddSyZeroAutoMapper_ReplacesExistingObjectMapperRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IObjectMapper, DefaultObjectMapper>();

        services.AddSyZeroAutoMapper(typeof(TestProfile).Assembly);

        var registrations = services.Where(descriptor => descriptor.ServiceType == typeof(IObjectMapper)).ToList();

        Assert.Single(registrations);
        Assert.Equal(typeof(SyZero.AutoMapper.ObjectMapper), registrations[0].ImplementationType);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<SyZero.AutoMapper.ObjectMapper>(provider.GetRequiredService<IObjectMapper>());
    }

    private sealed class TestProfile : Profile
    {
        public TestProfile()
        {
            CreateMap<Source, Destination>();
        }
    }

    private sealed class Source
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<string>? Values { get; set; }
    }

    private sealed class Destination
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<string>? Values { get; set; }
    }
}
