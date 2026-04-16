using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class SpaProxyLaunchManager
    {
        private static readonly HttpClient HttpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        })
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        private readonly Lazy<SpaProxyServerInfo?> _serverInfo;
        private readonly ILogger<SpaProxyLaunchManager> _logger;
        private readonly SemaphoreSlim _launchLock = new SemaphoreSlim(1, 1);

        public SpaProxyLaunchManager(IWebHostEnvironment environment, ILogger<SpaProxyLaunchManager> logger)
        {
            _logger = logger;
            _serverInfo = new Lazy<SpaProxyServerInfo?>(() => LoadServerInfo(environment));
        }

        public SpaProxyServerInfo? ServerInfo => _serverInfo.Value;

        public async Task<bool> IsServerRunningAsync(CancellationToken cancellationToken)
        {
            var serverInfo = ServerInfo;
            if (serverInfo == null || !serverInfo.IsConfigured)
            {
                return false;
            }

            return await CanReachServerAsync(serverInfo.ServerUrl, cancellationToken);
        }

        public async Task EnsureServerStartedAsync(CancellationToken cancellationToken)
        {
            var serverInfo = ServerInfo;
            if (serverInfo == null || !serverInfo.IsConfigured)
            {
                return;
            }

            if (await CanReachServerAsync(serverInfo.ServerUrl, cancellationToken))
            {
                return;
            }

            await _launchLock.WaitAsync(cancellationToken);
            try
            {
                if (await CanReachServerAsync(serverInfo.ServerUrl, cancellationToken))
                {
                    return;
                }

                LaunchSpaProcess(serverInfo);
            }
            finally
            {
                _launchLock.Release();
            }
        }

        private void LaunchSpaProcess(SpaProxyServerInfo serverInfo)
        {
            var startInfo = BuildProcessStartInfo(serverInfo);
            _logger.LogInformation("Starting SPA development server with command '{Command}' in '{WorkingDirectory}'.", serverInfo.LaunchCommand, serverInfo.WorkingDirectory);

            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();
        }

        private static ProcessStartInfo BuildProcessStartInfo(SpaProxyServerInfo serverInfo)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + serverInfo.LaunchCommand,
                    WorkingDirectory = serverInfo.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            return new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c \"" + serverInfo.LaunchCommand.Replace("\"", "\\\"") + "\"",
                WorkingDirectory = serverInfo.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        private static async Task<bool> CanReachServerAsync(string serverUrl, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var targetUri))
            {
                return false;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static SpaProxyServerInfo? LoadServerInfo(IWebHostEnvironment environment)
        {
            foreach (var candidatePath in new[]
            {
                Path.Combine(AppContext.BaseDirectory, "spa.proxy.json"),
                Path.Combine(environment.ContentRootPath, "spa.proxy.json")
            })
            {
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                using var stream = File.OpenRead(candidatePath);
                using var document = JsonDocument.Parse(stream);
                if (!document.RootElement.TryGetProperty("SpaProxyServer", out var serverElement))
                {
                    continue;
                }

                return ParseServerInfo(serverElement);
            }

            return null;
        }

        private static SpaProxyServerInfo ParseServerInfo(JsonElement serverElement)
        {
            var serverInfo = new SpaProxyServerInfo
            {
                ServerUrl = GetString(serverElement, nameof(SpaProxyServerInfo.ServerUrl)),
                RedirectUrl = GetString(serverElement, nameof(SpaProxyServerInfo.RedirectUrl)),
                LaunchCommand = GetString(serverElement, nameof(SpaProxyServerInfo.LaunchCommand)),
                WorkingDirectory = GetString(serverElement, nameof(SpaProxyServerInfo.WorkingDirectory)),
                MaxTimeoutInSeconds = GetInt32(serverElement, nameof(SpaProxyServerInfo.MaxTimeoutInSeconds), 120),
                KeepRunning = GetBoolean(serverElement, nameof(SpaProxyServerInfo.KeepRunning))
            };

            return serverInfo;
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            if (!TryGetProperty(element, propertyName, out var propertyValue))
            {
                return string.Empty;
            }

            return propertyValue.ValueKind == JsonValueKind.String
                ? propertyValue.GetString() ?? string.Empty
                : propertyValue.ToString();
        }

        private static int GetInt32(JsonElement element, string propertyName, int defaultValue)
        {
            if (!TryGetProperty(element, propertyName, out var propertyValue))
            {
                return defaultValue;
            }

            if (propertyValue.ValueKind == JsonValueKind.Number && propertyValue.TryGetInt32(out var numericValue))
            {
                return numericValue;
            }

            if (propertyValue.ValueKind == JsonValueKind.String &&
                int.TryParse(propertyValue.GetString(), out var stringValue))
            {
                return stringValue;
            }

            return defaultValue;
        }

        private static bool GetBoolean(JsonElement element, string propertyName)
        {
            if (!TryGetProperty(element, propertyName, out var propertyValue))
            {
                return false;
            }

            if (propertyValue.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (propertyValue.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (propertyValue.ValueKind == JsonValueKind.String &&
                bool.TryParse(propertyValue.GetString(), out var stringValue))
            {
                return stringValue;
            }

            return false;
        }

        private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
        {
            foreach (var jsonProperty in element.EnumerateObject())
            {
                if (string.Equals(jsonProperty.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = jsonProperty.Value;
                    return true;
                }
            }

            propertyValue = default;
            return false;
        }
    }
}
