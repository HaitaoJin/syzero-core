using Microsoft.Extensions.Logging;
using SqlSugar;
using System;
using System.Linq;

namespace SyZero.SqlSugar.DbContext
{
    public class SyZeroDbContext : SqlSugarClient, ISyZeroDbContext
    {
        private readonly ILogger _logger;

        public SyZeroDbContext(ConnectionConfig config, ILoggerFactory loggerFactory)
            : base(config)
        {
            _logger = loggerFactory.CreateLogger(GetType().FullName ?? typeof(SyZeroDbContext).FullName);

            Context.Aop.OnLogExecuted = (sql, pars) =>
            {
                WriteLog(sql, pars ?? Array.Empty<SugarParameter>(), Context.Ado.SqlExecutionTime.TotalMilliseconds);
            };
            Context.Aop.OnError = exp =>
            {
                _logger.LogError(exp, "sql执行出错");
            };
        }

        private void WriteLog(string sql, SugarParameter[] sugarParameters, double sqlExecutionTime)
        {
            var parameters = sugarParameters.Select(p => new
            {
                p.ParameterName,
                p.DbType,
                p.Value
            }).ToArray();

            _logger.LogDebug(
                "sql执行完成，耗时 {ElapsedMilliseconds} 毫秒。SQL: {Sql}; Parameters: {@Parameters}",
                sqlExecutionTime,
                sql,
                parameters);
        }
    }
}
