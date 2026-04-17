# SyZero.DynamicWebApi

SyZero 框架的动态 Web API 模块，支持自动生成 RESTful API。

## 📦 安装

```bash
dotnet add package SyZero.DynamicWebApi
```

## ✨ 特性

- 🚀 **动态生成** - 根据应用服务自动生成 Web API
- 🎯 **RESTful** - 自动映射为 RESTful 风格
- 📖 **Swagger** - 自动生成 API 文档

---

## 🚀 快速开始

### 1. 配置 appsettings.json

```json
{
  "DynamicWebApi": {
    "DefaultAreaName": "api",
    "DefaultHttpVerb": "POST"
  }
}
```

### 2. 注册服务

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
// 添加SyZero
builder.AddSyZero();

// 注册服务方式1 - 使用默认配置
builder.Services.AddDynamicWebApi();

// 注册服务方式2 - 使用委托配置
builder.Services.AddDynamicWebApi(options =>
{
    options.DefaultAreaName = "api";
    options.DefaultHttpVerb = "POST";
});

// 注册服务方式3 - 指定服务程序集
builder.Services.AddDynamicWebApi(typeof(UserAppService).Assembly);

var app = builder.Build();
// 使用SyZero
app.UseSyZero();
app.MapControllers();
app.Run();
```

### 3. 使用示例

```csharp
public interface IUserAppService : IApplicationService
{
    Task<UserDto> GetAsync(long id);
    Task<UserDto> CreateAsync(CreateUserInput input);
    Task<UserDto> UpdateAsync(long id, UpdateUserInput input);
    Task DeleteAsync(long id);
}

public class UserAppService : IUserAppService
{
    // 实现方法
    // 自动生成:
    // GET    /api/user/{id}
    // POST   /api/user
    // PUT    /api/user/{id}
    // DELETE /api/user/{id}
}
```

---

## 📖 配置选项

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `DefaultAreaName` | `string` | `"api"` | 默认区域名称 |
| `DefaultHttpVerb` | `string` | `"POST"` | 默认 HTTP 方法 |
| `RemoveActionPostfixes` | `string[]` | `["Async"]` | 移除的方法后缀 |

---

## 📖 API 说明

### 方法命名约定

| 方法前缀 | HTTP 方法 |
|------|------|
| `Get/Find/Fetch/Query` | GET |
| `Create/Add/Insert` | POST |
| `Update/Modify/Edit` | PUT |
| `Delete/Remove` | DELETE |

> 方法名自动映射为对应的 HTTP 方法

---

## 🔧 高级用法

### 自定义路由

```csharp
[DynamicWebApi]
[Route("api/v2/[controller]")]
public class UserAppService : IUserAppService
{
    [HttpGet("{id}")]
    public async Task<UserDto> GetAsync(long id)
    {
        // 实现逻辑
    }
}
```

### 禁用特定方法

```csharp
[NonDynamicWebApi]
public async Task InternalMethodAsync()
{
    // 此方法不会暴露为 API
}
```

---

## ⚠️ 注意事项

1. **接口定义** - 服务必须实现 IApplicationService 接口
2. **命名约定** - 遵循命名约定以正确映射 HTTP 方法
3. **特性覆盖** - 可以使用特性覆盖自动生成的路由

---

## 📄 许可证

MIT License - 详见 [LICENSE](../../LICENSE)

