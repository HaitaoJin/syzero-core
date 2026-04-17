# SyZero.MongoDB

SyZero 框架的 MongoDB 集成模块，提供 `IMongoContext` 和 `IRepository<TEntity>` 的 MongoDB 实现。

## 安装

```bash
dotnet add package SyZero.MongoDB
```

## 配置

```json
{
  "MongoDB": {
    "DataBase": "syzero",
    "UserName": "",
    "Password": "",
    "Services": [
      {
        "Host": "localhost",
        "Port": 27017
      }
    ]
  }
}
```

- `UserName` 和 `Password` 可留空，留空时按无认证连接处理。
- `Services` 至少需要配置一个节点。

## 注册

```csharp
using SyZero;

var builder = WebApplication.CreateBuilder(args);

builder.AddSyZero();

builder.Services.AddSyZeroMongoDB();

// 或者从指定配置读取
builder.Services.AddSyZeroMongoDB(builder.Configuration, "MongoDB");

// 或者在默认配置基础上追加覆盖
builder.Services.AddSyZeroMongoDB(options =>
{
    options.DataBase = "syzero";
    options.Services = new List<MongoServers>
    {
        new() { Host = "localhost", Port = 27017 }
    };
});
```

## 使用示例

```csharp
using SyZero.Domain.Repository;

public class UserService
{
    private readonly IRepository<User> _userRepository;

    public UserService(IRepository<User> userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<User> CreateAsync(User user)
    {
        return _userRepository.AddAsync(user);
    }

    public Task<IQueryable<User>> GetActiveUsersAsync()
    {
        return _userRepository.GetListAsync(x => x.Enabled);
    }
}
```

## 当前支持能力

- `Add` / `AddAsync`
- `AddList` / `AddListAsync`
- `GetModel` / `GetModelAsync`
- `GetList` / `GetListAsync`
- `GetPaged` / `GetPagedAsync`
- `Update` / `UpdateAsync`
- `Delete` / `DeleteAsync`
- `Count` / `CountAsync`

当前模块公开接口仍以 `IRepository<TEntity>` 为准，不提供 `IRepository<TEntity, TKey>`、聚合管道封装或索引管理 API。

## 注意事项

1. 集合名默认使用实体类型名。
2. 实体需要实现 `IEntity`，主键类型为 `long`。
3. 注册成功不代表 MongoDB 连接立即建立，实际访问集合时才会触发网络连接。

## 许可证

MIT License - 详见 [LICENSE](../../LICENSE)

