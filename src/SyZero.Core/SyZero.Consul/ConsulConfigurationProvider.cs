using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Threading;

namespace SyZero.Consul
{
    public sealed class ConsulConfigurationProvider : ConfigurationProvider
    {
        private readonly ConsulConfigurationParser configurationParser;
        private readonly IConsulConfigurationSource source;

        public ConsulConfigurationProvider(IConsulConfigurationSource source, ConsulConfigurationParser configurationParser)
        {
            this.configurationParser = configurationParser;
            this.source = source;

            if (source.ReloadOnChange)
            {
                ChangeToken.OnChange(
                    () => this.configurationParser.Watch(this.source.ServiceKey, this.source.CancellationToken),
                    this.ReloadConfiguration);
            }
        }

        public override void Load()
        {
            this.LoadCore(false);
        }

        private void ReloadConfiguration()
        {
            Console.WriteLine("--------- SyZero.Consul：检测到配置变更 => 开始重新加载配置");
            this.LoadCore(true);
            Console.WriteLine("--------- SyZero.Consul：检测到配置变更 => 完成");
            this.OnReload();
        }

        private void LoadCore(bool reloading)
        {
            try
            {
                if (reloading && this.source.ReloadDelay > 0)
                {
                    Thread.Sleep(this.source.ReloadDelay);
                }

                this.Data = this.configurationParser.GetConfig(reloading, this.source).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (AggregateException aggregateException)
            {
                if (aggregateException.InnerException != null)
                {
                    throw aggregateException.InnerException;
                }

                throw;
            }
        }
    }
}
