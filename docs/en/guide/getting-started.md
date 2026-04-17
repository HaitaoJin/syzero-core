# Getting Started

This guide helps you bootstrap a minimal SyZero Web API in minutes.

## 1. Install packages

```bash
dotnet add package SyZero
dotnet add package SyZero.AspNetCore
dotnet add package SyZero.DynamicWebApi
dotnet add package SyZero.Swagger
```

## 2. Configure Program.cs

```csharp
using SyZero;
using SyZero.DynamicWebApi;
using SyZero.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.AddSyZero();
builder.Services.AddControllers();
builder.Services.AddDynamicWebApi(new DynamicWebApiOptions
{
    DefaultApiPrefix = "/api",
    DefaultAreaName = "MyService"
});
builder.Services.AddSwagger();

var app = builder.Build();

app.UseSyZero();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
```

## 3. Next

1. Read [Architecture](/en/guide/architecture)
2. Explore [Modules](/en/modules/index)