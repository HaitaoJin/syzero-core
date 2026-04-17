# SyZero.DynamicGrpc

SyZero.DynamicGrpc is a code-first gRPC module based on protobuf-net.Grpc.AspNetCore, designed for dynamic and fast gRPC service exposure.

## Install

```bash
dotnet add package SyZero.DynamicGrpc
```

## Key Features

- Code-first gRPC
- Dynamic service registration
- Message size limits for send/receive
- Detailed error support for debugging

## Quick Start

```csharp
builder.Services.AddDynamicGrpc();
app.MapDynamicGrpcServices();
```

## Configuration Example (appsettings.json)

```json
{
	"DynamicGrpc": {
		"MaxReceiveMessageSize": 4194304,
		"MaxSendMessageSize": 4194304,
		"EnableDetailedErrors": true
	}
}
```

## Common Configuration

- DynamicGrpc.MaxReceiveMessageSize
- DynamicGrpc.MaxSendMessageSize
- DynamicGrpc.EnableDetailedErrors

## References

- Chinese documentation: [/modules/SyZero.DynamicGrpc](/modules/SyZero.DynamicGrpc)


