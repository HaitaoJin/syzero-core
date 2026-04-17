namespace SyZero.Redis
{
    /// <summary>
    /// Redis 事件总线配置选项
    /// </summary>
    public class RedisEventBusOptions
    {
        /// <summary>
        /// 配置节名称
        /// </summary>
        public const string SectionName = "RedisEventBus";

        /// <summary>
        /// Redis 发布/订阅频道前缀
        /// </summary>
        public string ChannelPrefix { get; set; } = "SyZero:EventBus:";

        /// <summary>
        /// 验证配置
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ChannelPrefix))
            {
                ChannelPrefix = "SyZero:EventBus:";
            }
        }
    }
}
