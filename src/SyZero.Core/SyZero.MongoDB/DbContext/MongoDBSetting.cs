using System;
using System.Collections.Generic;
using System.Linq;

namespace SyZero.MongoDB
{
    public class MongoOptions
    {
        public string DataBase { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public List<MongoServers> Services { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DataBase))
            {
                throw new ArgumentException("MongoDB: DataBase 不能为空");
            }

            if (Services == null || Services.Count == 0)
            {
                throw new ArgumentException("MongoDB: 至少需要配置一个 Services 节点");
            }

            if (Services.Any(service => string.IsNullOrWhiteSpace(service.Host)))
            {
                throw new ArgumentException("MongoDB: Services.Host 不能为空");
            }
        }
    }

    public class MongoServers
    {
        public string Host { get; set; }

        public int Port { get; set; } = 27017;
    }
}
