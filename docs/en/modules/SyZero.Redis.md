# SyZero.Redis

SyZero.Redis provides Redis integration for distributed caching, distributed locks, Redis-based service management, and Redis event bus.

## Install

```bash
dotnet add package SyZero.Redis
```

## Key Features

- Distributed cache implementation
- Distributed lock utility
- Redis-based service management
- Redis pub/sub event bus
- Support for multiple Redis topologies

## Quick Start

```csharp
// Required for Redis cache and lock features.
builder.Services.AddSyZeroRedis();

// Optional: Redis-based service management.
builder.Services.AddRedisServiceManagement();

// Optional: Redis pub/sub event bus.
builder.Services.AddRedisEventBus();
```

## Configuration Example (appsettings.json)

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

## Common Configuration

- Redis (Type, Master, Slave)
- RedisServiceManagement
- RedisEventBus

## References

- Chinese documentation: [/modules/SyZero.Redis](/modules/SyZero.Redis)


