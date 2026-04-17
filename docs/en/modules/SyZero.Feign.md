# SyZero.Feign

SyZero.Feign is a declarative HTTP client module based on Refit, with optional service discovery and load-balancing integration.

## Install

```bash
dotnet add package SyZero.Feign
```

## Key Features

- Declarative client interfaces
- Refit-based strongly typed HTTP calls
- Service discovery and load balancing support
- Interceptor and policy extension points

## Quick Start

```csharp
builder.Services.AddSyZeroFeign();

// Optional fluent extension APIs shown in the module README.
builder.Services
		.AddSyZeroFeign()
		.AddClient<IMyRemoteService>()
		.AddInterceptor<MyFeignInterceptor>();
```

## Configuration Example (appsettings.json)

```json
{
	"Feign": {
		"Timeout": 30,
		"RetryCount": 3,
		"BaseUrl": "http://localhost:5000"
	}
}
```

## Common Configuration

- Feign.Timeout
- Feign.RetryCount
- Feign.BaseUrl

## References

- Chinese documentation: [/modules/SyZero.Feign](/modules/SyZero.Feign)


