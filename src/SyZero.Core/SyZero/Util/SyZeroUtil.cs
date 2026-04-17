using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;

namespace SyZero.Util
{
    /// <summary>
    /// Autofac依赖注入服务
    /// </summary>
    public class SyZeroUtil
    {
        private static readonly AsyncLocal<IServiceProvider> ScopeServiceProvider = new AsyncLocal<IServiceProvider>();

        /// <summary>
        /// Autofac依赖注入静态服务
        /// </summary>
        public static IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// 设置当前异步上下文中的作用域服务提供者
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public static IDisposable BeginScope(IServiceProvider serviceProvider)
        {
            var previousServiceProvider = ScopeServiceProvider.Value;
            ScopeServiceProvider.Value = serviceProvider;
            return new DisposeAction(() => ScopeServiceProvider.Value = previousServiceProvider);
        }

        /// <summary>
        /// 获取服务(Single)
        /// </summary>
        /// <typeparam name="T">接口类型</typeparam>
        /// <returns></returns>
        public static T GetService<T>() where T : class
        {
            return GetCurrentServiceProvider().GetService<T>();
        }

        /// <summary>
        /// 获取单例
        /// </summary>
        /// <typeparam name="T">接口类型</typeparam>
        /// <returns></returns>
        public static T GetRequiredService<T>() where T : class
        {
            return GetCurrentServiceProvider().GetRequiredService<T>();
        }

        /// <summary>
        /// 获取服务(请求生命周期内)
        /// </summary>
        /// <typeparam name="T">接口类型</typeparam>
        /// <returns></returns>
        public static T GetScopeService<T>() where T : class
        {
            return GetCurrentServiceProvider().GetService<T>();
        }

        private static IServiceProvider GetCurrentServiceProvider()
        {
            return ScopeServiceProvider.Value ?? ServiceProvider ?? throw new InvalidOperationException("SyZero service provider has not been initialized.");
        }

        private sealed class DisposeAction : IDisposable
        {
            private readonly Action _onDispose;

            public DisposeAction(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                _onDispose();
            }
        }
    }
}
