using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class DefaultSpaProxyProcessFactory : ISpaProxyProcessFactory
    {
        public ISpaProxyProcess Create(SpaProxyServerInfo serverInfo)
        {
            return new SystemSpaProxyProcess(new Process
            {
                StartInfo = BuildProcessStartInfo(serverInfo),
                EnableRaisingEvents = true
            });
        }

        private static ProcessStartInfo BuildProcessStartInfo(SpaProxyServerInfo serverInfo)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + serverInfo.LaunchCommand,
                    WorkingDirectory = serverInfo.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            return new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c \"" + serverInfo.LaunchCommand.Replace("\"", "\\\"") + "\"",
                WorkingDirectory = serverInfo.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
    }
}
