using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class SpaProxyLaunchHostedService : IHostedService
    {
        private readonly IHostEnvironment _environment;
        private readonly SpaProxyLaunchManager _launchManager;
        private readonly ILogger<SpaProxyLaunchHostedService> _logger;

        public SpaProxyLaunchHostedService(
            IHostEnvironment environment,
            SpaProxyLaunchManager launchManager,
            ILogger<SpaProxyLaunchHostedService> logger)
        {
            _environment = environment;
            _launchManager = launchManager;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var serverInfo = _launchManager.ServerInfo;
            if (!_environment.IsDevelopment() || serverInfo == null || !serverInfo.IsConfigured)
            {
                return;
            }

            _logger.LogInformation("Ensuring SPA development server is running at '{ServerUrl}'.", serverInfo.ServerUrl);
            await _launchManager.EnsureServerStartedAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
