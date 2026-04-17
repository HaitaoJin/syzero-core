using System;
using System.Collections.Generic;
using System.Text;

namespace SyZero.Redis
{
    public class RedisOptions
    {
        public RedisType Type { get; set; }

        public string Master { get; set; }

        public List<string> Slave { get; set; } = new List<string>();

        public List<string> Sentinel { get; set; } = new List<string>();

        public void Validate()
        {
            Slave ??= new List<string>();
            Sentinel ??= new List<string>();

            if (string.IsNullOrWhiteSpace(Master))
            {
                throw new ArgumentException("Redis Master 不能为空");
            }

            switch (Type)
            {
                case RedisType.MasterSlave:
                    break;
                case RedisType.Sentinel:
                    if (Sentinel.Count == 0)
                    {
                        throw new ArgumentException("Sentinel 模式至少需要一个 Sentinel 节点");
                    }
                    break;
                case RedisType.Cluster:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Type), Type, "不支持的 Redis 类型");
            }
        }
    }


    public enum RedisType
    {
        MasterSlave = 0,
        Sentinel = 1,
        Cluster = 2
    }
}
