using System;

namespace SyZero.RabbitMQ
{
    /// <summary>
    /// RabbitMQ 事件总线配置选项
    /// </summary>
    public class RabbitMQEventBusOptions
    {
        /// <summary>
        /// 配置节名称
        /// </summary>
        public const string SectionName = "RabbitMQ";

        /// <summary>
        /// RabbitMQ 主机地址
        /// <para>默认值: localhost</para>
        /// </summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>
        /// RabbitMQ 端口号
        /// <para>默认值: 5672</para>
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// 用户名
        /// <para>默认值: guest</para>
        /// </summary>
        public string UserName { get; set; } = "guest";

        /// <summary>
        /// 密码
        /// <para>默认值: guest</para>
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// 虚拟主机
        /// <para>默认值: /</para>
        /// </summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// 交换机名称
        /// <para>默认值: syzero_event_bus</para>
        /// </summary>
        public string ExchangeName { get; set; } = "syzero_event_bus";

        /// <summary>
        /// 交换机类型（direct, topic, fanout, headers）
        /// <para>默认值: topic</para>
        /// </summary>
        public string ExchangeType { get; set; } = "topic";

        /// <summary>
        /// 队列名称前缀
        /// <para>默认值: syzero</para>
        /// </summary>
        public string QueueNamePrefix { get; set; } = "syzero";

        /// <summary>
        /// 队列名称
        /// <para>默认值: null（自动生成）</para>
        /// </summary>
        public string QueueName { get; set; }

        /// <summary>
        /// 客户端提供的名称
        /// <para>默认值: SyZero.EventBus</para>
        /// </summary>
        public string ClientProvidedName { get; set; } = "SyZero.EventBus";

        /// <summary>
        /// 重试次数
        /// <para>默认值: 3</para>
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// 重试间隔（毫秒）
        /// <para>默认值: 1000</para>
        /// </summary>
        public int RetryIntervalMilliseconds { get; set; } = 1000;

        /// <summary>
        /// 预取数量
        /// <para>默认值: 1</para>
        /// </summary>
        public ushort PrefetchCount { get; set; } = 1;

        /// <summary>
        /// 是否持久化队列
        /// <para>默认值: true</para>
        /// </summary>
        public bool QueueDurable { get; set; } = true;

        /// <summary>
        /// 是否自动删除队列
        /// <para>默认值: false</para>
        /// </summary>
        public bool QueueAutoDelete { get; set; } = false;

        /// <summary>
        /// 是否持久化消息
        /// <para>默认值: true</para>
        /// </summary>
        public bool MessagePersistent { get; set; } = true;

        /// <summary>
        /// 是否自动应答
        /// <para>默认值: false</para>
        /// </summary>
        public bool AutoAck { get; set; } = false;

        /// <summary>
        /// 启用死信队列
        /// <para>默认值: true</para>
        /// </summary>
        public bool EnableDeadLetter { get; set; } = true;

        /// <summary>
        /// 死信交换机名称
        /// <para>默认值: syzero_event_bus_dlx</para>
        /// </summary>
        public string DeadLetterExchangeName { get; set; } = "syzero_event_bus_dlx";

        /// <summary>
        /// 消息TTL（毫秒）
        /// <para>默认值: null（不限制）</para>
        /// </summary>
        public int? MessageTTL { get; set; }

        /// <summary>
        /// 最大消息长度
        /// <para>默认值: null（不限制）</para>
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// 连接超时时间（毫秒）
        /// <para>默认值: 30000（30秒）</para>
        /// </summary>
        public int RequestedConnectionTimeout { get; set; } = 30000;

        /// <summary>
        /// 心跳间隔（秒）
        /// <para>默认值: 60</para>
        /// </summary>
        public ushort RequestedHeartbeat { get; set; } = 60;

        /// <summary>
        /// 验证并规范化配置
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                HostName = "localhost";
            }

            if (Port <= 0)
            {
                Port = 5672;
            }

            if (string.IsNullOrWhiteSpace(UserName))
            {
                UserName = "guest";
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                Password = "guest";
            }

            if (string.IsNullOrWhiteSpace(VirtualHost))
            {
                VirtualHost = "/";
            }

            if (string.IsNullOrWhiteSpace(ExchangeName))
            {
                ExchangeName = "syzero_event_bus";
            }

            if (string.IsNullOrWhiteSpace(ExchangeType))
            {
                ExchangeType = "topic";
            }

            if (string.IsNullOrWhiteSpace(QueueNamePrefix))
            {
                QueueNamePrefix = "syzero";
            }

            if (string.IsNullOrWhiteSpace(QueueName))
            {
                QueueName = null;
            }

            if (string.IsNullOrWhiteSpace(ClientProvidedName))
            {
                ClientProvidedName = "SyZero.EventBus";
            }

            if (RetryCount <= 0)
            {
                RetryCount = 3;
            }

            if (RetryIntervalMilliseconds < 0)
            {
                RetryIntervalMilliseconds = 1000;
            }

            if (PrefetchCount == 0)
            {
                PrefetchCount = 1;
            }

            if (EnableDeadLetter && string.IsNullOrWhiteSpace(DeadLetterExchangeName))
            {
                DeadLetterExchangeName = "syzero_event_bus_dlx";
            }

            if (MessageTTL <= 0)
            {
                MessageTTL = null;
            }

            if (MaxLength <= 0)
            {
                MaxLength = null;
            }

            if (RequestedConnectionTimeout <= 0)
            {
                RequestedConnectionTimeout = 30000;
            }

            if (RequestedHeartbeat == 0)
            {
                RequestedHeartbeat = 60;
            }
        }
    }
}
