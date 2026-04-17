using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SyZero.Runtime.Security;
using SyZero.Runtime.Session;
using SyZero.Serialization;
using SyZero.Util;
using Xunit;

namespace SyZero.Tests;

[Collection("AppConfig")]
public class SyZeroCoreTests
{
    [Fact]
    public void AppConfig_ServerOptions_ReturnsDefaultInstance_WhenSectionMissing()
    {
        AppConfig.Configuration = new ConfigurationBuilder().Build();
        ResetAppConfigCache("serverOptions");

        var options = AppConfig.ServerOptions;

        Assert.NotNull(options);
        Assert.Equal(ProtocolType.HTTP, options.Protocol);
        Assert.Null(options.Name);
    }

    [Fact]
    public void SySession_ParseClaimsPrincipal_HandlesMissingIdentity_AndReadsClaims()
    {
        var session = (ISySession)new SySession(new DefaultJsonSerialize());

        session.Parse(new ClaimsPrincipal());

        Assert.Null(session.UserId);
        Assert.Null(session.UserRole);
        Assert.Null(session.UserName);
        Assert.Null(session.Permission);
        Assert.Null(session.Token);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(SyClaimTypes.UserId, "42"),
            new Claim(SyClaimTypes.UserRole, "admin"),
            new Claim(SyClaimTypes.UserName, "alice"),
            new Claim(SyClaimTypes.Permission, "[\"read\",\"write\"]"),
            new Claim(SyClaimTypes.Token, "jwt-token")
        }, "test"));

        session.Parse(principal);

        Assert.Equal(42, session.UserId);
        Assert.Equal("admin", session.UserRole);
        Assert.Equal("alice", session.UserName);
        Assert.Equal(new[] { "read", "write" }, session.Permission);
        Assert.Equal("jwt-token", session.Token);
    }

    [Fact]
    public void SySession_ParseToken_UsesRegisteredTokenService()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(SyClaimTypes.UserId, "7")
        }, "token"));

        var tokenMock = new Mock<IToken>();
        tokenMock.Setup(token => token.GetPrincipal("access-token")).Returns(principal);

        var services = new ServiceCollection();
        services.AddSingleton(tokenMock.Object);

        using var provider = services.BuildServiceProvider();
        var previousProvider = SyZeroUtil.ServiceProvider;

        try
        {
            SyZeroUtil.ServiceProvider = provider;
            var session = new SySession(new DefaultJsonSerialize());

            session.Parse("access-token");

            Assert.Equal(7, session.UserId);
            tokenMock.Verify(token => token.GetPrincipal("access-token"), Times.Once);
        }
        finally
        {
            SyZeroUtil.ServiceProvider = previousProvider;
        }
    }

    private static void ResetAppConfigCache(string fieldName)
    {
        var field = typeof(AppConfig).GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AppConfig).FullName, fieldName);
        field.SetValue(null, null);
    }
}
