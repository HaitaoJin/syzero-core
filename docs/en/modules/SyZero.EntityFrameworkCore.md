# SyZero.EntityFrameworkCore

SyZero.EntityFrameworkCore integrates EF Core with repository pattern and unit-of-work conventions used by SyZero applications.

## Install

```bash
dotnet add package SyZero.EntityFrameworkCore
```

## Key Features

- Repository pattern implementation
- Unit-of-work support
- Transaction and async operation support
- Multi-database friendly architecture

## Quick Start

```csharp
builder.Services
	.AddSyZeroEntityFramework<MyDbContext>()
	.AddRepositories();
```

`AddRepositories()` enables repository registration for your entities, so your application services can inject `IRepository<TEntity>` directly.

## Configuration Example (appsettings.json)

```json
{
	"ConnectionStrings": {
		"Default": "Server=localhost;Database=mydb;User=root;Password=123456;"
	}
}
```

## Common Configuration

- ConnectionStrings.Default

## References

- Chinese documentation: [/modules/SyZero.EntityFrameworkCore](/modules/SyZero.EntityFrameworkCore)


