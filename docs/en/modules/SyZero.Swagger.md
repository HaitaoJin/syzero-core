# SyZero.Swagger

SyZero.Swagger provides API documentation integration with JWT support and XML comment support.

## Install

```bash
dotnet add package SyZero.Swagger
```

## Key Features

- Automatic OpenAPI/Swagger generation
- JWT authorization integration
- XML comment loading
- Multi-version documentation support

## Quick Start

```csharp
builder.Services.AddSwagger();

app.UseSwagger();
app.UseSwaggerUI();
```

## Configuration Example (appsettings.json)

```json
{
	"Swagger": {
		"Title": "SyZero API",
		"Version": "v1"
	}
}
```

## Common Configuration

- Swagger.Title
- Swagger.Version

## References

- Chinese documentation: [/modules/SyZero.Swagger](/modules/SyZero.Swagger)


