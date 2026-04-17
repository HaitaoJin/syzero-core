using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Refit;
using SyZero.Application.Routing;
using SyZero.Application.Service;
using SyZero.Feign;
using SyZero.Feign.Proxy;
using SyZero.Runtime.Security;
using SyZero.Runtime.Session;
using SyZero.Serialization;
using SyZero.Service;
using SyZero.Util;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class FeignTests
{
    [Fact]
    public async Task RequestFeignHandler_BuildsGatewayPath_WithoutDuplicateSlashes()
    {
        var innerHandler = new CaptureHandler();
        var handler = new RequestFeignHandler("sample-service", innerHandler);
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/orders/list");
        request.Options.Set(new HttpRequestOptionsKey<TypeInfo>(HttpRequestMessageOptions.InterfaceType), typeof(OrderClient).GetTypeInfo());

        await client.SendAsync(request);

        Assert.Equal("/api/sample-service/orders/orders/list", innerHandler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task RequestFeignHandler_StripsApiPrefix_CaseInsensitively()
    {
        var innerHandler = new CaptureHandler();
        var handler = new RequestFeignHandler("sample-service", innerHandler);
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/api/orders/list");

        Assert.Equal("/orders/list", innerHandler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task AuthenticationFeignHandler_PreservesExistingAuthorizationHeader()
    {
        var previousProvider = SyZeroUtil.ServiceProvider;
        var services = new ServiceCollection();
        services.AddScoped<ISySession>(_ => new TestSySession("session-token"));

        using var provider = services.BuildServiceProvider();
        using var client = new HttpClient(new AuthenticationFeignHandler("sample-service", new CaptureHandler()));
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test")
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", "external-token")
            }
        };

        try
        {
            SyZeroUtil.ServiceProvider = provider;

            await client.SendAsync(request);

            Assert.Equal("external-token", request.Headers.Authorization?.Parameter);
        }
        finally
        {
            SyZeroUtil.ServiceProvider = previousProvider;
        }
    }

    [Fact]
    public async Task AuthenticationFeignHandler_SkipsBlankToken()
    {
        var previousProvider = SyZeroUtil.ServiceProvider;
        var services = new ServiceCollection();
        services.AddScoped<ISySession>(_ => new TestSySession(" "));

        using var provider = services.BuildServiceProvider();
        using var client = new HttpClient(new AuthenticationFeignHandler("sample-service", new CaptureHandler()));
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/test");

        try
        {
            SyZeroUtil.ServiceProvider = provider;

            await client.SendAsync(request);

            Assert.Null(request.Headers.Authorization);
        }
        finally
        {
            SyZeroUtil.ServiceProvider = previousProvider;
        }
    }

    [Fact]
    public async Task ResponseFeignHandler_UnwrapsSuccessfulEnvelope()
    {
        var handler = new ResponseFeignHandler("sample-service", new StaticResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":1,\"data\":{\"id\":7}}", System.Text.Encoding.UTF8, "application/json")
            }));
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://localhost/test");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"id\":7}", body);
    }

    [Fact]
    public async Task ResponseFeignHandler_ConvertsFailedEnvelopeToServerError()
    {
        var handler = new ResponseFeignHandler("sample-service", new StaticResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":0,\"msg\":\"failed\"}", System.Text.Encoding.UTF8, "application/json")
            }));
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://localhost/test");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public void FeignProxyFactoryManager_RegistersBuiltInFactories_AndAllowsOverrides()
    {
        var manager = new FeignProxyFactoryManager();

        Assert.True(manager.IsProtocolRegistered(FeignProtocol.Http));
        Assert.True(manager.IsProtocolRegistered(FeignProtocol.Grpc));
        Assert.IsType<HttpProxyFactory>(manager.GetFactory(FeignProtocol.Http));
        Assert.IsType<GrpcProxyFactory>(manager.GetFactory(FeignProtocol.Grpc));

        var customFactory = new StubFeignProxyFactory(FeignProtocol.Http);
        manager.RegisterFactory(customFactory);

        Assert.Same(customFactory, manager.GetFactory(FeignProtocol.Http));
    }

    [Fact]
    public void FeignServiceRegistrar_CreateEffectiveService_AppliesGlobalDefaults_WithoutMutatingOriginal()
    {
        var service = new FeignService
        {
            ServiceName = "svc",
            DllName = "Sample.Contracts"
        };
        var global = new ServiceSetting
        {
            Protocol = FeignProtocol.Grpc,
            Strategy = "Random",
            Retry = 2,
            Timeout = 45,
            EnableSsl = true,
            MaxMessageSize = 4096
        };

        var result = (FeignService)InvokePrivateStaticMethod(
            GetFeignRegistrarType(),
            "CreateEffectiveService",
            service,
            global)!;

        Assert.Equal(FeignProtocol.Grpc, result.Protocol);
        Assert.Equal("Random", result.Strategy);
        Assert.Equal(2, result.Retry);
        Assert.Equal(45, result.Timeout);
        Assert.True(result.EnableSsl);
        Assert.Equal(4096, result.MaxMessageSize);

        Assert.Equal(FeignProtocol.Http, service.Protocol);
        Assert.Null(service.Strategy);
        Assert.Equal(0, service.Retry);
        Assert.Equal(30, service.Timeout);
        Assert.False(service.EnableSsl);
        Assert.Equal(0, service.MaxMessageSize);
    }

    [Fact]
    public void FeignServiceRegistrar_GetServiceEndpoint_UsesServiceInstanceAndRegistryProtocol()
    {
        var serviceManagement = new Mock<IServiceManagement>(MockBehavior.Strict);
        serviceManagement
            .Setup(x => x.GetServiceInstance("svc"))
            .ReturnsAsync(new ServiceInfo
            {
                ServiceName = "svc",
                ServiceAddress = "127.0.0.1",
                ServicePort = 8443,
                ServiceProtocol = ProtocolType.HTTPS
            });

        var endpoint = (string)InvokePrivateStaticMethod(
            GetFeignRegistrarType(),
            "GetServiceEndpoint",
            serviceManagement.Object,
            new FeignService
            {
                ServiceName = "svc",
                DllName = "Sample.Contracts",
                Protocol = FeignProtocol.Http,
                EnableSsl = false
            })!;

        Assert.Equal("https://127.0.0.1:8443", endpoint);
        serviceManagement.Verify(x => x.GetServiceInstance("svc"), Times.Once);
        serviceManagement.Verify(x => x.GetService(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HttpProxyFactory_CreateRefitSettings_ReturnsExceptionsInsteadOfSwallowingFailures()
    {
        var method = typeof(HttpProxyFactory).GetMethod("CreateRefitSettings", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(HttpProxyFactory).FullName, "CreateRefitSettings");
        var settings = (RefitSettings)method.Invoke(new HttpProxyFactory(), [new DefaultJsonSerialize()])!;

        Assert.NotNull(settings.DeserializationExceptionFactory);
        var deserializationException = await settings.DeserializationExceptionFactory!(
            new HttpResponseMessage(HttpStatusCode.OK),
            new FormatException("bad json"));
        Assert.IsType<InvalidOperationException>(deserializationException);
        Assert.IsType<FormatException>(deserializationException!.InnerException);

        Assert.NotNull(settings.ExceptionFactory);
        var clientError = await settings.ExceptionFactory!(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"bad-request\"}", System.Text.Encoding.UTF8, "application/json")
            });
        Assert.IsType<HttpRequestException>(clientError);

        var serverError = await settings.ExceptionFactory!(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"code\":0,\"msg\":\"failed\"}", System.Text.Encoding.UTF8, "application/json")
            });
        Assert.IsType<SyMessageException>(serverError);
    }

    private static object? InvokePrivateStaticMethod(Type type, string methodName, params object[] parameters)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, methodName);
        return method.Invoke(null, parameters);
    }

    private static Type GetFeignRegistrarType()
    {
        return typeof(SyZeroFeignExtension).Assembly.GetType("SyZero.Feign.FeignServiceRegistrar", throwOnError: true)!;
    }

    [Api("orders")]
    private interface OrderClient : IApplicationService
    {
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StaticResponseHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    private sealed class StubFeignProxyFactory : IFeignProxyFactory
    {
        public StubFeignProxyFactory(FeignProtocol protocol)
        {
            Protocol = protocol;
        }

        public FeignProtocol Protocol { get; }

        public object CreateProxy(Type targetType, string endPoint, FeignService feignService, IJsonSerialize jsonSerialize)
        {
            return new object();
        }
    }

    private sealed class TestSySession : ISySession
    {
        public TestSySession(string? token)
        {
            Token = token!;
        }

        public System.Security.Claims.ClaimsPrincipal Principal { get; } = new(new System.Security.Claims.ClaimsIdentity());

        public long? UserId => null;

        public string UserRole => string.Empty;

        public string UserName => string.Empty;

        public List<string> Permission => [];

        public string Token { get; }

        public ISySession Parse(System.Security.Claims.ClaimsPrincipal claimsPrincipal)
        {
            throw new NotSupportedException();
        }

        public ISySession Parse(string token)
        {
            throw new NotSupportedException();
        }
    }
}
