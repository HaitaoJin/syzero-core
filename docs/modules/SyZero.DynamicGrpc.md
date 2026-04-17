# SyZero.DynamicGrpc

SyZero 的动态 gRPC 模块，基于 `protobuf-net.Grpc.AspNetCore` 自动注册 Code-First gRPC 服务。

## 安装

```bash
dotnet add package SyZero.DynamicGrpc
```

## 使用

### 1. 定义应用服务

```csharp
using SyZero.Application.Attributes;
using SyZero.Application.Service;

[DynamicApi]
public interface IUserAppService : IDynamicApi
{
    Task<UserDto> GetUserAsync(long id);
}

public class UserAppService : IUserAppService
{
    public Task<UserDto> GetUserAsync(long id)
    {
        return Task.FromResult(new UserDto { Id = id });
    }
}
```

### 2. 注册服务

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddSyZero();

// 方式 1：扫描当前已加载的业务程序集
builder.Services.AddDynamicGrpc();

// 方式 2：只扫描指定程序集
builder.Services.AddDynamicGrpc(typeof(UserAppService).Assembly);

// 方式 3：结合配置进一步覆盖选项
builder.Services.AddDynamicGrpc(options =>
{
    options.EnableDetailedErrors = true;
    options.MaxReceiveMessageSize = 8 * 1024 * 1024;
});
```

### 3. 映射端点

```csharp
var app = builder.Build();

app.MapDynamicGrpcServices();

app.Run();
```

## 配置

```json
{
  "DynamicGrpc": {
    "MaxReceiveMessageSize": 4194304,
    "MaxSendMessageSize": 4194304,
    "EnableDetailedErrors": false
  }
}
```

可用配置项：

| 属性 | 类型 | 说明 |
|------|------|------|
| `MaxReceiveMessageSize` | `int?` | 最大接收消息大小，单位字节 |
| `MaxSendMessageSize` | `int?` | 最大发送消息大小，单位字节 |
| `EnableDetailedErrors` | `bool` | 是否输出详细错误信息 |

## 约束

- 服务实现类型必须是 `public`、非抽象、非泛型类。
- 服务必须实现 `IDynamicApi`，并通过 `[DynamicApi]` 启用。
- 标注 `[NonDynamicApi]` 或 `[NonGrpcService]` 的服务不会注册为 gRPC 服务。
- 标记为 `IFallback` 的类型不会被注册。

## 许可证

MIT License

