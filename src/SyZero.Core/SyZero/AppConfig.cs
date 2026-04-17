using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using SyZero.Configurations;

namespace SyZero
{
    public class AppConfig
    {
        public static IConfiguration Configuration { get; set; }
        //  public static readonly IConfiguration Configuration;

        static AppConfig()
        {
            if (Configuration == null)
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                Configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile($"appsettings.json", true, true)
                    .AddJsonFile($"appsettings.{environment}.json", true, true)
                    .AddEnvironmentVariables()
                    .Build();
            }
        }

        public static T GetSection<T>(string key) where T : class, new()
        {
            var obj = Configuration.GetSection(key).Get<T>();
            return obj;
        }

        public static string GetSection(string key)
        {
            return Configuration.GetValue<string>(key);
        }

        private static SyZeroServerOptions serverOptions;
        public static SyZeroServerOptions ServerOptions
        {
            get
            {
                if (serverOptions == null)
                {
                    SyZeroServerOptions options = GetSection<SyZeroServerOptions>("SyZero") ?? new SyZeroServerOptions();
                    if (string.IsNullOrEmpty(options.Ip))
                    {
                        options.Ip = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                                      .Select(p => p.GetIPProperties())
                                      .SelectMany(p => p.UnicastAddresses)
                                      .Where(p => p.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || p.DuplicateAddressDetectionState == DuplicateAddressDetectionState.Preferred) && !System.Net.IPAddress.IsLoopback(p.Address))
                                      .FirstOrDefault()?.Address.ToString();
                    }
                    if (string.IsNullOrEmpty(options.WanIp))
                    {
                        options.WanIp = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                                      .Select(p => p.GetIPProperties())
                                      .SelectMany(p => p.UnicastAddresses)
                                      .Where(p => p.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || p.DuplicateAddressDetectionState == DuplicateAddressDetectionState.Preferred) && !System.Net.IPAddress.IsLoopback(p.Address))
                                      .FirstOrDefault()?.Address.ToString();
                    }
                    serverOptions = options;
                }
                return serverOptions;
            }
        }

        private static SyZeroConnectionOptions connectionOptions;
        public static SyZeroConnectionOptions ConnectionOptions
        {
            get
            {
                if (connectionOptions == null)
                {
                    SyZeroConnectionOptions options = GetSection<SyZeroConnectionOptions>("ConnectionString");
                    connectionOptions = options;
                }
                return connectionOptions;
            }
        }
    }
}
