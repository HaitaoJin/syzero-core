# SyZero

SyZero 是一个轻量级的 .NET 微服务框架核心库，提供依赖注入、基础领域模型、服务管理和轻量级事件总线等通用能力。

## 📦 安装

```bash
dotnet add package SyZero
```

## ✨ 特性

- 🚀 **依赖注入** - 基于 `Microsoft.Extensions.DependencyInjection` 的约定注册
- 🧩 **应用基础设施** - `SyZeroServiceBase`、`ApplicationService`、会话与权限基础能力
- 💾 **仓储与工作单元接口** - 统一抽象，方便接入 EF Core、SqlSugar、MongoDB
- 🌐 **轻量级服务管理** - 内置 `LocalServiceManagement`、`DBServiceManagement`
- 📣 **轻量级事件总线** - 内置 `LocalEventBus`、`DBEventBus`
- 🔧 **配置与工具** - 统一配置读取、异常模型、常用扩展工具

---

## 🚀 快速开始

### 1. 注册核心服务

```csharp
using SyZero;

var builder = WebApplication.CreateBuilder(args);

builder.AddSyZero();
builder.Services.AddControllers();

var app = builder.Build();

app.UseSyZero();
app.MapControllers();

app.Run();
```

### 2. 使用约定注入

```csharp
using SyZero.Dependency;

public interface IUserService
{
    Task<string> GetNameAsync(long id);
}

public class UserService : IUserService, IScopedDependency
{
    public Task<string> GetNameAsync(long id)
    {
        return Task.FromResult($"user-{id}");
    }
}
```

### 3. 继承应用服务基类

```csharp
using SyZero.Application.Service;

public class UserAppService : ApplicationService
{
    public string GetCurrentUser()
    {
        return SySession.UserName ?? "anonymous";
    }
}
```

---

## 📖 轻量级服务管理

SyZero 核心包内置两种无需额外中间件的服务管理实现：

| 实现 | 场景 | 特点 |
|------|------|------|
| `LocalServiceManagement` | 单机 / 开发环境 | 基于本地文件，无外部依赖 |
| `DBServiceManagement` | 简单多实例部署 | 基于数据库，支持健康检查和 Leader 选举 |

示例：

```csharp
builder.Services.AddLocalServiceManagement(options =>
{
    options.EnableHealthCheck = true;
    options.EnableLeaderElection = false;
});
```

---

## 📖 轻量级事件总线

SyZero 核心包内置两种事件总线实现：

| 实现 | 场景 | 特点 |
|------|------|------|
| `LocalEventBus` | 进程内事件 | 基于内存，适合单体应用 |
| `DBEventBus` | 需要持久化与重试 | 基于数据库，适合简单分布式场景 |

示例：

```csharp
using SyZero.EventBus;

builder.Services.AddLocalEventBus();

public class OrderCreatedEvent : EventBase
{
    public long OrderId { get; set; }
}

public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    public Task HandleAsync(OrderCreatedEvent @event)
    {
        Console.WriteLine($"order created: {@event.OrderId}");
        return Task.CompletedTask;
    }
}
```

发布事件：

```csharp
public class OrderService
{
    private readonly IEventBus _eventBus;

    public OrderService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<OrderCreatedEvent, OrderCreatedHandler>(() => new OrderCreatedHandler());
    }

    public Task CreateAsync(long orderId)
    {
        return _eventBus.PublishAsync(new OrderCreatedEvent
        {
            OrderId = orderId
        });
    }
}
```

---

## ⚠️ 说明

1. `builder.AddSyZero()` 会注册核心依赖，并在 `app.UseSyZero()` 时初始化全局运行时。
2. `ISySession` 是请求作用域对象，应只在请求链路内访问。
3. 轻量级事件总线和服务管理适合简单场景；需要更强可靠性时，建议使用 `SyZero.RabbitMQ`、`SyZero.Redis`、`SyZero.Consul`、`SyZero.Nacos` 等模块。

---

## 📄 许可证

MIT License - 详见 [LICENSE](../../LICENSE)

