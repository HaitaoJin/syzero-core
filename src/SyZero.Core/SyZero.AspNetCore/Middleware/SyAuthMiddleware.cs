using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Runtime.Security;
using SyZero.Runtime.Session;
using SyZero.Util;

namespace SyZero.AspNetCore.Middleware
{
    /// <summary>
    /// 权限中间件
    /// </summary>
    public class SyAuthMiddleware : IMiddleware
    {
        public IToken _token;

        public SyAuthMiddleware(IToken token)
        {
            this._token = token;
        }
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            using (SyZeroUtil.BeginScope(context.RequestServices))
            {
                context.User = null;
                if (context.Request.Headers.TryGetValue("Authorization", out var token) && token.ToString().StartsWith("Bearer "))
                {
                    var tokenString = token.ToString().Replace("Bearer ", string.Empty);

                    // 如果令牌有效，则将用户信息添加到上下文中
                    var claimsPrincipal = _token.GetPrincipal(tokenString);

                    if (claimsPrincipal != null)
                    {
                        context.User = claimsPrincipal;
                        SyZeroUtil.GetScopeService<ISySession>().Parse(claimsPrincipal);
                    }
                }
                await next.Invoke(context);
            }
        }
    }
}
