using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class SystemSpaProxyProcess : ISpaProxyProcess
    {
        private readonly Process _process;

        public SystemSpaProxyProcess(Process process)
        {
            _process = process;
            _process.Exited += HandleProcessExited;
        }

        public event EventHandler? Exited;

        public int ExitCode
        {
            get
            {
                try
                {
                    return _process.ExitCode;
                }
                catch (InvalidOperationException)
                {
                    return 0;
                }
            }
        }

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public int Id => _process.Id;

        public void Kill(bool entireProcessTree)
        {
            _process.Kill(entireProcessTree);
        }

        public void Start()
        {
            _process.Start();
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            return _process.WaitForExitAsync(cancellationToken);
        }

        public void Dispose()
        {
            _process.Exited -= HandleProcessExited;
            _process.Dispose();
        }

        private void HandleProcessExited(object? sender, EventArgs args)
        {
            Exited?.Invoke(this, args);
        }
    }
}
