using Refit;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using SyZero.Application.Routing;
using SyZero.Extension;

namespace SyZero.Feign
{
    public class RequestFeignHandler : DelegatingHandler
    {
        private readonly string _serverName;

        public RequestFeignHandler(string serverName, HttpMessageHandler innerHandler = null) : base(innerHandler ?? new HttpClientHandler())
        {
            _serverName = serverName;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri == null)
            {
                throw new InvalidOperationException("请求地址不能为空。");
            }

            var builder = new UriBuilder(request.RequestUri);
            var originalPath = builder.Path ?? string.Empty;

            if (!HasApiPrefix(originalPath))
            {
                var controllerName = GetControllerName(request);
                var relativePath = TrimSlashes(RemoveApiPrefix(originalPath));
                var segments = new List<string> { "api", _serverName };

                if (!string.IsNullOrWhiteSpace(controllerName))
                {
                    segments.Add(controllerName);
                }

                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    segments.Add(relativePath);
                }

                builder.Path = "/" + string.Join("/", segments);
            }
            else
            {
                var relativePath = RemoveApiPrefix(originalPath);
                builder.Path = string.IsNullOrWhiteSpace(relativePath) ? "/" : EnsureLeadingSlash(relativePath);
            }

            request.RequestUri = builder.Uri;

            return await base.SendAsync(request, cancellationToken);
        }

        private static bool HasApiPrefix(string path)
        {
            return path.Equals(RoutingHelper.ApiUrlPre, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith($"{RoutingHelper.ApiUrlPre}/", StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveApiPrefix(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (path.Equals(RoutingHelper.ApiUrlPre, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (path.StartsWith($"{RoutingHelper.ApiUrlPre}/", StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(RoutingHelper.ApiUrlPre.Length);
            }

            return path;
        }

        private static string EnsureLeadingSlash(string path)
        {
            return path.StartsWith("/") ? path : "/" + path;
        }

        private static string TrimSlashes(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Trim('/');
        }

        private static string GetControllerName(HttpRequestMessage request)
        {
            if (!request.Options.TryGetValue(new HttpRequestOptionsKey<TypeInfo>(HttpRequestMessageOptions.InterfaceType), out var interfaceType))
            {
                return string.Empty;
            }

            var interfaceName = interfaceType.Name.StartsWith("I", StringComparison.Ordinal) && interfaceType.Name.Length > 1
                ? interfaceType.Name.Substring(1)
                : interfaceType.Name;

            var controllerName = RoutingHelper.GetControllerName(interfaceName);
            var customApiName = interfaceType.GetSingleAttributeOrDefaultByFullSearch<ApiAttribute>();
            return customApiName?.Name ?? controllerName;
        }
    }
}
