using System.Threading;
using System.Threading.Tasks;

namespace SyZero.AspNetCore.SpaProxy
{
    internal interface ISpaProxyServerProbe
    {
        Task<bool> CanReachServerAsync(string serverUrl, CancellationToken cancellationToken);
    }
}
