# {项目名称}

{一句话简介描述}

## 📦 安装

```bash
dotnet add package {包名}
```

## ✨ 特性

- 🚀 **特性一** - 简要描述
- 💾 **特性二** - 简要描述
- 🔒 **特性三** - 简要描述

---

## 🚀 快速开始

### 1. 配置 appsettings.json

```json
{
  "{配置节点}": {
    "Option1": "value1",
    "Option2": "value2"
  }
}
```

### 2. 注册服务

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
// 添加SyZero
builder.AddSyZero();

// 注册服务方式1
builder.Services.Add{服务名}();

// 注册服务方式2
builder.Services.Add{服务名}(options =>
{
    options.Option1 = "value1";
    options.Option2 = "value2";
});

// 注册服务方式3
builder.Services.Add{服务名}(options =>
{
    options.Option1 = "value1";
    options.Option2 = "value2";
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
    private readonly I{接口名} _{字段名};

    public MyService(I{接口名} {参数名})
    {
        _{字段名} = {参数名};
    }

    public async Task DoSomethingAsync()
    {
        // 使用示例代码
    }
}
```

---

## 📖 配置选项

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Option1` | `string` | `""` | 配置项说明 |
| `Option2` | `int` | `0` | 配置项说明 |

---

## 📖 API 说明

### I{接口名} 接口

| 方法 | 说明 |
|------|------|
| `Method1()` | 方法说明 |
| `Method2(param)` | 方法说明 |

> 所有方法都有对应的异步版本（带 `Async` 后缀）

---

## 🔧 高级用法

### 场景一

```csharp
// 高级用法示例代码
```

### 场景二

```csharp
// 高级用法示例代码
```

---

## ⚠️ 注意事项

1. **注意事项一** - 详细说明
2. **注意事项二** - 详细说明
3. **注意事项三** - 详细说明

---

## 📄 许可证

MIT License - 详见 [LICENSE](../../../LICENSE)
