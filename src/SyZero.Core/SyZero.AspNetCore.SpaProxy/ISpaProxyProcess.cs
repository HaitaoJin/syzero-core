using System;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.AspNetCore.SpaProxy
{
    internal interface ISpaProxyProcess : IDisposable
    {
        event EventHandler? Exited;

        int ExitCode { get; }

        bool HasExited { get; }

        int Id { get; }

        void Kill(bool entireProcessTree);

        void Start();

        Task WaitForExitAsync(CancellationToken cancellationToken);
    }
}
