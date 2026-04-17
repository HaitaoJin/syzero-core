# SyZero.AspNetCore

SyZero.AspNetCore is the ASP.NET Core web-layer extension for unified web setup, MVC extensions, and middleware integration.

## Install

```bash
dotnet add package SyZero.AspNetCore
```

## Key Features

- Unified ASP.NET Core setup
- MVC options extension
- Built-in exception handling integration
- SyZero logging integration

## Quick Start

```csharp
builder.AddSyZero();
builder.Services.AddSyZeroAspNet();

var app = builder.Build();
app.UseSyZero();
```

## Configuration Example (appsettings.json)

```json
{
	"Server": {
		"Name": "MyWebApp",
		"Port": 5000
	}
}
```

## Common Configuration

- Server.Name
- Server.Port

## References

- Chinese documentation: [/modules/SyZero.AspNetCore](/modules/SyZero.AspNetCore)


