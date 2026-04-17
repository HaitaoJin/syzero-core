# SyZero.SqlSugar

SyZero.SqlSugar integrates SqlSugar ORM with SyZero repository and unit-of-work conventions.

## Install

```bash
dotnet add package SyZero.SqlSugar
```

## Key Features

- SqlSugar ORM abstraction
- Repository pattern support
- Unit-of-work support
- Query composition and async operations
- Multi-database architecture support

## Quick Start

```csharp
builder.Services.AddSyZeroSqlSugar();
// Optional typed registration:
// builder.Services.AddSyZeroSqlSugar<MyDbContext>();
// app.InitTables();
```

## Configuration Example (appsettings.json)

```json
{
	"ConnectionString": {
		"Type": "MySql",
		"Master": "Server=localhost;Database=mydb;User=root;Password=123456;",
		"Slave": []
	}
}
```

## Common Configuration

- ConnectionString.Type
- ConnectionString.Master
- ConnectionString.Slave

## References

- Chinese documentation: [/modules/SyZero.SqlSugar](/modules/SyZero.SqlSugar)


