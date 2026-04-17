# SyZero.Log4Net

SyZero 框架的 Log4Net 日志集成模块。

## 📦 安装

```bash
dotnet add package SyZero.Log4Net
```

## ✨ 特性

- 🚀 **日志集成** - 集成 Log4Net 到 Microsoft.Extensions.Logging
- 💾 **多输出** - 支持文件、控制台等多种输出
- 🔒 **配置灵活** - 支持 XML 配置

---

## 🚀 快速开始

### 1. 配置 log4net.config

```xml
<?xml version="1.0" encoding="utf-8" ?>
<log4net>
  <appender name="RollingFile" type="log4net.Appender.RollingFileAppender">
    <file value="logs/app.log" />
    <appendToFile value="true" />
    <rollingStyle value="Date" />
    <datePattern value="yyyyMMdd" />
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%date [%thread] %-5level %logger - %message%newline" />
    </layout>
  </appender>
  <root>
    <level value="INFO" />
    <appender-ref ref="RollingFile" />
  </root>
</log4net>
```

### 2. 注册服务

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
// 添加SyZero
builder.AddSyZero();

// 注册服务方式1 - 使用默认配置文件
builder.Logging.AddSyZeroLog4Net();

// 注册服务方式2 - 指定配置文件
builder.Logging.AddSyZeroLog4Net(options =>
{
    options.ConfigFile = "log4net.config";
});

// 注册服务方式3 - 使用委托配置
builder.Logging.AddSyZeroLog4Net(options =>
{
    options.ConfigFile = "Configs/log4net.config";
    options.Watch = true;
    options.RepositoryName = "MyServiceRepository";
});

var app = builder.Build();
// 使用SyZero
app.UseSyZero();
app.Run();
```

### 3. 使用示例

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public void DoSomething()
    {
        _logger.LogInformation("操作执行成功");
        _logger.LogWarning("警告信息");
        _logger.LogError("错误信息");
    }
}
```

---

## 📖 配置选项

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ConfigFile` | `string` | `"log4net.config"` | 配置文件路径 |
| `Watch` | `bool` | `false` | 是否监听配置文件变化 |
| `RepositoryName` | `string` | `null` | 指定 log4net repository 名称，适合隔离多实例或测试场景 |

---

## 📖 API 说明

### ILogger 接口

| 方法 | 说明 |
|------|------|
| `LogTrace(message)` | 跟踪级别日志 |
| `LogDebug(message)` | 调试级别日志 |
| `LogInformation(message)` | 信息级别日志 |
| `LogWarning(message)` | 警告级别日志 |
| `LogError(message)` | 错误级别日志 |

> 使用标准的 ILogger 接口，无需直接依赖 Log4Net

---

## 🔧 高级用法

### 按类别输出

```xml
<logger name="MyApp.Services">
  <level value="DEBUG" />
  <appender-ref ref="ServiceLog" />
</logger>
```

### 异步写入

```xml
<appender name="AsyncFile" type="log4net.Appender.AsyncAppender">
  <appender-ref ref="RollingFile" />
</appender>
```

---

## ⚠️ 注意事项

1. **配置文件** - 确保 log4net.config 文件存在且可访问
2. **日志目录** - 确保应用有权限写入日志目录
3. **性能** - 生产环境建议使用异步写入

---

## 📄 许可证

MIT License - 详见 [LICENSE](../../../LICENSE)
