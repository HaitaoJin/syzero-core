# SyZero.OpenTelemetry

SyZero 的 OpenTelemetry 扩展，负责注册 ASP.NET Core / HttpClient 的 tracing、metrics 和 logging，并按配置挂接 OTLP exporter。

## 安装

```bash
dotnet add package SyZero.OpenTelemetry
```

## 快速开始

```json
{
  "SyZero": {
    "Name": "order-service"
  },
  "OpenTelemetry": {
    "OtlpUrl": "http://localhost:4317",
    "EnableTracing": true,
    "EnableMetrics": true,
    "EnableLogging": true,
    "AspNetCorePathPrefixes": [ "/api" ],
    "HttpClientPathPrefixes": [ "/api" ],
    "ActivitySources": [ "OrderService" ],
    "Meters": [ "OrderService" ]
  }
}
```

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddSyZero();
builder.Services.AddSyZeroOpenTelemetry();
```

也可以通过代码覆盖配置：

```csharp
builder.Services.AddSyZeroOpenTelemetry(options =>
{
    options.ServiceName = "order-service";
    options.OtlpUrl = "http://localhost:4318";
    options.OtlpProtocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
    options.HttpClientPathPrefixes = Array.Empty<string>(); // 采集所有出站 HTTP 请求
});
```

## 可用重载

```csharp
services.AddSyZeroOpenTelemetry();
services.AddSyZeroOpenTelemetry(configuration);
services.AddSyZeroOpenTelemetry(options => { });
services.AddSyZeroOpenTelemetry(options => { }, configuration);
```

## 配置说明

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ServiceName` | `string` | `SyZero:Name` 或入口程序集名 | 资源中的服务名 |
| `ServiceVersion` | `string` | 入口程序集版本 | 资源中的服务版本 |
| `OtlpUrl` | `string` | `null` | OTLP 导出地址；未配置时不会注册 OTLP exporter |
| `OtlpProtocol` | `OtlpExportProtocol` | `Grpc` | OTLP 协议 |
| `EnableTracing` | `bool` | `true` | 是否启用 tracing |
| `EnableMetrics` | `bool` | `true` | 是否启用 metrics |
| `EnableLogging` | `bool` | `true` | 是否启用 logging |
| `ActivitySources` | `string[]` | 空数组 | 需要额外订阅的 `ActivitySource` 名称 |
| `Meters` | `string[]` | 空数组 | 需要额外订阅的 `Meter` 名称；内置会额外监听 `Microsoft.AspNetCore.Hosting`、`Microsoft.AspNetCore.Server.Kestrel`、`Microsoft.AspNetCore.Routing`、`System.Net.Http` |
| `AspNetCorePathPrefixes` | `string[]` | `["/api"]` | 需要采集的入站请求路径前缀 |
| `HttpClientPathPrefixes` | `string[]` | 空数组 | 需要采集的出站请求路径前缀；为空表示不过滤 |

## 说明

- `AddSource("*")` / `AddMeter("*")` 不会自动订阅所有自定义 source/meter，因此这里改成显式配置名称。
- metrics 默认通过监听 .NET 8+ 的内置 meter 名称来采集 HTTP / Kestrel / Routing 指标，而不是依赖当前目标框架下不可用的 metrics instrumentation 扩展。
- 如果没有配置 `OtlpUrl`，模块仍然会完成 OpenTelemetry 注册，但不会主动创建 OTLP 导出连接。
- `AspNetCorePathPrefixes` 会同时支持 `/api` 和 `/api/...` 这种匹配，避免只识别带尾斜杠的路径。

## 许可证

MIT License - 详见 [LICENSE](../../../LICENSE)
