using System.Reflection;
using System.Text;
using System.Xml.XPath;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SyZero.Swagger;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class SwaggerTests
{
    [Fact]
    public void AddSwagger_RegistersEndpointApiExplorer()
    {
        using var tempDirectory = new TempDirectory();
        var builder = CreateBuilder(tempDirectory.Path);

        builder.Services.AddSwagger(new SyZero.Swagger.SwaggerOptions
        {
            EnableJwtAuth = false,
            IncludeXmlComments = false
        });

        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IApiDescriptionProvider)
            && descriptor.ImplementationType?.Name == "EndpointMetadataApiDescriptionProvider");
    }

    [Fact]
    public void AddSwagger_LoadsXmlComments_ForOperationsAndSchemas()
    {
        using var tempDirectory = new TempDirectory();
        var xmlCommentsPath = Path.Combine(tempDirectory.Path, "SwaggerTests.xml");
        File.WriteAllText(xmlCommentsPath, CreateXmlCommentsDocument(), Encoding.UTF8);

        var builder = CreateBuilder(tempDirectory.Path);
        builder.Services.AddControllers()
            .PartManager.ApplicationParts.Add(new AssemblyPart(typeof(SwaggerXmlCommentsController).Assembly));
        builder.Services.AddSwagger(new SyZero.Swagger.SwaggerOptions
        {
            EnableJwtAuth = false,
            IncludeXmlComments = true,
            XmlCommentFiles = new List<string> { xmlCommentsPath }
        });

        using var app = builder.Build();
        app.MapControllers();

        var document = app.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>().GetSwagger("v1");
        var operation = document.Paths["/swagger-xml-comments/{id}"].Operations[Microsoft.OpenApi.Models.OperationType.Get];
        var schema = document.Components.Schemas[nameof(SwaggerXmlCommentsModel)];

        Assert.Equal("Gets a sample item.", operation.Summary);
        Assert.Equal("Returns the sample payload.", operation.Description);
        Assert.Equal("The route identifier.", Assert.Single(operation.Parameters).Description);
        Assert.Equal("Success response.", operation.Responses["200"].Description);
        Assert.Equal("Response payload.", schema.Description);
        Assert.Equal("The display name.", schema.Properties["name"].Description);
    }

    [Fact]
    public async Task TryExportSwaggerAsync_WritesSwaggerJson_WhenArgumentIsProvided()
    {
        using var tempDirectory = new TempDirectory();
        var builder = CreateBuilder(tempDirectory.Path);
        builder.Services.AddControllers()
            .PartManager.ApplicationParts.Add(new AssemblyPart(typeof(SwaggerXmlCommentsController).Assembly));

        builder.Services.AddSwagger(new SyZero.Swagger.SwaggerOptions
        {
            EnableJwtAuth = false,
            IncludeXmlComments = false
        });

        await using var app = builder.Build();
        app.MapControllers();

        var outputPath = Path.Combine(tempDirectory.Path, "artifacts", "swagger.json");
        var exported = await app.TryExportSwaggerAsync(["--swagger-output", outputPath]);
        var json = await File.ReadAllTextAsync(outputPath);

        Assert.True(exported);
        Assert.Contains("/swagger-xml-comments/{id}", json);
    }

    [Fact]
    public void TryGetSwaggerOutputPath_ReturnsFalse_WhenArgsAreNull()
    {
        var result = SwaggerExportExtensions.TryGetSwaggerOutputPath(null!, out var swaggerOutputPath);

        Assert.False(result);
        Assert.Equal(string.Empty, swaggerOutputPath);
    }

    [Theory]
    [MemberData(nameof(GetInvalidSwaggerOutputArguments))]
    public void TryGetSwaggerOutputPath_Throws_WhenPathIsMissing(string[] args)
    {
        Assert.Throws<InvalidOperationException>(() =>
            SwaggerExportExtensions.TryGetSwaggerOutputPath(args, out _));
    }

    [Fact]
    public void XmlCommentsOperation2Filter_ResolvesOverloadedMethods_OnConstructedGenericTypes()
    {
        var xmlDocument = new XPathDocument(new StringReader("<doc />"));
        var filter = new XmlCommentsOperation2Filter(xmlDocument);
        var helperMethod = typeof(XmlCommentsOperation2Filter).GetMethod(
            "GetGenericTypeMethodOrNullFor",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(XmlCommentsOperation2Filter).FullName, "GetGenericTypeMethodOrNullFor");
        var constructedMethod = typeof(GenericSwaggerHandler<int>).GetMethod(
            nameof(GenericSwaggerHandler<int>.Handle),
            [typeof(int)])
            ?? throw new MissingMethodException(typeof(GenericSwaggerHandler<int>).FullName, nameof(GenericSwaggerHandler<int>.Handle));

        var resolvedMethod = (MethodInfo?)helperMethod.Invoke(filter, [constructedMethod]);

        Assert.NotNull(resolvedMethod);
        Assert.Equal(typeof(GenericSwaggerHandler<>), resolvedMethod!.DeclaringType);
        Assert.True(resolvedMethod.GetParameters()[0].ParameterType.IsGenericParameter);
    }

    public static IEnumerable<object[]> GetInvalidSwaggerOutputArguments()
    {
        yield return new object[] { new[] { "--swagger-output" } };
        yield return new object[] { new[] { "--swagger-output", "" } };
        yield return new object[] { new[] { "--swagger-output", " " } };
        yield return new object[] { new[] { "--swagger-output=" } };
    }

    private static WebApplicationBuilder CreateBuilder(string contentRootPath)
    {
        return WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SwaggerTests).Assembly.FullName,
            ContentRootPath = contentRootPath,
            EnvironmentName = Environments.Development
        });
    }

    private static string CreateXmlCommentsDocument()
    {
        var actionMethod = typeof(SwaggerXmlCommentsController).GetMethod(nameof(SwaggerXmlCommentsController.Get))
            ?? throw new MissingMethodException(typeof(SwaggerXmlCommentsController).FullName, nameof(SwaggerXmlCommentsController.Get));
        var modelType = typeof(SwaggerXmlCommentsModel);
        var property = modelType.GetProperty(nameof(SwaggerXmlCommentsModel.Name))
            ?? throw new MissingMemberException(modelType.FullName, nameof(SwaggerXmlCommentsModel.Name));

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <doc>
              <members>
                <member name="{XmlCommentsMemberNameHelper.GetMemberNameForMethod(actionMethod)}">
                  <summary>Gets a sample item.</summary>
                  <remarks>Returns the sample payload.</remarks>
                  <param name="id">The route identifier.</param>
                  <response code="200">Success response.</response>
                </member>
                <member name="{XmlCommentsMemberNameHelper.GetMemberNameForType(modelType)}">
                  <summary>Response payload.</summary>
                </member>
                <member name="{XmlCommentsMemberNameHelper.GetMemberNameForMember(property)}">
                  <summary>The display name.</summary>
                </member>
              </members>
            </doc>
            """;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "syzero-swagger-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class GenericSwaggerHandler<T>
    {
        public void Handle(T value)
        {
        }

        public void Handle(string value)
        {
        }
    }
}

[ApiController]
[Route("swagger-xml-comments")]
public sealed class SwaggerXmlCommentsController : ControllerBase
{
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SwaggerXmlCommentsModel), Microsoft.AspNetCore.Http.StatusCodes.Status200OK)]
    public ActionResult<SwaggerXmlCommentsModel> Get(int id)
    {
        return new SwaggerXmlCommentsModel
        {
            Name = id.ToString()
        };
    }
}

public sealed class SwaggerXmlCommentsModel
{
    public string Name { get; set; } = string.Empty;
}
