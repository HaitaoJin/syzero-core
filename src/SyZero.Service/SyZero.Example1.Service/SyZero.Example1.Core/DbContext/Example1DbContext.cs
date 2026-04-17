using Microsoft.Extensions.Logging;
using SqlSugar;
using SyZero.SqlSugar.DbContext;

namespace SyZero.Example1.Core.DbContext
{
    public class Example1DbContext : SyZeroDbContext
    {
        public Example1DbContext(ConnectionConfig config, ILoggerFactory loggerFactory)
            : base(config, loggerFactory)
        {
        }
    }
}
