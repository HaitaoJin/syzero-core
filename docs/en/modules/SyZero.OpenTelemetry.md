# SyZero.OpenTelemetry

SyZero.OpenTelemetry adds tracing, metrics, and logging integration with OTLP export for observability in distributed systems.

## Install

```bash
dotnet add package SyZero.OpenTelemetry
```

## Key Features

- Distributed tracing
- Metrics collection
- Logging pipeline integration
- OTLP export support
- Path-based instrumentation filtering

## Quick Start

```csharp
builder.Services.AddSyZeroOpenTelemetry();
```

## Configuration Example (appsettings.json)

```json
{
	"OpenTelemetry": {
		"OtlpUrl": "http://localhost:4317",
		"EnableTracing": true,
		"EnableMetrics": true,
		"EnableLogging": true,
		"AspNetCorePathPrefixes": ["/api"],
		"HttpClientPathPrefixes": ["https://"],
		"ActivitySources": ["SyZero"],
		"Meters": ["SyZero"]
	}
}
```

## Common Configuration

- OpenTelemetry.OtlpUrl
- OpenTelemetry.EnableTracing
- OpenTelemetry.EnableMetrics
- OpenTelemetry.EnableLogging
- OpenTelemetry.AspNetCorePathPrefixes
- OpenTelemetry.HttpClientPathPrefixes
- OpenTelemetry.ActivitySources
- OpenTelemetry.Meters

## References

- Chinese documentation: [/modules/SyZero.OpenTelemetry](/modules/SyZero.OpenTelemetry)


