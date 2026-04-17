# SyZero.Web.Common

SyZero.Web.Common provides common web-layer components such as JWT authentication, unified responses, and shared exception handling patterns.

## Install

```bash
dotnet add package SyZero.Web.Common
```

## Key Features

- JWT authentication integration
- Unified API response model
- Request context helpers
- Common exception-handling support

## Quick Start

```csharp
builder.Services.AddSyZeroWebCommon();

// Required for JWT-based authorization.
app.UseAuthentication();
app.UseAuthorization();
```

## Configuration Example (appsettings.json)

```json
{
	"Jwt": {
		"SecretKey": "ReplaceWithStrongSecretKey",
		"Issuer": "SyZero",
		"Audience": "SyZeroClients",
		"ExpireMinutes": 60
	}
}
```

## Common Configuration

- Jwt.SecretKey
- Jwt.Issuer
- Jwt.Audience
- Jwt.ExpireMinutes

## References

- Chinese documentation: [/modules/SyZero.Web.Common](/modules/SyZero.Web.Common)


