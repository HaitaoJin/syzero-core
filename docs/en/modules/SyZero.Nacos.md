# SyZero.Nacos

SyZero.Nacos enables Nacos-based service registration, service discovery, and configuration-center usage for microservice environments.

## Install

```bash
dotnet add package SyZero.Nacos
```

## Key Features

- Service registration and discovery
- Config-center integration
- Health-check and metadata support
- Namespace and group management support

## Quick Start

```csharp
// Required: service registration and discovery.
builder.Services.AddNacos();

// Optional: load configuration from Nacos configuration center.
builder.Configuration.AddNacosConfiguration();
```

## Configuration Example (appsettings.json)

```json
{
	"Nacos": {
		"ServerAddresses": ["http://localhost:8848"],
		"Namespace": "public",
		"ServiceName": "MyService",
		"GroupName": "DEFAULT_GROUP",
		"ClusterName": "DEFAULT",
		"Ip": "",
		"Port": 5000
	}
}
```

## Common Configuration

- Nacos.ServerAddresses
- Nacos.Namespace
- Nacos.ServiceName
- Nacos.GroupName
- Nacos.ClusterName
- Nacos.Ip
- Nacos.Port

## References

- Chinese documentation: [/modules/SyZero.Nacos](/modules/SyZero.Nacos)


