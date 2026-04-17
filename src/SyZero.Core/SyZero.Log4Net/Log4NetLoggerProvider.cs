using log4net;
using log4net.Repository;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace SyZero.Log4Net
{
    public sealed class Log4NetLoggerProvider : ILoggerProvider
    {
        private readonly ILoggerRepository _repository;
        private readonly string _defaultLoggerName;
        private readonly ConcurrentDictionary<string, Log4NetLogger> _loggers = new ConcurrentDictionary<string, Log4NetLogger>(StringComparer.Ordinal);
        private bool _disposed;

        public Log4NetLoggerProvider(string defaultLoggerName = "DefaultLogger")
            : this(LogManager.GetRepository(), defaultLoggerName)
        {
        }

        public Log4NetLoggerProvider(ILoggerRepository repository, string defaultLoggerName = "DefaultLogger")
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            if (string.IsNullOrWhiteSpace(defaultLoggerName))
            {
                throw new ArgumentException("Default logger name cannot be empty.", nameof(defaultLoggerName));
            }

            _defaultLoggerName = defaultLoggerName;
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Log4NetLoggerProvider));
            }

            var loggerName = string.IsNullOrWhiteSpace(categoryName) ? _defaultLoggerName : categoryName;
            return _loggers.GetOrAdd(loggerName, name => new Log4NetLogger(LogManager.GetLogger(_repository.Name, name)));
        }

        public void Dispose()
        {
            _disposed = true;
            _loggers.Clear();
        }
    }
}
