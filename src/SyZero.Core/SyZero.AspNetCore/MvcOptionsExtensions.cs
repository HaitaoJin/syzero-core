using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security.Claims;
using System.Threading;
using SyZero.AspNetCore;
using SyZero.AspNetCore.Middleware;
using SyZero.Cache;
using SyZero.Runtime.Session;
using SyZero.Util;

namespace Microsoft.AspNetCore.Builder
{
    public static class MvcOptionsExtensions
    {
        /// <summary>
        /// 扩展方法
        /// </summary>
        /// <param name="opts"></param>
        /// <param name="routeAttribute"></param>
        public static void UseCentralRoutePrefix(this MvcOptions opts, IRouteTemplateProvider routeAttribute)
        {
            ArgumentNullException.ThrowIfNull(opts);
            ArgumentNullException.ThrowIfNull(routeAttribute);

            // 添加我们自定义 实现IApplicationModelConvention的RouteConvention
            opts.Conventions.Insert(0, new RouteConvention(routeAttribute));
        }

        /// <summary>
        /// 权限中间件 - 扩展方法
        /// </summary>
        /// <param name="app"></param>
        public static IApplicationBuilder UseSyAuthMiddleware(this IApplicationBuilder app, Func<ISySession, string> cacheKeyFun = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseMiddleware<SyAuthMiddleware>();
            if (cacheKeyFun != null)
            {
                app.Use(async (context, next) =>
                {
                    var sySeesion = SyZeroUtil.GetScopeService<ISySession>();
                    if (sySeesion?.UserId != null)
                    {
                        var cache = context.RequestServices.GetService<ICache>();
                        if (cache != null)
                        {
                            var cacheKey = cacheKeyFun(sySeesion);
                            if (string.IsNullOrWhiteSpace(cacheKey) || !cache.Exist(cacheKey))
                            {
                                ResetPrincipal(context);
                            }
                        }
                    }

                    await next.Invoke();
                });
            }
            return app;
        }

        private static void ResetPrincipal(HttpContext context)
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            Thread.CurrentPrincipal = anonymous;
            context.User = anonymous;
        }
    }
}
