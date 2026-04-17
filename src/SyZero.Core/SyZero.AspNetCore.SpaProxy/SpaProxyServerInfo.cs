using Microsoft.AspNetCore.Http;
using System;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class SpaProxyServerInfo
    {
        public string ServerUrl { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public string LaunchCommand { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public int MaxTimeoutInSeconds { get; set; } = 120;
        public bool KeepRunning { get; set; }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ServerUrl) &&
            !string.IsNullOrWhiteSpace(LaunchCommand) &&
            !string.IsNullOrWhiteSpace(WorkingDirectory);

        public string BuildRedirectUrl(PathString requestPath, QueryString queryString)
        {
            var targetUrl = string.IsNullOrWhiteSpace(RedirectUrl) ? ServerUrl : RedirectUrl;
            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri))
            {
                return targetUrl;
            }

            var builder = new UriBuilder(targetUri);
            builder.Path = CombinePath(targetUri.AbsolutePath, requestPath.HasValue ? requestPath.Value! : "/");
            builder.Query = queryString.HasValue ? queryString.Value!.TrimStart('?') : string.Empty;
            return builder.Uri.ToString();
        }

        private static string CombinePath(string basePath, string requestPath)
        {
            var normalizedBasePath = string.IsNullOrWhiteSpace(basePath) ? "/" : basePath.TrimEnd('/');
            var normalizedRequestPath = string.IsNullOrWhiteSpace(requestPath) ? "/" : requestPath;
            if (!normalizedRequestPath.StartsWith("/", StringComparison.Ordinal))
            {
                normalizedRequestPath = "/" + normalizedRequestPath;
            }

            if (normalizedBasePath == "/")
            {
                return normalizedRequestPath;
            }

            return normalizedBasePath + normalizedRequestPath;
        }
    }
}
