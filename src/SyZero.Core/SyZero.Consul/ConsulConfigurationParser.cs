using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using NConsul;
using NConsul.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.Consul
{
    public sealed class ConsulConfigurationParser
    {
        private readonly IConsulConfigurationSource consulConfigurationSource;
        private readonly Func<string, QueryOptions, CancellationToken, Task<QueryResult<KVPair>>> kvPairLoader;
        private readonly object lastIndexLock = new object();
        private ulong lastIndex;
        private ConfigurationReloadToken reloadToken = new ConfigurationReloadToken();

        public ConsulConfigurationParser(
            IConsulConfigurationSource consulConfigurationSource,
            Func<string, QueryOptions, CancellationToken, Task<QueryResult<KVPair>>> kvPairLoader = null)
        {
            this.consulConfigurationSource = consulConfigurationSource;
            this.kvPairLoader = kvPairLoader;
        }

        /// <summary>
        /// 获取并转换Consul配置信息
        /// </summary>
        /// <param name="reloading"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public async Task<IDictionary<string, string>> GetConfig(bool reloading, IConsulConfigurationSource source)
        {
            QueryResult<KVPair> kvPair = await this.GetKvPairs(source.ServiceKey, source.QueryOptions, source.CancellationToken).ConfigureAwait(false);
            switch (kvPair?.Response)
            {
                case null when !source.Optional:
                    {
                        if (!reloading)
                        {
                            throw new FormatException("Error_InvalidService" + source.ServiceKey);
                        }

                        return new Dictionary<string, string>();
                    }
                case null:
                    return new Dictionary<string, string>();
                default:
                    this.UpdateLastIndex(kvPair);

                    return JsonConfigurationFileParser.Parse(
                        source.ServiceKey,
                        new MemoryStream(kvPair.Response.Value ?? Encoding.UTF8.GetBytes("{}")));
            }
        }

        /// <summary>
        /// Consul配置信息监控
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IChangeToken Watch(string key, CancellationToken cancellationToken)
        {
            Task.Run(() => this.RefreshForChanges(key, cancellationToken), cancellationToken);

            return this.reloadToken;
        }

        private async Task<QueryResult<KVPair>> GetKvPairs(string key, QueryOptions queryOptions, CancellationToken cancellationToken)
        {
            if (this.kvPairLoader != null)
            {
                return await this.kvPairLoader(key, queryOptions, cancellationToken).ConfigureAwait(false);
            }

            using (IConsulClient consulClient = new ConsulClient(
                this.consulConfigurationSource.ConsulClientConfiguration,
                this.consulConfigurationSource.ConsulHttpClient,
                this.consulConfigurationSource.ConsulHttpClientHandler))
            {
                QueryResult<KVPair> result = await consulClient.KV.Get(key, queryOptions, cancellationToken).ConfigureAwait(false);

                switch (result.StatusCode)
                {
                    case HttpStatusCode.OK:
                    case HttpStatusCode.NotFound:
                        return result;

                    default:
                        throw new FormatException("Error_Request" + key);
                }
            }
        }

        private async Task<bool> IsValueChanged(string key, CancellationToken cancellationToken)
        {
            QueryOptions queryOptions;
            lock (this.lastIndexLock)
            {
                queryOptions = new QueryOptions
                {
                    WaitIndex = this.lastIndex
                };
            }

            QueryResult<KVPair> result = await this.GetKvPairs(key, queryOptions, cancellationToken).ConfigureAwait(false);

            return result != null && this.UpdateLastIndex(result);
        }

        private async Task RefreshForChanges(string key, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (await this.IsValueChanged(key, cancellationToken).ConfigureAwait(false))
                {
                    ConfigurationReloadToken previousToken = Interlocked.Exchange(ref this.reloadToken, new ConfigurationReloadToken());
                    previousToken.OnReload();

                    return;
                }
            }
        }

        private bool UpdateLastIndex(QueryResult queryResult)
        {
            lock (this.lastIndexLock)
            {
                if (queryResult.LastIndex > this.lastIndex)
                {
                    this.lastIndex = queryResult.LastIndex;
                    return true;
                }
            }

            return false;
        }
    }
}
