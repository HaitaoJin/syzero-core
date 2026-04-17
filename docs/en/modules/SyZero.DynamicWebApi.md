# SyZero.DynamicWebApi

SyZero.DynamicWebApi automatically generates RESTful APIs from application services and integrates cleanly with Swagger.

## Install

```bash
dotnet add package SyZero.DynamicWebApi
```

## Key Features

- Dynamic RESTful API generation
- Convention-based HTTP verb mapping
- Automatic controller exposure from service methods
- Swagger-friendly metadata output

## Quick Start

```csharp
builder.Services.AddDynamicWebApi();
app.MapControllers();
```

## Configuration Example (appsettings.json)

```json
{
	"DynamicWebApi": {
		"DefaultAreaName": "MyService",
		"DefaultHttpVerb": "POST",
		"RemoveActionPostfixes": ["Async"]
	}
}
```

## Common Configuration

- DynamicWebApi.DefaultAreaName
- DynamicWebApi.DefaultHttpVerb
- DynamicWebApi.RemoveActionPostfixes

## References

- Chinese documentation: [/modules/SyZero.DynamicWebApi](/modules/SyZero.DynamicWebApi)


