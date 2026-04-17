# SyZero.AspNetCore.SpaProxy

SyZero 的 SPA 开发代理和前端构建扩展。

## 功能

- 生成 `spa.proxy.json`
- 提供 `HostingStartup` 开发代理
- 构建时自动执行前端工程
- 发布时自动复制前端 `dist` 到 `wwwroot`

## 安装

```bash
dotnet add package SyZero.AspNetCore.SpaProxy
```

## 常用配置

```xml
<PropertyGroup>
  <SpaRoot>..\Foo.Frontend\</SpaRoot>
  <SpaProxyLaunchCommand>npm run dev</SpaProxyLaunchCommand>
  <SpaProxyServerUrl>http://localhost:5173</SpaProxyServerUrl>
  <BuildFrontendOnBuild>true</BuildFrontendOnBuild>
  <FrontendProjectPath>..\Foo.Frontend\Foo.Frontend.esproj</FrontendProjectPath>
  <FrontendDistDir>..\Foo.Frontend\dist</FrontendDistDir>
</PropertyGroup>
```

## 开发期启动

项目启动后，`SyZero.AspNetCore.SpaProxy` 会：

- 读取输出目录中的 `spa.proxy.json`
- 检查 `SpaProxyServerUrl` 是否已可访问
- 如果前端 dev server 未启动，则执行 `SpaProxyLaunchCommand`
- 当浏览器访问 HTML 路由时，重定向到前端 dev server

### `dotnet run`

如果走 `launchSettings.json` profile，仍建议保留：

```json
{
  "environmentVariables": {
    "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES": "SyZero.AspNetCore.SpaProxy"
  }
}
```

### `dotnet watch run`

从 `SyZero.AspNetCore.SpaProxy 1.1.8` 开始，包会在 `ComputeRunArguments` 阶段自动给 `dotnet run` / `dotnet watch run` 注入：

```text
ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=SyZero.AspNetCore.SpaProxy
```

因此下面的命令也能自动拉起前端，不依赖 `launchSettings.json`：

```bash
dotnet watch run --no-launch-profile
```

## 注意事项

- `SpaProxyLaunchCommand` 只有在 `SpaProxyServerUrl` 当前不可访问时才会执行；如果本机对应端口已经有前端进程在监听，代理不会重复启动。
- 默认 `SpaProxyKeepRunning=false`。如果前端是由 `SyZero.AspNetCore.SpaProxy` 拉起的，那么后端项目停止时会一并关闭前端进程；如果希望前端常驻，可显式设置 `SpaProxyKeepRunning=true`。
- `dotnet build` 会执行前端生产构建；`dotnet run` / `dotnet watch run` 使用的是开发期代理逻辑，两者是不同链路。
- 若更新了本地 NuGet 包版本，记得重新 `restore`，避免继续命中旧缓存。

