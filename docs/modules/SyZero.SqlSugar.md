# SyZero.SqlSugar

SyZero 框架的 SqlSugar ORM 集成模块。

## 安装

```bash
dotnet add package SyZero.SqlSugar
```

## 配置

在 `appsettings.json` 中配置数据库连接：

```json
{
  "ConnectionString": {
    "Type": "MySql",
    "Master": "Server=localhost;Database=MyDb;User=root;Password=123456;",
    "Slave": [
      {
        "ConnectionString": "Server=localhost;Database=MyDbRead;User=root;Password=123456;",
        "HitRate": 10
      }
    ]
  }
}
```

## 注册

### 使用默认 `SyZeroDbContext`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddSyZero();
builder.Services.AddSyZeroSqlSugar();

var app = builder.Build();
app.UseSyZero();
app.InitTables();
app.Run();
```

### 使用自定义 `DbContext`

```csharp
using Microsoft.Extensions.Logging;
using SqlSugar;
using SyZero.SqlSugar.DbContext;

public class MyDbContext : SyZeroDbContext
{
    public MyDbContext(ConnectionConfig config, ILoggerFactory loggerFactory)
        : base(config, loggerFactory)
    {
    }
}

builder.Services.AddSyZeroSqlSugar<MyDbContext>();
```

## 使用示例

```csharp
using SyZero.Domain.Repository;

public class UserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IRepository<User> userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<User> CreateUserAsync(User user)
    {
        return _userRepository.AddAsync(user);
    }

    public IQueryable<User> GetActiveUsers()
    {
        return _userRepository.GetList(x => x.IsActive);
    }

    public Task ExecuteInTransactionAsync(Func<Task> action)
    {
        return _unitOfWork.ExecuteInTransactionAsync(action);
    }
}
```

## 说明

- `ISyZeroDbContext`、`IRepository<>`、`IUnitOfWork` 默认按 `Scoped` 注册。
- 同一请求作用域内，仓储和工作单元会共享同一个 `DbContext` 实例。
- `GetList` / `GetPaged` 返回可继续组合的查询对象，不会先把数据全部加载到内存。
- SQL 日志默认通过 `ILogger` 输出原始 SQL 和参数。

