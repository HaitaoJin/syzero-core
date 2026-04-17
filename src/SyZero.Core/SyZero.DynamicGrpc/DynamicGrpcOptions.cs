using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SyZero.DynamicGrpc
{
    /// <summary>
    /// Dynamic gRPC 配置选项
    /// </summary>
    public class DynamicGrpcOptions
    {
        /// <summary>
        /// 配置节名称
        /// </summary>
        public const string SectionName = "DynamicGrpc";

        /// <summary>
        /// 初始化默认配置
        /// </summary>
        public DynamicGrpcOptions()
        {
            RemoveServicePostfixes = new List<string>(GrpcConsts.DefaultServicePostfixes);
            RemoveMethodPostfixes = new List<string>(GrpcConsts.DefaultMethodPostfixes);
            DefaultServicePrefix = GrpcConsts.DefaultServicePrefix;
            MaxReceiveMessageSize = GrpcConsts.DefaultMaxMessageSize;
            MaxSendMessageSize = GrpcConsts.DefaultMaxMessageSize;
            EnableDetailedErrors = false;
            AssemblyOptions = new Dictionary<Assembly, AssemblyDynamicGrpcOptions>();
        }

        /// <summary>
        /// 默认服务前缀
        /// </summary>
        public string DefaultServicePrefix { get; set; }

        /// <summary>
        /// 默认区域/模块名称
        /// </summary>
        public string DefaultAreaName { get; set; }

        /// <summary>
        /// 需要移除的服务名称后缀
        /// <para>默认值: {"AppService", "ApplicationService", "Service", "GrpcService"}</para>
        /// </summary>
        public List<string> RemoveServicePostfixes { get; set; }

        /// <summary>
        /// 需要移除的方法名称后缀
        /// <para>默认值: {"Async"}</para>
        /// </summary>
        public List<string> RemoveMethodPostfixes { get; set; }

        /// <summary>
        /// 最大接收消息大小（字节）
        /// <para>默认值: 4MB</para>
        /// </summary>
        public int? MaxReceiveMessageSize { get; set; }

        /// <summary>
        /// 最大发送消息大小（字节）
        /// <para>默认值: 4MB</para>
        /// </summary>
        public int? MaxSendMessageSize { get; set; }

        /// <summary>
        /// 是否启用详细错误信息
        /// <para>默认值: false（生产环境建议关闭）</para>
        /// </summary>
        public bool EnableDetailedErrors { get; set; }

        /// <summary>
        /// 程序集级别的 Dynamic gRPC 选项
        /// </summary>
        public Dictionary<Assembly, AssemblyDynamicGrpcOptions> AssemblyOptions { get; }

        /// <summary>
        /// 自定义服务名称处理函数
        /// </summary>
        public Func<string, string> GetServiceName { get; set; }

        /// <summary>
        /// 自定义方法名称处理函数
        /// </summary>
        public Func<string, string> GetMethodName { get; set; }

        /// <summary>
        /// 验证所有配置是否有效
        /// </summary>
        /// <exception cref="ArgumentException">配置无效时抛出</exception>
        public void Validate()
        {
            DefaultServicePrefix ??= string.Empty;
            DefaultAreaName ??= string.Empty;

            if (RemoveServicePostfixes == null)
            {
                throw new ArgumentException($"{nameof(RemoveServicePostfixes)} 不能为 null。");
            }

            if (RemoveMethodPostfixes == null)
            {
                throw new ArgumentException($"{nameof(RemoveMethodPostfixes)} 不能为 null。");
            }

            RemoveServicePostfixes = RemoveServicePostfixes
                .Where(postfix => !string.IsNullOrWhiteSpace(postfix))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            RemoveMethodPostfixes = RemoveMethodPostfixes
                .Where(postfix => !string.IsNullOrWhiteSpace(postfix))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (MaxReceiveMessageSize.HasValue && MaxReceiveMessageSize.Value <= 0)
            {
                throw new ArgumentException($"{nameof(MaxReceiveMessageSize)} 必须大于 0。");
            }

            if (MaxSendMessageSize.HasValue && MaxSendMessageSize.Value <= 0)
            {
                throw new ArgumentException($"{nameof(MaxSendMessageSize)} 必须大于 0。");
            }
        }

        /// <summary>
        /// 添加程序集级别的 Dynamic gRPC 选项
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <param name="servicePrefix">服务前缀</param>
        /// <exception cref="ArgumentNullException">assembly 为 null 时抛出</exception>
        public void AddAssemblyOptions(Assembly assembly, string servicePrefix = null)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            AssemblyOptions[assembly] = new AssemblyDynamicGrpcOptions(servicePrefix);
        }
    }
}
