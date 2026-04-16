using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class SpaProxyMiddleware
    {
        private const string LaunchPageHtml = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta http-equiv="refresh" content="2" />
  <title>Starting SPA dev server</title>
  <style>
    body { font-family: sans-serif; margin: 3rem; line-height: 1.5; }
    code { background: #f4f4f4; padding: 0.2rem 0.4rem; }
  </style>
</head>
<body>
  <h1>Starting SPA development server...</h1>
  <p>Launch command: <code>__COMMAND__</code></p>
  <p>Target URL: <code>__TARGET__</code></p>
  <p>This page refreshes automatically.</p>
</body>
</html>
""";

        private readonly RequestDelegate _next;

        public SpaProxyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IHostEnvironment environment, SpaProxyLaunchManager launchManager, ILogger<SpaProxyMiddleware> logger)
        {
            var serverInfo = launchManager.ServerInfo;
            if (!environment.IsDevelopment() || serverInfo == null || !serverInfo.IsConfigured || !ShouldHandleRequest(context))
            {
                await _next(context);
                return;
            }

            if (await launchManager.IsServerRunningAsync(context.RequestAborted))
            {
                context.Response.Redirect(serverInfo.BuildRedirectUrl(context.Request.Path, context.Request.QueryString));
                return;
            }

            await launchManager.EnsureServerStartedAsync(context.RequestAborted);
            if (await launchManager.IsServerRunningAsync(context.RequestAborted))
            {
                context.Response.Redirect(serverInfo.BuildRedirectUrl(context.Request.Path, context.Request.QueryString));
                return;
            }

            logger.LogInformation("SPA development server is starting for request '{Path}'.", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = MediaTypeNames.Text.Html;
            await context.Response.WriteAsync(LaunchPageHtml
                .Replace("__COMMAND__", serverInfo.LaunchCommand, StringComparison.Ordinal)
                .Replace("__TARGET__", serverInfo.ServerUrl, StringComparison.Ordinal));
        }

        private static bool ShouldHandleRequest(HttpContext context)
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                return false;
            }

            if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var path = context.Request.Path.Value ?? "/";
            if (!string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase) &&
                Path.HasExtension(path))
            {
                return false;
            }

            var acceptHeader = context.Request.Headers.Accept.ToString();
            return string.IsNullOrWhiteSpace(acceptHeader) ||
                   acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
                   acceptHeader.Contains("*/*", StringComparison.OrdinalIgnoreCase);
        }
    }
}
