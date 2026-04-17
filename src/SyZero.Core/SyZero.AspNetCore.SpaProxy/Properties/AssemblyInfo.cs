using Microsoft.AspNetCore.Hosting;
using System.Runtime.CompilerServices;

[assembly: HostingStartup(typeof(SyZero.AspNetCore.SpaProxy.SyZeroSpaProxyHostingStartup))]
[assembly: InternalsVisibleTo("SyZero.Tests")]
