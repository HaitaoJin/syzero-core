# SyZero

<p align="center">
  <img src="doc/icon/logo.png" alt="SyZero Logo" width="120"/>
</p>

<p align="center">
  <strong>一个轻量级、模块化的 .NET 微服务开发框架</strong>
</p>

<p align="center">
  <a href="https://github.com/HaitaoJin/syzero-core"><img src="https://img.shields.io/github/stars/HaitaoJin/syzero-core?style=flat-square" alt="GitHub Stars"/></a>
  <a href="https://github.com/HaitaoJin/syzero-core/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License"/></a>
  <a href="https://www.nuget.org/packages/SyZero"><img src="https://img.shields.io/nuget/v/SyZero?style=flat-square" alt="NuGet"/></a>
  <a href="https://docs.syzero.com"><img src="https://img.shields.io/badge/docs-docs.syzero.com-green?style=flat-square" alt="Documentation"/></a>
</p>

---

## ✨ 简介

SyZero 是一个基于 .NET 的模块化微服务开发框架，提供了丰富的组件和工具，帮助开发者快速构建高性能、可扩展的分布式应用程序。

## 🚀 核心特性

- 🎯 **模块化设计** - 按需引用，灵活组合
- 🌐 **服务治理** - Consul / Nacos 服务注册发现、负载均衡、健康检查
- 💾 **数据访问** - 支持 EF Core / SqlSugar / MongoDB，内置仓储模式
- ⚡ **高性能** - Redis 缓存、RabbitMQ 消息队列、OpenTelemetry 追踪
- 📝 **动态 API** - 自动生成 RESTful API / gRPC 服务和 Swagger 文档
- 🏗️ **DDD 支持** - 领域驱动设计模式与依赖注入

## 📦 核心模块

| 模块 | NuGet 包 | 说明 |
|------|----------|------|
| **SyZero** | [![NuGet](https://img.shields.io/nuget/v/SyZero?style=flat-square)](https://www.nuget.org/packages/SyZero) | 核心模块，提供基础功能和依赖注入 |
| **SyZero.AspNetCore** | [![NuGet](https://img.shields.io/nuget/v/SyZero.AspNetCore?style=flat-square)](https://www.nuget.org/packages/SyZero.AspNetCore) | ASP.NET Core 集成 |
| **SyZero.DynamicWebApi** | [![NuGet](https://img.shields.io/nuget/v/SyZero.DynamicWebApi?style=flat-square)](https://www.nuget.org/packages/SyZero.DynamicWebApi) | 动态 Web API 生成 |
| **SyZero.DynamicGrpc** | [![NuGet](https://img.shields.io/nuget/v/SyZero.DynamicGrpc?style=flat-square)](https://www.nuget.org/packages/SyZero.DynamicGrpc) | 动态 gRPC 服务生成 |
| **SyZero.Swagger** | [![NuGet](https://img.shields.io/nuget/v/SyZero.Swagger?style=flat-square)](https://www.nuget.org/packages/SyZero.Swagger) | Swagger API 文档 |

### 数据访问

| 模块 | NuGet 包 | 说明 |
|------|----------|------|
| **SyZero.EntityFrameworkCore** | [![NuGet](https://img.shields.io/nuget/v/SyZero.EntityFrameworkCore?style=flat-square)](https://www.nuget.org/packages/SyZero.EntityFrameworkCore) | Entity Framework Core 集成 (SQL Server/MySQL) |
| **SyZero.SqlSugar** | [![NuGet](https://img.shields.io/nuget/v/SyZero.SqlSugar?style=flat-square)](https://www.nuget.org/packages/SyZero.SqlSugar) | SqlSugar ORM 集成 |
| **SyZero.MongoDB** | [![NuGet](https://img.shields.io/nuget/v/SyZero.MongoDB?style=flat-square)](https://www.nuget.org/packages/SyZero.MongoDB) | MongoDB 数据库支持 |

### 缓存与消息

| 模块 | NuGet 包 | 说明 |
|------|----------|------|
| **SyZero.Redis** | [![NuGet](https://img.shields.io/nuget/v/SyZero.Redis?style=flat-square)](https://www.nuget.org/packages/SyZero.Redis) | Redis 缓存、服务管理与 Redis 事件总线 |
| **SyZero.RabbitMQ** | [![NuGet](https://img.shields.io/nuget/v/SyZero.RabbitMQ?style=flat-square)](https://www.nuget.org/packages/SyZero.RabbitMQ) | RabbitMQ 消息队列与事件总线 |

> 💡 **内置事件总线**：SyZero 核心模块还提供了 `LocalEventBus`（基于内存）和 `DBEventBus`（基于数据库）两种轻量级事件总线实现，适用于单体应用或简单分布式场景。

### 服务治理

| 模块 | NuGet 包 | 说明 |
|------|----------|------|
| **SyZero.Consul** | [![NuGet](https://img.shields.io/nuget/v/SyZero.Consul?style=flat-square)](https://www.nuget.org/packages/SyZero.Consul) | Consul 服务注册与发现 |
| **SyZero.Nacos** | [![NuGet](https://img.shields.io/nuget/v/SyZero.Nacos?style=flat-square)](https://www.nuget.org/packages/SyZero.Nacos) | Nacos 服务注册与配置中心 |
| **SyZero.ApiGateway** | [![NuGet](https://img.shields.io/nuget/v/SyZero.ApiGateway?style=flat-square)](https://www.nuget.org/packages/SyZero.ApiGateway) | API 网关支持 |
| **SyZero.Feign** | [![NuGet](https://img.shields.io/nuget/v/SyZero.Feign?style=flat-square)](https://www.nuget.org/packages/SyZero.Feign) | 声明式 HTTP 客户端 |

> 💡 **内置服务管理**：SyZero 核心模块还提供了 `LocalServiceManagement`（基于文件）、`DBServiceManagement`（基于数据库）和 `RedisServiceManagement`（基于 Redis）三种轻量级服务管理实现，适用于开发测试或简单部署场景。

### 工具与扩展

| 模块 | NuGet 包 | 说明 |
|------|----------|------|
| **SyZero.AutoMapper** | [![NuGet](https://img.shields.io/nuget/v/SyZero.AutoMapper?style=flat-square)](https://www.nuget.org/packages/SyZero.AutoMapper) | AutoMapper 对象映射 |
| **SyZero.Log4Net** | [![NuGet](https://img.shields.io/nuget/v/SyZero.Log4Net?style=flat-square)](https://www.nuget.org/packages/SyZero.Log4Net) | Log4Net 日志支持 |
| **SyZero.OpenTelemetry** | [![NuGet](https://img.shields.io/nuget/v/SyZero.OpenTelemetry?style=flat-square)](https://www.nuget.org/packages/SyZero.OpenTelemetry) | OpenTelemetry 分布式追踪 |

## 🛠️ 快速开始

### 安装

通过 NuGet 安装核心包：

```bash
dotnet add package SyZero
```

根据需要安装其他模块：

```bash
dotnet add package SyZero.AspNetCore
dotnet add package SyZero.DynamicWebApi
dotnet add package SyZero.SqlSugar
dotnet add package SyZero.Swagger
```

### 基础使用

#### 1. 创建最小化 Web API

```csharp
using SyZero;
using SyZero.DynamicWebApi;
using SyZero.Swagger;

var builder = WebApplication.CreateBuilder(args);

// 添加 SyZero 核心服务
builder.AddSyZero();

// 添加控制器和动态 WebApi
builder.Services.AddControllers();
builder.Services.AddDynamicWebApi(new DynamicWebApiOptions()
{
    DefaultApiPrefix = "/api",
    DefaultAreaName = "MyService"
});

// 添加 Swagger 文档
builder.Services.AddSwagger();

// 添加 SqlSugar ORM (可选)
builder.Services.AddSyZeroSqlSugar();

// 添加 AutoMapper (可选)
builder.Services.AddSyZeroAutoMapper();

var app = builder.Build();

app.UseSyZero();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
```

#### 2. 创建业务服务

```csharp
public interface IUserService : IApplicationService
{
    Task<UserDto> GetUserAsync(int id);
    Task<bool> CreateUserAsync(CreateUserDto input);
}

public class UserService : SyZeroServiceBase, IUserService, IScopedDependency
{
    private readonly IRepository<User> _userRepository;
    
    public UserService(IRepository<User> userRepository)
    {
        _userRepository = userRepository;
    }
    
    public async Task<UserDto> GetUserAsync(int id)
    {
        var user = await _userRepository.GetAsync(id);
        return ObjectMapper.Map<UserDto>(user);
    }
    
    public async Task<bool> CreateUserAsync(CreateUserDto input)
    {
        var user = ObjectMapper.Map<User>(input);
        await _userRepository.InsertAsync(user);
        return true;
    }
}
```

#### 3. 启用服务注册与发现

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddSyZero();

// 使用 Consul
builder.Services.AddSyZeroConsul();

// 或使用 Nacos
// builder.Services.AddSyZeroNacos();

// 或使用 Redis 服务管理
// builder.Services.AddRedisServiceManagement();

var app = builder.Build();
app.UseSyZero();
app.Run();
```

### 配置文件示例

#### appsettings.json

```json
{
  "SyZero": {
    "Name": "MyService",
    "Protocol": "http",
    "Port": 5000,
    "Ip": "",
    "WanIp": ""
  },
  "ConnectionString": {
    "DbType": "MySql",
    "ConnectionString": "Server=localhost;Database=mydb;User=root;Password=123456;"
  },
  "Redis": {
    "Configuration": "localhost:6379",
    "InstanceName": "MyService:"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  },
  "Consul": {
    "Address": "http://localhost:8500"
  },
  "Nacos": {
    "ServerAddresses": ["http://localhost:8848"],
    "Namespace": "public"
  }
}
```

### 依赖注入

SyZero 提供自动依赖注入，只需实现相应的接口：

```csharp
// Scoped 生命周期 - 每次请求创建一个实例
public class UserService : IUserService, IScopedDependency
{
    private readonly IRepository<User> _userRepository;
    
    public UserService(IRepository<User> userRepository)
    {
        _userRepository = userRepository;
    }
}

// Singleton 生命周期 - 全局单例
public class ConfigService : IConfigService, ISingletonDependency
{
    private readonly IConfiguration _configuration;
    
    public ConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
}

// Transient 生命周期 - 每次使用创建新实例
public class EmailService : IEmailService, ITransientDependency
{
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // 发送邮件逻辑
    }
}
```

**手动注入：**

```csharp
builder.Services.AddScoped<IMyService, MyService>();
builder.Services.AddSingleton<IMySingletonService, MySingletonService>();
builder.Services.AddTransient<IMyTransientService, MyTransientService>();
```

## 🏥 服务管理

SyZero 提供了统一的 `IServiceManagement` 接口，支持多种服务注册发现后端：

| 实现 | 适用场景 | 特点 |
|------|----------|------|
| **LocalServiceManagement** | 开发测试、单机部署 | 基于本地文件，无需外部依赖 |
| **DBServiceManagement** | 简单生产环境 | 基于数据库，支持多实例 |
| **RedisServiceManagement** | 分布式环境 | 基于 Redis，支持发布/订阅实时通知 |
| **ConsulServiceManagement** | 生产环境 | 基于 Consul，功能完整 |
| **NacosServiceManagement** | 生产环境 | 基于 Nacos，支持配置中心 |

### 核心功能

- **服务注册/注销** - 自动注册服务实例，应用关闭时自动注销
- **健康检查** - 支持 HTTP 健康端点检查和心跳检测
- **自动清理** - 自动清理过期未心跳的服务实例
- **负载均衡** - 支持加权随机负载均衡
- **Leader 选举** - 多实例部署时，仅 Leader 执行健康检查和清理

### 使用示例

```csharp
// 配置服务管理（使用本地文件）
builder.Services.AddSyZeroLocalServiceManagement(options =>
{
    options.EnableHealthCheck = true;
    options.HealthCheckIntervalSeconds = 10;
    options.AutoCleanExpiredServices = true;
    options.EnableLeaderElection = true;  // 启用 Leader 选举
});

// 或使用 Redis
builder.Services.AddRedisServiceManagement(options =>
{
    options.EnableHealthCheck = true;
    options.EnableLeaderElection = true;
    options.EnablePubSub = true;  // 启用发布/订阅实时通知
});

// 或使用 Consul
builder.Services.AddSyZeroConsul();

// 或使用 Nacos  
builder.Services.AddSyZeroNacos();
```

### Leader 选举配置

当多个服务实例同时运行时，启用 Leader 选举可避免并发写入冲突：

```csharp
options.EnableLeaderElection = true;       // 启用 Leader 选举
options.LeaderLockExpireSeconds = 30;      // Leader 锁过期时间
options.LeaderLockRenewIntervalSeconds = 10; // Leader 锁续期间隔
```

## 事件总线

SyZero 提供了统一的 `IEventBus` 接口，支持多种事件总线实现：

| 实现 | 适用场景 | 特点 |
|------|----------|------|
| **LocalEventBus** | 单体应用、进程内通信 | 基于内存，高性能，无需外部依赖 |
| **DBEventBus** | 单体应用、持久化需求 | 基于数据库，支持事件持久化和重试 |
| **RedisEventBus** | 简单分布式广播 | 基于 Redis Pub/Sub，适合轻量跨实例事件广播 |
| **RabbitMQEventBus** | 分布式系统、微服务 | 基于 RabbitMQ，支持跨服务通信和可靠投递 |

### 核心功能

- **事件发布/订阅** - 支持强类型和动态事件
- **批量发布** - 支持批量发布事件提高性能
- **事件持久化** - DBEventBus 支持事件持久化和重试
- **跨服务通信** - RabbitMQEventBus 支持分布式事件传递
- **解耦架构** - 发布者与订阅者完全解耦

### 使用示例

#### 1. 配置事件总线

```csharp
var builder = WebApplication.CreateBuilder(args);

// 使用本地内存事件总线（单体应用）
builder.Services.AddLocalEventBus();

// 或使用数据库事件总线（需要持久化）
// builder.Services.AddDBEventBus();

// 或使用 Redis 事件总线（轻量跨实例广播）
// builder.Services.AddSyZeroRedis();
// builder.Services.AddRedisEventBus();

// 或使用 RabbitMQ 事件总线（分布式系统）
// builder.Services.AddRabbitMQEventBus();

var app = builder.Build();
app.Run();
```

#### 2. 定义事件

```csharp
using SyZero.EventBus;

public class OrderCreatedEvent : EventBase
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; }
    public decimal Amount { get; set; }
}
```

#### 3. 定义事件处理器

```csharp
using SyZero.EventBus;

public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedEventHandler> _logger;
    
    public OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(OrderCreatedEvent @event)
    {
        _logger.LogInformation($"订单创建：{@event.OrderId}, 客户：{@event.CustomerName}");
        // 处理订单创建逻辑（如发送邮件、更新库存等）
        await Task.CompletedTask;
    }
}
```

#### 4. 发布和订阅事件

```csharp
public class OrderService : IScopedDependency
{
    private readonly IEventBus _eventBus;
    
    public OrderService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        
        // 订阅事件
        _eventBus.Subscribe<OrderCreatedEvent, OrderCreatedEventHandler>();
    }
    
    public async Task CreateOrderAsync(CreateOrderDto input)
    {
        // 创建订单业务逻辑
        var orderId = SaveOrder(input);
        
        // 发布事件
        await _eventBus.PublishAsync(new OrderCreatedEvent
        {
            OrderId = orderId,
            CustomerName = input.CustomerName,
            Amount = input.Amount
        });
    }
}
```

#### 5. 批量发布事件

```csharp
var events = new List<OrderCreatedEvent>
{
    new OrderCreatedEvent { OrderId = 1, CustomerName = "张三", Amount = 100 },
    new OrderCreatedEvent { OrderId = 2, CustomerName = "李四", Amount = 200 }
};

await _eventBus.PublishBatchAsync(events);
```

## 📁 项目结构

```
syzero-core/
├── src/
│   ├── SyZero.Core/                    # 核心模块
│   │   ├── SyZero/                     # 核心库
│   │   ├── SyZero.AspNetCore/          # ASP.NET Core 集成
│   │   ├── SyZero.AutoMapper/          # AutoMapper 支持
│   │   ├── SyZero.Consul/              # Consul 服务发现
│   │   ├── SyZero.DynamicGrpc/         # 动态 gRPC
│   │   ├── SyZero.DynamicWebApi/       # 动态 WebApi
│   │   ├── SyZero.EntityFrameworkCore/ # EF Core 支持
│   │   ├── SyZero.Feign/               # 声明式 HTTP 客户端
│   │   ├── SyZero.Log4Net/             # Log4Net 日志
│   │   ├── SyZero.MongoDB/             # MongoDB 支持
│   │   ├── SyZero.Nacos/               # Nacos 支持
│   │   ├── SyZero.OpenTelemetry/       # 链路追踪
│   │   ├── SyZero.RabbitMQ/            # RabbitMQ 消息队列
│   │   ├── SyZero.Redis/               # Redis 缓存
│   │   ├── SyZero.SqlSugar/            # SqlSugar ORM
│   │   ├── SyZero.Swagger/             # Swagger 文档
│   │   └── SyZero.Web.Common/          # Web 公共组件
│   ├── SyZero.Gateway/                 # API 网关示例
│   └── SyZero.Service/                 # 示例服务
│       ├── SyZero.Example1.Service/    # 示例服务 1
│       └── SyZero.Example2.Service/    # 示例服务 2
├── doc/                                # 文档
├── nuget/                              # NuGet 发布脚本
└── README.md
```

## 🔧 技术栈

- **.NET**: .NET 9.0+ / .NET Standard 2.1+
- **IDE**: Visual Studio 2022 / VS Code / Rider
- **数据库**: SQL Server / MySQL / MongoDB / PostgreSQL (可选)
- **缓存**: Redis / 内存缓存 (可选)
- **消息队列**: RabbitMQ (可选)
- **服务注册**: Consul / Nacos / Local / DB / Redis (可选)
- **链路追踪**: OpenTelemetry (可选)

## 📖 文档

访问 [syzero.com](https://docs.syzero.com) 获取完整文档。

## 📋 更新历史

查看完整的 [更新日志](ReleaseNotes.md)。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 提交 Pull Request

## 📄 许可证

本项目基于 [Apache License 2.0](LICENSE) 许可证开源。

## 👤 作者

**HaitaoJin**

- GitHub: [@HaitaoJin](https://github.com/HaitaoJin)

## ⭐ Star History

如果这个项目对你有帮助，请给一个 Star ⭐

---

<p align="center">Made with ❤️ by HaitaoJin</p>
