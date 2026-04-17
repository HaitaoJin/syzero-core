# SyZero.Consul

SyZero.Consul provides Consul-based service registration, service discovery, health checks, and optional configuration-center integration.

## Install

```bash
dotnet add package SyZero.Consul
```

## Key Features

- Service registration and discovery
- Health check integration
- Consul KV configuration loading
- Metadata and leader-election support

## Quick Start

```csharp
// Required: service registration and discovery.
builder.Services.AddConsul();

// Optional: load configuration from Consul KV.
builder.Configuration.AddConsulConfiguration();
```

## Configuration Example (appsettings.json)

```json
{
	"Consul": {
		"ConsulAddress": "http://localhost:8500",
		"Token": "",
		"ServiceName": "MyService",
		"ServiceAddress": "localhost",
		"ServicePort": 5000,
		"HealthCheckUrl": "/health"
	}
}
```

## Common Configuration

- Consul.ConsulAddress
- Consul.Token
- Consul.ServiceName
- Consul.ServiceAddress
- Consul.ServicePort
- Consul.HealthCheckUrl

## References

- Chinese documentation: [/modules/SyZero.Consul](/modules/SyZero.Consul)


