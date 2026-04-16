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

`launchSettings.json` 需要包含：

```json
{
  "environmentVariables": {
    "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES": "SyZero.AspNetCore.SpaProxy"
  }
}
```
