using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Security.Claims;
using SyZero.AspNetCore;
using SyZero.AspNetCore.Controllers;
using SyZero.Cache;
using SyZero.ObjectMapper;
using SyZero.Runtime.Security;
using SyZero.Runtime.Session;
using Xunit;

namespace SyZero.Tests;

public class AspNetCoreTests
{
    [Fact]
    public void AddSyZeroController_RegistersControllersAndHttpContextAccessor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IToken>(new TestToken(CreatePrincipal(1)));

        services.AddSyZeroController();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<HealthController>());
        Assert.NotNull(provider.GetService<IHttpContextAccessor>());
        Assert.NotNull(provider.GetService<SyZero.AspNetCore.Middleware.SyAuthMiddleware>());
    }

    [Fact]
    public void SyZeroController_ResolvesServicesFromRequestServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IObjectMapper, DefaultObjectMapper>();
        services.AddSingleton<ICache>(new DictionaryCache());
        services.AddScoped<ISySession, TestSySession>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = scope.ServiceProvider
                }
            }
        };

        Assert.Same(scope.ServiceProvider.GetRequiredService<ICache>(), controller.Cache);
        Assert.Same(scope.ServiceProvider.GetRequiredService<IObjectMapper>(), controller.ObjectMapper);
        Assert.Same(scope.ServiceProvider.GetRequiredService<ISySession>(), controller.SySession);
        Assert.NotNull(controller.Logger);
    }

    [Fact]
    public void RouteConvention_AppliesPrefixToAttributedAndUnattributedSelectors()
    {
        var convention = new RouteConvention(new RouteAttribute("api"));
        var controller = new ControllerModel(typeof(TestController).GetTypeInfo(), Array.Empty<object>());
        controller.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute("values"))
        });
        controller.Selectors.Add(new SelectorModel());

        var application = new ApplicationModel();
        application.Controllers.Add(controller);

        convention.Apply(application);

        Assert.Equal("api/values", controller.Selectors[0].AttributeRouteModel!.Template);
        Assert.Equal("api", controller.Selectors[1].AttributeRouteModel!.Template);
    }

    [Fact]
    public async Task UseSyAuthMiddleware_WithoutCacheRegistration_PreservesAuthenticatedPrincipal()
    {
        var principal = CreatePrincipal(7);
        var services = new ServiceCollection();
        services.AddSingleton<IToken>(new TestToken(principal));
        services.AddScoped<IMiddlewareFactory, MiddlewareFactory>();
        services.AddScoped<ISySession, TestSySession>();
        services.AddScoped<SyZero.AspNetCore.Middleware.SyAuthMiddleware>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var builder = new ApplicationBuilder(provider);
        builder.UseSyAuthMiddleware(session => $"Token:{session.UserId}");

        ClaimsPrincipal? observedUser = null;
        TestSySession? observedSession = null;
        builder.Run(context =>
        {
            observedUser = context.User;
            observedSession = context.RequestServices.GetRequiredService<ISySession>() as TestSySession;
            return Task.CompletedTask;
        });

        var app = builder.Build();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        context.Request.Headers.Authorization = "Bearer valid-token";

        await app(context);

        Assert.Same(principal, observedUser);
        Assert.NotNull(observedSession);
        Assert.Same(principal, observedSession!.Principal);
    }

    [Fact]
    public async Task UseSyAuthMiddleware_WhenCacheMisses_ClearsAuthenticatedPrincipal()
    {
        var principal = CreatePrincipal(9);
        var cache = new DictionaryCache();
        var services = new ServiceCollection();
        services.AddSingleton<IToken>(new TestToken(principal));
        services.AddSingleton<ICache>(cache);
        services.AddScoped<IMiddlewareFactory, MiddlewareFactory>();
        services.AddScoped<ISySession, TestSySession>();
        services.AddScoped<SyZero.AspNetCore.Middleware.SyAuthMiddleware>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var builder = new ApplicationBuilder(provider);
        builder.UseSyAuthMiddleware(session => $"Token:{session.UserId}");

        ClaimsPrincipal? observedUser = null;
        builder.Run(context =>
        {
            observedUser = context.User;
            return Task.CompletedTask;
        });

        var app = builder.Build();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        context.Request.Headers.Authorization = "Bearer valid-token";

        await app(context);

        Assert.NotNull(observedUser);
        Assert.False(observedUser!.Identity?.IsAuthenticated ?? false);
        Assert.Empty(observedUser.Claims);
    }

    private static ClaimsPrincipal CreatePrincipal(long userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(SyClaimTypes.UserId, userId.ToString())
        ], "Bearer"));
    }

    private sealed class TestController : SyZeroController
    {
    }

    private sealed class TestToken : IToken
    {
        private readonly ClaimsPrincipal _principal;

        public TestToken(ClaimsPrincipal principal)
        {
            _principal = principal;
        }

        public ClaimsPrincipal GetPrincipal(string token)
        {
            return token == "valid-token" ? _principal : null!;
        }

        public string CreateAccessToken(IEnumerable<Claim> claims)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestSySession : ISySession
    {
        public ClaimsPrincipal Principal { get; private set; } = new ClaimsPrincipal(new ClaimsIdentity());

        public long? UserId
        {
            get
            {
                var value = Principal.FindFirst(SyClaimTypes.UserId)?.Value;
                return long.TryParse(value, out var parsed) ? parsed : null;
            }
        }

        public string UserRole => Principal.FindFirst(SyClaimTypes.UserRole)?.Value!;

        public string UserName => Principal.FindFirst(SyClaimTypes.UserName)?.Value!;

        public List<string> Permission => [];

        public string Token => Principal.FindFirst(SyClaimTypes.Token)?.Value!;

        public ISySession Parse(ClaimsPrincipal claimsPrincipal)
        {
            Principal = claimsPrincipal;
            return this;
        }

        public ISySession Parse(string token)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class DictionaryCache : ICache
    {
        private readonly Dictionary<string, object?> _values = new();

        public bool Exist(string key) => _values.ContainsKey(key);

        public string[] GetKeys(string pattern) => _values.Keys.ToArray();

        public T Get<T>(string key) => _values.TryGetValue(key, out var value) ? (T)value! : default!;

        public void Remove(string key) => _values.Remove(key);

        public Task RemoveAsync(string key)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key) => Task.CompletedTask;

        public void Set<T>(string key, T value, int exprireTime = 86400) => _values[key] = value;

        public Task SetAsync<T>(string key, T value, int exprireTime = 86400)
        {
            Set(key, value, exprireTime);
            return Task.CompletedTask;
        }
    }
}
