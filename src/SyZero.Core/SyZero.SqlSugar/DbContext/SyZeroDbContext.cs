using Microsoft.Extensions.Logging;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using SyZero.Util;
namespace SyZero.SqlSugar.DbContext
{
    public class SyZeroDbContext : SqlSugarClient, ISyZeroDbContext
    {
        private readonly ILogger<SyZeroDbContext> _logger;
        public SyZeroDbContext(ConnectionConfig config):base(config)
        {
            _logger = SyZeroUtil.GetService<ILogger<SyZeroDbContext>>();

            this.Context.Aop.OnLogExecuted = (sql, pars) =>//SQL执行后
            {
                WriteLog(sql, pars, this.Context.Ado.SqlExecutionTime.TotalMilliseconds);
            };
            this.Context.Aop.OnError = (exp) =>//SQL报错
            {
                _logger.LogError("sql执行出错：" + exp.Message);
            };
        }
        //public SyZeroDbContext()
        //{
        //    Database = new SqlSugarClient(new ConnectionConfig()
        //    {

        //        ConnectionString = AppConfig.ConnectionOptions.Master,
        //        DbType = (DbType)AppConfig.ConnectionOptions.Type,
        //        IsAutoCloseConnection = true,
        //        InitKeyType = InitKeyType.Attribute,
        //        SlaveConnectionConfigs = AppConfig.ConnectionOptions.Slave.Select(p => new SlaveConnectionConfig()
        //        {
        //            HitRate = p.Value,
        //            ConnectionString = p.Key
        //        }).ToList()
        //    }); ;
        //}
        //public SqlSugarClient Database;//用来处理事务多表查询和复杂的操作

        /// <summary>
        /// 打印执行日志
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="sugarParameters"></param>
        /// <param name="sqlExecutionTime"></param>
        private void WriteLog(string sql, SugarParameter[] sugarParameters, double sqlExecutionTime)
        {
            foreach (var p in sugarParameters)
            {
                if (p.DbType == System.Data.DbType.Int16
                    || p.DbType == System.Data.DbType.Int32
                    || p.DbType == System.Data.DbType.Int64
                    || p.DbType == System.Data.DbType.Decimal
                    || p.DbType == System.Data.DbType.Double
                    || p.DbType == System.Data.DbType.Single
                    || p.DbType == System.Data.DbType.UInt16
                    || p.DbType == System.Data.DbType.UInt32
                    || p.DbType == System.Data.DbType.UInt64
                    || p.DbType == System.Data.DbType.VarNumeric)
                {
                    sql = sql.Replace(p.ParameterName, p.Value == null ? "" : p.Value.ToString());
                }
                else
                {
                    sql = sql.Replace(p.ParameterName, $"'{(p.Value == null ? "" : p.Value)}'");
                }
            }
            var content = $"\r\nsql : 执行本次SQL消耗 {sqlExecutionTime} 毫秒,执行 SQL 内容如下:\r\n{sql};\n";
            _logger.LogDebug(content);
        }
    }
}
