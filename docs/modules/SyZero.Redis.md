# SyZero.Redis

SyZero.Redis 提供 Redis 缓存、分布式锁、Redis 服务管理和 Redis 事件总线能力。

## 📦 安装

```bash
dotnet add package SyZero.Redis
```

## ✨ 特性

- 🚀 **分布式缓存** - `ICache` 基于 Redis 实现
- 🔒 **分布式锁** - `ILockUtil` 基于 Redis 实现
- 🔍 **服务发现** - `RedisServiceManagement` 支持注册、发现、健康检查、Pub/Sub 通知
- 📣 **事件总线** - `RedisEventBus` 基于 Redis Pub/Sub 实现跨实例广播
- 💾 **多模式连接** - 支持主从、哨兵、集群模式

---

## 🚀 快速开始

### 1. 配置 appsettings.json

```json
{
  "Redis": {
    "Type": "MasterSlave",
    "Master": "localhost:6379,password=123456,defaultDatabase=0",
    "Slave": []
  },
  "RedisServiceManagement": {
    "EnableHealthCheck": true,
    "EnableLeaderElection": true,
    "EnablePubSub": true
  },
  "RedisEventBus": {
    "ChannelPrefix": "SyZero:EventBus:"
  }
}
```

### 2. 注册服务

```csharp
using SyZero;

var builder = WebApplication.CreateBuilder(args);

builder.AddSyZero();
builder.Services.AddSyZeroRedis();

// 可选：Redis 服务管理
builder.Services.AddRedisServiceManagement();

// 可选：Redis 事件总线
builder.Services.AddRedisEventBus();

var app = builder.Build();
app.UseSyZero();
app.Run();
```

---

## 📖 缓存与锁

```csharp
public class UserService
{
    private readonly ICache _cache;
    private readonly ILockUtil _lockUtil;

    public UserService(ICache cache, ILockUtil lockUtil)
    {
        _cache = cache;
        _lockUtil = lockUtil;
    }

    public async Task<User> GetUserAsync(long id)
    {
        var cacheKey = $"user:{id}";
        var user = await _cache.GetAsync<User>(cacheKey);

        if (user == null)
        {
            user = await LoadFromDbAsync(id);
            await _cache.SetAsync(cacheKey, user, 1800);
        }

        return user;
    }

    public async Task CreateOrderAsync(string orderNo)
    {
        using (await _lockUtil.LockAsync($"order:{orderNo}", TimeSpan.FromSeconds(30)))
        {
            // 在锁内执行业务逻辑
        }
    }
}
```

---

## 📖 Redis 服务管理

`RedisServiceManagement` 适合简单分布式部署，支持：

- 服务注册 / 注销
- 心跳与健康检查
- Leader 选举
- Redis Pub/Sub 实时通知

```csharp
builder.Services.AddSyZeroRedis();
builder.Services.AddRedisServiceManagement(options =>
{
    options.EnableHealthCheck = true;
    options.EnableLeaderElection = true;
    options.EnablePubSub = true;
});
```

---

## 📖 Redis 事件总线

`RedisEventBus` 基于 Redis Pub/Sub，适合需要跨实例广播、但不要求持久化和可靠重试的场景。

如果你需要持久化、重试或死信队列，请改用 `DBEventBus` 或 `RabbitMQEventBus`。

### 注册

```csharp
builder.Services.AddSyZeroRedis();
builder.Services.AddRedisEventBus(options =>
{
    options.ChannelPrefix = "SyZero:EventBus:";
});
```

### 定义事件与处理器

```csharp
using SyZero.EventBus;

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

### 订阅与发布

```csharp
public class OrderPublisher
{
    private readonly IEventBus _eventBus;

    public OrderPublisher(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<OrderCreatedEvent, OrderCreatedHandler>(() => new OrderCreatedHandler());
    }

    public Task PublishAsync(long orderId)
    {
        return _eventBus.PublishAsync(new OrderCreatedEvent
        {
            OrderId = orderId
        });
    }
}
```

---

## 📖 配置项

### Redis

| 属性 | 类型 | 说明 |
|------|------|------|
| `Type` | `MasterSlave / Sentinel / Cluster` | Redis 模式 |
| `Master` | `string` | 主节点连接串或主服务名 |
| `Slave` | `string[]` | 从节点列表 |
| `Sentinel` | `string[]` | 哨兵节点列表 |

### RedisServiceManagement

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `KeyPrefix` | `syzero:services:` | 服务注册 Key 前缀 |
| `LeaderKeyPrefix` | `syzero:leader:` | Leader 锁前缀 |
| `ServiceNamesKey` | `syzero:service:names` | 服务名集合 Key |
| `EnableHealthCheck` | `true` | 是否启用健康检查 |
| `ServiceExpireSeconds` | `30` | 多久未心跳标记不健康 |
| `AutoCleanExpiredServices` | `true` | 是否自动清理过期实例 |
| `EnableLeaderElection` | `true` | 是否启用 Leader 选举 |
| `EnablePubSub` | `true` | 是否启用服务变更通知 |

### RedisEventBus

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `ChannelPrefix` | `SyZero:EventBus:` | 事件总线频道前缀 |

---

## ⚠️ 注意事项

1. `RedisEventBus` 使用 Pub/Sub，不提供事件持久化、重试和死信队列。
2. 订阅关系保存在当前进程内，应用重启后需要重新执行订阅。
3. 建议将订阅逻辑放在应用启动阶段或长期存活服务中，而不是高频短生命周期对象里重复订阅。

---

## 📄 许可证

MIT License - 详见 [LICENSE](../../LICENSE)

