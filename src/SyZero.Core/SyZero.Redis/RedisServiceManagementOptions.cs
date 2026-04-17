using System;

namespace SyZero.Redis
{
    /// <summary>
    /// Redis 服务管理配置选项
    /// </summary>
    public class RedisServiceManagementOptions
    {
        /// <summary>
        /// 配置节名称
        /// </summary>
        public const string SectionName = "RedisServiceManagement";

        /// <summary>
        /// 服务注册 Key 前缀
        /// <para>默认值: syzero:services:</para>
        /// </summary>
        public string KeyPrefix { get; set; } = "syzero:services:";

        /// <summary>
        /// Leader 选举 Key 前缀
        /// <para>默认值: syzero:leader:</para>
        /// </summary>
        public string LeaderKeyPrefix { get; set; } = "syzero:leader:";

        /// <summary>
        /// 服务列表 Key
        /// <para>默认值: syzero:service:names</para>
        /// </summary>
        public string ServiceNamesKey { get; set; } = "syzero:service:names";

        /// <summary>
        /// 是否启用健康检查
        /// <para>默认值: true</para>
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;

        /// <summary>
        /// 健康检查间隔（秒）
        /// <para>默认值: 10</para>
        /// </summary>
        public int HealthCheckIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// 健康检查超时时间（秒）
        /// <para>默认值: 5</para>
        /// </summary>
        public int HealthCheckTimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// 服务实例过期时间（秒），超过此时间未心跳的实例将被标记为不健康
        /// <para>默认值: 30</para>
        /// </summary>
        public int ServiceExpireSeconds { get; set; } = 30;

        /// <summary>
        /// 是否自动清理过期服务
        /// <para>默认值: true</para>
        /// </summary>
        public bool AutoCleanExpiredServices { get; set; } = true;

        /// <summary>
        /// 自动清理间隔（秒）
        /// <para>默认值: 300（5分钟）</para>
        /// </summary>
        public int AutoCleanIntervalSeconds { get; set; } = 300;

        /// <summary>
        /// 服务实例清理时间（秒），超过此时间未心跳的实例将被自动移除
        /// <para>默认值: 600（10分钟），应大于 ServiceExpireSeconds</para>
        /// </summary>
        public int ServiceCleanSeconds { get; set; } = 600;

        /// <summary>
        /// 是否启用 Leader 选举（多实例时只有 Leader 执行健康检查和清理）
        /// <para>默认值: true</para>
        /// </summary>
        public bool EnableLeaderElection { get; set; } = true;

        /// <summary>
        /// Leader 锁过期时间（秒）
        /// <para>默认值: 30</para>
        /// </summary>
        public int LeaderLockExpireSeconds { get; set; } = 30;

        /// <summary>
        /// Leader 锁续期间隔（秒）
        /// <para>默认值: 10，应小于 LeaderLockExpireSeconds</para>
        /// </summary>
        public int LeaderLockRenewIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// 是否启用发布/订阅通知
        /// <para>默认值: true</para>
        /// </summary>
        public bool EnablePubSub { get; set; } = true;

        /// <summary>
        /// 发布/订阅频道前缀
        /// <para>默认值: syzero:pubsub:service:</para>
        /// </summary>
        public string PubSubChannelPrefix { get; set; } = "syzero:pubsub:service:";

        /// <summary>
        /// 验证配置
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(KeyPrefix))
            {
                throw new ArgumentException("KeyPrefix 不能为空");
            }

            if (string.IsNullOrWhiteSpace(LeaderKeyPrefix))
            {
                throw new ArgumentException("LeaderKeyPrefix 不能为空");
            }

            if (string.IsNullOrWhiteSpace(ServiceNamesKey))
            {
                throw new ArgumentException("ServiceNamesKey 不能为空");
            }

            if (string.IsNullOrWhiteSpace(PubSubChannelPrefix))
            {
                throw new ArgumentException("PubSubChannelPrefix 不能为空");
            }

            if (HealthCheckIntervalSeconds < 1)
            {
                throw new ArgumentException("HealthCheckIntervalSeconds 必须大于 0");
            }

            if (HealthCheckTimeoutSeconds < 1)
            {
                throw new ArgumentException("HealthCheckTimeoutSeconds 必须大于 0");
            }

            if (ServiceExpireSeconds < HealthCheckIntervalSeconds)
            {
                throw new ArgumentException("ServiceExpireSeconds 必须大于 HealthCheckIntervalSeconds");
            }

            if (AutoCleanIntervalSeconds < 1)
            {
                throw new ArgumentException("AutoCleanIntervalSeconds 必须大于 0");
            }

            if (ServiceCleanSeconds < ServiceExpireSeconds)
            {
                throw new ArgumentException("ServiceCleanSeconds 必须大于或等于 ServiceExpireSeconds");
            }

            if (LeaderLockExpireSeconds < 1)
            {
                throw new ArgumentException("LeaderLockExpireSeconds 必须大于 0");
            }

            if (LeaderLockRenewIntervalSeconds >= LeaderLockExpireSeconds)
            {
                throw new ArgumentException("LeaderLockRenewIntervalSeconds 必须小于 LeaderLockExpireSeconds");
            }
        }
    }
}
