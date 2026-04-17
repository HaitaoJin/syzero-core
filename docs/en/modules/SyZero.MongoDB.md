# SyZero.MongoDB

SyZero.MongoDB provides MongoDB integration with SyZero abstractions such as `IMongoContext` and `IRepository<TEntity>`.

## Install

```bash
dotnet add package SyZero.MongoDB
```

## Key Features

- Document-database access for SyZero apps
- Repository-style CRUD operations
- Query support with MongoDB driver
- Connection management support

## Quick Start

```csharp
builder.Services.AddSyZeroMongoDB();
```

## Configuration Example (appsettings.json)

```json
{
	"MongoDB": {
		"DataBase": "MyDb",
		"UserName": "",
		"Password": "",
		"Services": ["localhost:27017"]
	}
}
```

## Common Configuration

- MongoDB.DataBase
- MongoDB.UserName
- MongoDB.Password
- MongoDB.Services

## References

- Chinese documentation: [/modules/SyZero.MongoDB](/modules/SyZero.MongoDB)


