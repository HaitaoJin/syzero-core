using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System;

namespace SyZero.AspNetCore.SpaProxy
{
    internal sealed class SpaProxyStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                builder.UseMiddleware<SpaProxyMiddleware>();
                next(builder);
            };
        }
    }
}
