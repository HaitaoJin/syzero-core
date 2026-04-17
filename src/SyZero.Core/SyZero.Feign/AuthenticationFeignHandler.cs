using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Runtime.Session;
using SyZero.Util;

namespace SyZero.Feign
{
    public class AuthenticationFeignHandler : DelegatingHandler
    {
        public AuthenticationFeignHandler(string serverName, HttpMessageHandler innerHandler = null) : base(innerHandler ?? new HttpClientHandler())
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization == null)
            {
                var sySession = SyZeroUtil.GetService<ISySession>();
                if (!string.IsNullOrWhiteSpace(sySession?.Token))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sySession.Token);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
