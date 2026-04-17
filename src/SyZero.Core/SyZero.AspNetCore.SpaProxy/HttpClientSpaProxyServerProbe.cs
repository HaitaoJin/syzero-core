using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class HttpClientSpaProxyServerProbe : ISpaProxyServerProbe
    {
        private static readonly HttpClient HttpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        })
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        public async Task<bool> CanReachServerAsync(string serverUrl, CancellationToken cancellationToken)
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
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
