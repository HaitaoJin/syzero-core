using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using RestSharp;
using SyZero.Client;
using SyZero.Util;
using SyZero.Web.Common;
using SyZero.Web.Common.Util;
using Xunit;

namespace SyZero.Tests;

public class SyZeroWebCommonTests
{
    [Fact]
    public void LongToStrConverter_SupportsUInt64Values()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new LongToStrConverter());
        var payload = new LongValueDto { Value = ulong.MaxValue };

        var json = JsonConvert.SerializeObject(payload, settings);
        var result = JsonConvert.DeserializeObject<LongValueDto>(json, settings);

        Assert.Contains($"\"Value\":\"{ulong.MaxValue}\"", json);
        Assert.NotNull(result);
        Assert.Equal(ulong.MaxValue, result!.Value);
    }

    [Fact]
    public void AliasMethod_Initialization_DoesNotMutateSourceProbabilities()
    {
        var source = new List<double> { 1, 3, 6 };
        var original = source.ToArray();
        var aliasMethod = new AliasMethod();

        aliasMethod.Initialization(source);

        Assert.Equal(original, source);
        Assert.InRange(aliasMethod.Next(), 0, source.Count - 1);
    }

    [Fact]
    public void AliasMethod_Next_ThrowsBeforeInitialization()
    {
        var aliasMethod = new AliasMethod();

        Assert.Throws<InvalidOperationException>(() => aliasMethod.Next());
    }

    [Fact]
    public void XmlSerialize_AppendChild_ReturnsFalse_WhenSourceNodesMissing()
    {
        var xmlSerialize = new XmlSerialize();
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPath);
        var sourcePath = Path.Combine(tempPath, "source.xml");
        var targetPath = Path.Combine(tempPath, "target.xml");

        try
        {
            File.WriteAllText(sourcePath, "<root />", Encoding.UTF8);
            File.WriteAllText(targetPath, "<root><items /></root>", Encoding.UTF8);

            Assert.False(xmlSerialize.AppendChild(sourcePath, "/root/missing", targetPath, "/root/items"));
            Assert.False(xmlSerialize.UpdateNodeInnerText(targetPath, "/root/missing", "value"));
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
        }
    }

    [Fact]
    public void RestHelper_Execute_ReturnsNull_WhenResponseContentIsEmpty()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            }));

        using var provider = CreateProvider(handler);
        using var scope = SyZeroUtil.BeginScope(provider);

        var result = RestHelper.Execute(new RestRequest("https://example.test/ping", Method.Get));

        Assert.Null(result);
    }

    [Fact]
    public async Task HttpRestClient_ExecuteAsync_DoesNotSendBodyForGetRequests()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":1,\"data\":\"ok\"}", Encoding.UTF8, "application/json")
            }));

        using var provider = CreateProvider(handler);
        using var scope = SyZeroUtil.BeginScope(provider);
        var client = new HttpRestClient();
        var template = new RequestTemplate(HttpMethod.Get, "https://example.test/items")
        {
            Headers = null!,
            QueryValue = null!
        };

        var response = await client.ExecuteAsync<string>(template, CancellationToken.None);

        Assert.Null(handler.LastBody);
        Assert.Equal(SyMessageBoxStatus.Success, response.Code);
        Assert.Equal("ok", response.Body);
    }

    [Fact]
    public async Task HttpRestClient_ExecuteAsync_SendsSerializedJsonBodyWithoutDoubleEncoding()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":1,\"data\":\"accepted\"}", Encoding.UTF8, "application/json")
            }));

        using var provider = CreateProvider(handler);
        using var scope = SyZeroUtil.BeginScope(provider);
        var client = new HttpRestClient();

        var response = await client.ExecuteAsync<string>(new RequestTemplate(HttpMethod.Post, "https://example.test/items")
        {
            Body = "{\"name\":\"alice\"}"
        }, CancellationToken.None);

        Assert.Equal("{\"name\":\"alice\"}", handler.LastBody);
        Assert.Equal("accepted", response.Body);
    }

    [Fact]
    public async Task HttpRestClient_ExecuteAsync_HandlesEmptySuccessResponses()
    {
        var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            }));

        using var provider = CreateProvider(handler);
        using var scope = SyZeroUtil.BeginScope(provider);
        var client = new HttpRestClient();

        var response = await client.ExecuteAsync<object>(new RequestTemplate(HttpMethod.Post, "https://example.test/items")
        {
            Body = "{}"
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
        Assert.Null(response.Body);
        Assert.Null(response.Msg);
    }

    [Fact]
    public void SyEncode_Encrypt_ThrowsArgumentOutOfRangeException_ForUnsupportedType()
    {
        var encoder = new SyEncode();

        Assert.Throws<ArgumentOutOfRangeException>(() => encoder.Encrypt("text", "12345678", (EncryptType)999));
    }

    private static ServiceProvider CreateProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new RestClient(new HttpClient(handler)));
        return services.BuildServiceProvider();
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return await _handler(request, cancellationToken);
        }
    }

    private sealed class LongValueDto
    {
        public ulong Value { get; set; }
    }
}
