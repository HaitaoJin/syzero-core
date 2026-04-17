using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class SpaProxyLaunchManager
    {
        private static readonly TimeSpan LaunchPollInterval = TimeSpan.FromMilliseconds(200);

        private readonly Lazy<SpaProxyServerInfo?> _serverInfo;
        private readonly ILogger<SpaProxyLaunchManager> _logger;
        private readonly ISpaProxyServerProbe _serverProbe;
        private readonly ISpaProxyProcessFactory _processFactory;
        private readonly SemaphoreSlim _launchLock = new SemaphoreSlim(1, 1);
        private readonly object _processLock = new object();
        private ISpaProxyProcess? _launchedProcess;

        public SpaProxyLaunchManager(
            IWebHostEnvironment environment,
            ILogger<SpaProxyLaunchManager> logger,
            ISpaProxyServerProbe serverProbe,
            ISpaProxyProcessFactory processFactory)
        {
            _logger = logger;
            _serverProbe = serverProbe;
            _processFactory = processFactory;
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

            return await _serverProbe.CanReachServerAsync(serverInfo.ServerUrl, cancellationToken);
        }

        public async Task EnsureServerStartedAsync(CancellationToken cancellationToken)
        {
            var serverInfo = ServerInfo;
            if (serverInfo == null || !serverInfo.IsConfigured)
            {
                return;
            }

            if (await _serverProbe.CanReachServerAsync(serverInfo.ServerUrl, cancellationToken))
            {
                return;
            }

            await _launchLock.WaitAsync(cancellationToken);
            try
            {
                if (await _serverProbe.CanReachServerAsync(serverInfo.ServerUrl, cancellationToken))
                {
                    return;
                }

                LaunchSpaProcess(serverInfo);
                if (await WaitForServerToAcceptConnectionsAsync(serverInfo, cancellationToken))
                {
                    return;
                }

                _logger.LogWarning("SPA development server at '{ServerUrl}' did not become reachable within {TimeoutSeconds} seconds.", serverInfo.ServerUrl, serverInfo.MaxTimeoutInSeconds);
            }
            finally
            {
                _launchLock.Release();
            }
        }

        public async Task StopServerAsync(CancellationToken cancellationToken)
        {
            var serverInfo = ServerInfo;
            if (serverInfo == null || serverInfo.KeepRunning)
            {
                return;
            }

            ISpaProxyProcess? processToStop = null;
            lock (_processLock)
            {
                if (_launchedProcess != null)
                {
                    processToStop = _launchedProcess;
                    _launchedProcess = null;
                }
            }

            if (processToStop == null)
            {
                return;
            }

            try
            {
                if (!processToStop.HasExited)
                {
                    _logger.LogInformation("Stopping SPA development server process {ProcessId}.", processToStop.Id);
                    processToStop.Kill(entireProcessTree: true);
                    await processToStop.WaitForExitAsync(cancellationToken);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop SPA development server process.");
            }
            finally
            {
                processToStop.Dispose();
            }
        }

        private void LaunchSpaProcess(SpaProxyServerInfo serverInfo)
        {
            lock (_processLock)
            {
                if (_launchedProcess != null && !_launchedProcess.HasExited)
                {
                    return;
                }
            }

            _logger.LogInformation("Starting SPA development server with command '{Command}' in '{WorkingDirectory}'.", serverInfo.LaunchCommand, serverInfo.WorkingDirectory);

            var process = _processFactory.Create(serverInfo);
            process.Exited += HandleLaunchedProcessExited;

            try
            {
                process.Start();
            }
            catch
            {
                process.Exited -= HandleLaunchedProcessExited;
                process.Dispose();
                throw;
            }

            lock (_processLock)
            {
                _launchedProcess = process;
            }

            _logger.LogInformation("Started SPA development server process {ProcessId}.", process.Id);
        }

        private async Task<bool> WaitForServerToAcceptConnectionsAsync(SpaProxyServerInfo serverInfo, CancellationToken cancellationToken)
        {
            var timeout = serverInfo.MaxTimeoutInSeconds <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(serverInfo.MaxTimeoutInSeconds);
            var deadline = DateTimeOffset.UtcNow.Add(timeout);

            while (true)
            {
                if (await _serverProbe.CanReachServerAsync(serverInfo.ServerUrl, cancellationToken))
                {
                    return true;
                }

                if (!IsLaunchedProcessAlive())
                {
                    return false;
                }

                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    return await _serverProbe.CanReachServerAsync(serverInfo.ServerUrl, cancellationToken);
                }

                await Task.Delay(remaining < LaunchPollInterval ? remaining : LaunchPollInterval, cancellationToken);
            }
        }

        private bool IsLaunchedProcessAlive()
        {
            lock (_processLock)
            {
                return _launchedProcess != null && !_launchedProcess.HasExited;
            }
        }

        private SpaProxyServerInfo? LoadServerInfo(IWebHostEnvironment environment)
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

                try
                {
                    using var stream = File.OpenRead(candidatePath);
                    using var document = JsonDocument.Parse(stream);
                    if (!document.RootElement.TryGetProperty("SpaProxyServer", out var serverElement))
                    {
                        continue;
                    }

                    return ParseServerInfo(serverElement);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load SPA proxy configuration from '{ConfigPath}'.", candidatePath);
                }
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

        private void HandleLaunchedProcessExited(object? sender, EventArgs args)
        {
            if (sender is not ISpaProxyProcess process)
            {
                return;
            }

            var exitCode = 0;
            try
            {
                if (process.HasExited)
                {
                    exitCode = process.ExitCode;
                }
            }
            catch (InvalidOperationException)
            {
            }

            var shouldDispose = false;
            lock (_processLock)
            {
                if (ReferenceEquals(_launchedProcess, process))
                {
                    _launchedProcess = null;
                    shouldDispose = true;
                }
            }

            if (!shouldDispose)
            {
                return;
            }

            _logger.LogInformation("SPA development server process {ProcessId} exited with code {ExitCode}.", process.Id, exitCode);
            process.Exited -= HandleLaunchedProcessExited;
            process.Dispose();
        }
    }
}
