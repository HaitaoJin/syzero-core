using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace SyZero.AspNetCore.SpaProxy
{
    public sealed class SyZeroSpaProxyHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISpaProxyServerProbe, HttpClientSpaProxyServerProbe>();
                services.AddSingleton<ISpaProxyProcessFactory, DefaultSpaProxyProcessFactory>();
                services.AddSingleton<SpaProxyLaunchManager>();
                services.AddHostedService<SpaProxyLaunchHostedService>();
                services.AddSingleton<IStartupFilter, SpaProxyStartupFilter>();
            });
        }
    }
}
