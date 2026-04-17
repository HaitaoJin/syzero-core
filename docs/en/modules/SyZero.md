# SyZero

SyZero is the lightweight core package of the framework. It provides dependency injection conventions, application service infrastructure, repository and unit-of-work abstractions, lightweight service management, and lightweight event bus implementations.

## Install

```bash
dotnet add package SyZero
```

## Key Features

- Convention-based dependency injection
- ApplicationService and SyZeroServiceBase infrastructure
- Repository and unit-of-work abstractions
- Local and DB-based service management
- Local and DB-based event bus

## Quick Start

```csharp
using SyZero;

var builder = WebApplication.CreateBuilder(args);
builder.AddSyZero();

var app = builder.Build();
app.UseSyZero();
app.Run();
```

## Notes

- Use this package as the base of all SyZero applications.
- Add additional SyZero modules only when you need their capabilities.

## References

- Chinese documentation: [/modules/SyZero](/modules/SyZero)


