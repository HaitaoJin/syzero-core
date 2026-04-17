# SyZero.AutoMapper

SyZero 框架的 AutoMapper 集成模块，提供对象映射自动配置。

## 📦 安装

```bash
dotnet add package SyZero.AutoMapper
```

## ✨ 特性

- 🚀 **自动扫描** - 自动扫描并注册所有 Profile
- 💾 **依赖注入** - 无缝集成 Microsoft DI
- 🔒 **类型安全** - 编译时类型检查
- 📦 **集合映射** - 内置 AutoMapper.Collection 支持
- 🎯 **多目标框架** - 支持 net8.0、net9.0

---

## 🚀 快速开始

### 注册服务

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
// 添加SyZero
builder.AddSyZero();

// 方式1 - 自动扫描所有程序集
builder.Services.AddSyZeroAutoMapper();

// 方式2 - 指定程序集
builder.Services.AddSyZeroAutoMapper(typeof(UserProfile).Assembly);

// 方式3 - 多个程序集
builder.Services.AddSyZeroAutoMapper(
    typeof(UserProfile).Assembly,
    typeof(OrderProfile).Assembly
);

// 方式4 - 自定义配置
builder.Services.AddSyZeroAutoMapper(cfg =>
{
    cfg.AllowNullCollections = true;
    cfg.AllowNullDestinationValues = false;
}, typeof(UserProfile).Assembly);

var app = builder.Build();
// 使用SyZero
app.UseSyZero();
app.Run();
```

### 使用示例

```csharp
public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>();
        CreateMap<CreateUserInput, User>();
        
        // 集合映射（由 AutoMapper.Collection 提供）
        CreateMap<User, UserDto>()
            .EqualityComparison((src, dest) => src.Id == dest.Id);
    }
}

public class UserService
{
    private readonly IObjectMapper _mapper;

    public UserService(IObjectMapper mapper)
    {
        _mapper = mapper;
    }

    public UserDto GetUser(User user)
    {
        return _mapper.Map<UserDto>(user);
    }
    
    public void UpdateUser(UserDto dto, User user)
    {
        _mapper.Map(dto, user);
    }
}
```

---

## 📖 API 说明

### IObjectMapper 接口（SyZero 抽象）

| 方法 | 说明 |
|------|------|
| `Map<TDestination>(source)` | 将源对象映射到目标类型 |
| `Map<TSource, TDestination>(source, dest)` | 映射到现有对象 |

### IMapper 接口（AutoMapper 原生）

| 方法 | 说明 |
|------|------|
| `Map<TDestination>(source)` | 将源对象映射到目标类型 |
| `Map<TSource, TDestination>(source, dest)` | 映射到现有对象 |
| `Map(source, sourceType, destType)` | 动态类型映射 |
| `ProjectTo<TDestination>(queryable)` | IQueryable 投影映射 |

> 所有映射操作都是线程安全的

---

## 🔧 高级用法

### 自定义值转换

```csharp
public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.FullName, 
                opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"));
    }
}
```

### 条件映射

```csharp
CreateMap<User, UserDto>()
    .ForMember(dest => dest.Email, 
        opt => opt.Condition(src => src.IsEmailVerified));
```

---

## ⚠️ 注意事项

1. **Profile 类** - 所有映射配置应在 Profile 类中定义
2. **循环引用** - 注意处理对象间的循环引用
3. **性能** - 避免在热路径中使用动态映射

---

## 📄 许可证

MIT License - 详见 [LICENSE](../../LICENSE)

