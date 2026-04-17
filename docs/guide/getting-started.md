# 快速开始

本页会在 10 分钟内带你启动一个最小 SyZero Web API。

## 1. 安装 NuGet 包

```bash
dotnet add package SyZero
dotnet add package SyZero.AspNetCore
dotnet add package SyZero.DynamicWebApi
dotnet add package SyZero.Swagger
```

## 2. 配置 Program.cs

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

## 3. 添加基础配置

```json
{
  "SyZero": {
    "Name": "MyService",
    "Protocol": "http",
    "Port": 5000
  }
}
```

## 4. 运行与验证

```bash
dotnet run
```

访问 `http://localhost:5000/swagger` 检查接口文档是否正常加载。

## 延伸阅读

1. [架构与约定](/guide/architecture)
2. [模块总览](/modules/index)