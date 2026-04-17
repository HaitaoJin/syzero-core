# SyZero.AspNetCore.SpaProxy

SyZero.AspNetCore.SpaProxy enables SPA development proxying with automatic frontend dev-server launch and production build integration.

## Install

```bash
dotnet add package SyZero.AspNetCore.SpaProxy
```

## Key Features

- Local dev-server proxy for SPA apps
- Automatic launch command support
- Frontend build-on-build option
- Dev and production behavior separation

## Common Configuration

```json
{
	"SpaRoot": "ClientApp",
	"SpaProxyLaunchCommand": "npm run dev",
	"SpaProxyServerUrl": "http://localhost:5173",
	"BuildFrontendOnBuild": false
}
```

- SpaRoot
- SpaProxyLaunchCommand
- SpaProxyServerUrl
- BuildFrontendOnBuild

## References

- Chinese documentation: [/modules/SyZero.AspNetCore.SpaProxy](/modules/SyZero.AspNetCore.SpaProxy)


