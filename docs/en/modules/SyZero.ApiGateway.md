# SyZero.ApiGateway

SyZero.ApiGateway provides API gateway capabilities on top of Ocelot, including dynamic routing, load balancing, and Swagger aggregation.

## Install

```bash
dotnet add package SyZero.ApiGateway
```

## Key Features

- Ocelot-based upstream/downstream routing
- Load balancing support
- Swagger aggregation for microservices
- Circuit breaking and retry support

## Quick Start

```csharp
builder.Services.AddSyZeroApiGateway();
await app.UseSyZeroApiGatewayAsync();
```

## Configuration Example (appsettings.json)

```json
{
	"Routes": [
		{
			"DownstreamPathTemplate": "/api/users/{everything}",
			"UpstreamPathTemplate": "/gateway/users/{everything}",
			"LoadBalancerOptions": {
				"Type": "RoundRobin"
			}
		}
	]
}
```

## Common Configuration

- Routes
- DownstreamPathTemplate
- UpstreamPathTemplate
- LoadBalancerOptions

## References

- Chinese documentation: [/modules/SyZero.ApiGateway](/modules/SyZero.ApiGateway)


