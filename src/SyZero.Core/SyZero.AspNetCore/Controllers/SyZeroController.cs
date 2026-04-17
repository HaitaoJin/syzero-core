using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using SyZero.Cache;
using SyZero.ObjectMapper;
using SyZero.Runtime.Session;
using SyZero.Util;

namespace SyZero.AspNetCore.Controllers
{
    public class SyZeroController : Controller
    {
        private ICache _cache;
        private IObjectMapper _objectMapper;
        private ILogger _logger;
        private ISySession _sySession;

        /// <summary>
        /// 缓存
        /// </summary>
        public ICache Cache
        {
            get => _cache ??= GetServiceProvider().GetService(typeof(ICache)) as ICache;
            set => _cache = value;
        }

        /// <summary>
        /// 对象映射
        /// </summary>
        public IObjectMapper ObjectMapper
        {
            get => _objectMapper ??= GetRequiredService<IObjectMapper>();
            set => _objectMapper = value;
        }

        /// <summary>
        /// 日志
        /// </summary>
        public ILogger Logger
        {
            get
            {
                if (_logger != null)
                {
                    return _logger;
                }

                var loggerFactory = GetServiceProvider().GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                _logger = loggerFactory?.CreateLogger(GetType()) ?? NullLogger.Instance;
                return _logger;
            }
            set => _logger = value;
        }

        /// <summary>
        /// Sy会话
        /// </summary>
        public ISySession SySession
        {
            get => _sySession ??= GetRequiredService<ISySession>();
            set => _sySession = value;
        }

        private IServiceProvider GetServiceProvider()
        {
            return HttpContext?.RequestServices
                   ?? SyZeroUtil.ServiceProvider
                   ?? throw new InvalidOperationException("SyZero controller services have not been initialized.");
        }

        private TService GetRequiredService<TService>() where TService : class
        {
            return GetServiceProvider().GetService(typeof(TService)) as TService
                   ?? throw new InvalidOperationException($"Required service '{typeof(TService).FullName}' was not registered.");
        }
    }
}
