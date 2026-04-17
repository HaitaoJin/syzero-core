using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.IO;
using System.Net.Sockets;

namespace SyZero.RabbitMQ
{
    /// <summary>
    /// RabbitMQ 持久化连接
    /// </summary>
    public class RabbitMQPersistentConnection : IDisposable
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger<RabbitMQPersistentConnection> _logger;
        private readonly int _retryCount;
        private IConnection _connection;
        private bool _disposed = false;
        private readonly object _lock = new object();

        /// <summary>
        /// 构造函数
        /// </summary>
        public RabbitMQPersistentConnection(
            IConnectionFactory connectionFactory,
            ILogger<RabbitMQPersistentConnection> logger,
            int retryCount = 5)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryCount = retryCount;
        }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _connection != null && _connection.IsOpen && !_disposed;

        /// <summary>
        /// 创建 Model
        /// </summary>
        public IModel CreateModel()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("RabbitMQ 连接不可用，无法创建 Model");
            }

            return _connection.CreateModel();
        }

        /// <summary>
        /// 尝试连接
        /// </summary>
        public bool TryConnect()
        {
            _logger.LogInformation("RabbitMQ 客户端正在尝试连接");

            lock (_lock)
            {
                if (IsConnected)
                {
                    return true;
                }

                var maxRetryCount = Math.Max(_retryCount, 1);
                var retryCount = 0;

                while (retryCount < maxRetryCount)
                {
                    try
                    {
                        var connection = _connectionFactory.CreateConnection();
                        if (connection != null && connection.IsOpen)
                        {
                            ReplaceConnection(connection);

                            _logger.LogInformation(
                            $"RabbitMQ 客户端已连接到 '{_connection.Endpoint.HostName}' 并订阅失败事件");

                            return true;
                        }

                        connection?.Dispose();
                    }
                    catch (SocketException ex)
                    {
                        retryCount++;
                        _logger.LogWarning(ex, 
                            $"RabbitMQ 连接失败，正在重试... ({retryCount}/{maxRetryCount})");
                    }
                    catch (BrokerUnreachableException ex)
                    {
                        retryCount++;
                        _logger.LogWarning(ex,
                            $"RabbitMQ Broker 不可达，正在重试... ({retryCount}/{maxRetryCount})");
                    }

                    if (retryCount < maxRetryCount)
                    {
                        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                    }
                }

                _logger.LogCritical($"无法建立 RabbitMQ 连接，已重试 {maxRetryCount} 次");
                return false;
            }
        }

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed) return;

            _logger.LogWarning("RabbitMQ 连接被阻塞，正在尝试重新连接...");

            TryConnect();
        }

        private void OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            if (_disposed) return;

            _logger.LogWarning(e.Exception, "RabbitMQ 回调异常，正在尝试重新连接...");

            TryConnect();
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            if (_disposed) return;

            _logger.LogWarning($"RabbitMQ 连接关闭，正在尝试重新连接... 原因: {reason.ReplyText}");

            TryConnect();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                DetachConnectionEvents(_connection);
                _connection?.Dispose();
                _logger.LogInformation("RabbitMQ 连接已释放");
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex, "释放 RabbitMQ 连接时发生异常");
            }
        }

        private void ReplaceConnection(IConnection connection)
        {
            DetachConnectionEvents(_connection);
            _connection?.Dispose();

            _connection = connection;
            _connection.ConnectionShutdown += OnConnectionShutdown;
            _connection.CallbackException += OnCallbackException;
            _connection.ConnectionBlocked += OnConnectionBlocked;
        }

        private void DetachConnectionEvents(IConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            connection.ConnectionShutdown -= OnConnectionShutdown;
            connection.CallbackException -= OnCallbackException;
            connection.ConnectionBlocked -= OnConnectionBlocked;
        }
    }
}
