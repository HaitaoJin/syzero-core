# SyZero.Web.Common

SyZero 框架的 Web 通用组件模块，提供 JWT 认证、响应包装等功能。

## 📦 安装

```bash
dotnet add package SyZero.Web.Common
```

## ✨ 特性

- 🔒 **JWT 认证** - 完整的 JWT Token 生成和验证
- 📦 **统一响应** - 统一的 API 响应格式
- 🎯 **请求上下文** - 当前用户和请求上下文管理
- ⚠️ **异常过滤器** - 统一的异常处理

---

## 🚀 快速开始

### 1. 配置 appsettings.json

```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-at-least-32-characters",
    "Issuer": "SyZero",
    "Audience": "SyZero",
    "ExpireMinutes": 120
  }
}
```

### 2. 注册服务

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
// 添加SyZero
builder.AddSyZero();

// 注册服务方式1 - 使用配置文件
builder.Services.AddSyZeroWebCommon();

// 注册服务方式2 - 使用委托配置
builder.Services.AddSyZeroWebCommon(options =>
{
    options.SecretKey = "your-secret-key-at-least-32-characters";
    options.ExpireMinutes = 120;
});

// 注册服务方式3 - 禁用某些功能
builder.Services.AddSyZeroWebCommon(options =>
{
    options.EnableJwt = true;
    options.EnableExceptionFilter = true;
});

var app = builder.Build();
// 使用SyZero
app.UseSyZero();
// 使用认证
app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

### 3. 使用示例

```csharp
// 生成 Token
public class AuthService
{
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(IJwtTokenGenerator tokenGenerator)
    {
        _tokenGenerator = tokenGenerator;
    }

    public string GenerateToken(User user)
    {
        return _tokenGenerator.GenerateToken(new Dictionary<string, string>
        {
            ["userId"] = user.Id.ToString(),
            ["userName"] = user.Name
        });
    }
}

// 获取当前用户
public class UserService
{
    private readonly ICurrentUser _currentUser;

    public UserService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public long GetCurrentUserId()
    {
        return _currentUser.Id ?? throw new UnauthorizedAccessException();
    }
}
```

---

## 📖 配置选项

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `SecretKey` | `string` | `""` | JWT 密钥（至少32字符） |
| `Issuer` | `string` | `""` | 签发者 |
| `Audience` | `string` | `""` | 接收者 |
| `ExpireMinutes` | `int` | `120` | 过期时间（分钟） |

---

## 📖 API 说明

### ICurrentUser 接口

| 属性/方法 | 说明 |
|------|------|
| `Id` | 当前用户 ID |
| `Name` | 当前用户名 |
| `IsAuthenticated` | 是否已认证 |
| `GetClaimValue(type)` | 获取指定声明值 |

### IJwtTokenGenerator 接口

| 方法 | 说明 |
|------|------|
| `GenerateToken(claims)` | 生成 JWT Token |
| `ValidateToken(token)` | 验证 Token |

> 使用标准的 ASP.NET Core 认证中间件

---

## 🔧 高级用法

### 自定义 Token 声明

```csharp
var token = _tokenGenerator.GenerateToken(new Dictionary<string, string>
{
    ["userId"] = user.Id.ToString(),
    ["role"] = user.Role,
    ["permissions"] = string.Join(",", user.Permissions)
});
```

### 刷新 Token

```csharp
public class AuthController : ControllerBase
{
    [HttpPost("refresh")]
    public async Task<TokenResult> RefreshAsync(string refreshToken)
    {
        // 验证 refresh token
        // 生成新的 access token
    }
}
```

---

## ⚠️ 注意事项

1. **密钥安全** - JWT 密钥必须保密且足够复杂
2. **Token 过期** - 合理设置 Token 过期时间
3. **中间件顺序** - Authentication 必须在 Authorization 之前

---

## 📄 许可证

MIT License - 详见 [LICENSE](../../LICENSE)

