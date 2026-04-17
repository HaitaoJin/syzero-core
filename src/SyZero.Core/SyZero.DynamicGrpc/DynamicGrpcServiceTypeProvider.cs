using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using SyZero.Application.Attributes;
using SyZero.Application.Service;
using SyZero.Client;
using SyZero.DynamicGrpc.Helpers;

namespace SyZero.DynamicGrpc
{
    /// <summary>
    /// 动态 gRPC 服务类型提供程序
    /// 用于识别和过滤可作为动态 gRPC 服务的类型
    /// </summary>
    public class DynamicGrpcServiceTypeProvider
    {
        private readonly DynamicGrpcOptions _options;

        // 缓存类型判断结果，避免重复反射
        private static readonly ConcurrentDictionary<Type, bool> _serviceCache = new();

        /// <summary>
        /// 使用指定选项初始化
        /// </summary>
        /// <param name="options">Dynamic gRPC 选项</param>
        public DynamicGrpcServiceTypeProvider(DynamicGrpcOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 判断指定类型是否可以作为动态 gRPC 服务
        /// </summary>
        /// <param name="typeInfo">类型信息</param>
        /// <returns>如果类型是有效的动态 gRPC 服务则返回 true</returns>
        public bool IsGrpcService(TypeInfo typeInfo)
        {
            if (typeInfo == null)
            {
                throw new ArgumentNullException(nameof(typeInfo));
            }

            var type = typeInfo.AsType();
            return _serviceCache.GetOrAdd(type, t => IsValidDynamicGrpcService(typeInfo));
        }

        /// <summary>
        /// 验证类型是否为有效的动态 gRPC 服务
        /// </summary>
        private static bool IsValidDynamicGrpcService(TypeInfo typeInfo)
        {
            // 基本条件检查：必须实现 IDynamicApi，必须是公开的非抽象非泛型类
            if (!IsDynamicGrpcType(typeInfo))
            {
                return false;
            }

            // 不能是 IFallback 类型（熔断降级类）
            if (typeof(IFallback).IsAssignableFrom(typeInfo.AsType()))
            {
                return false;
            }

            // 必须有 DynamicApiAttribute 标记
            var dynamicApiAttr = ReflectionHelper.GetSingleAttributeOrDefaultByFullSearch<DynamicApiAttribute>(typeInfo);
            if (dynamicApiAttr == null)
            {
                return false;
            }

            // 不能有 NonDynamicApiAttribute 排除标记
            if (ReflectionHelper.GetSingleAttributeOrDefaultByFullSearch<NonDynamicApiAttribute>(typeInfo) != null)
            {
                return false;
            }

            // 不能有 NonGrpcServiceAttribute 排除标记
            if (ReflectionHelper.GetSingleAttributeOrDefaultByFullSearch<Attributes.NonGrpcServiceAttribute>(typeInfo) != null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查类型是否符合动态 gRPC 的基本要求
        /// </summary>
        private static bool IsDynamicGrpcType(TypeInfo typeInfo)
        {
            return typeof(IDynamicApi).IsAssignableFrom(typeInfo.AsType()) &&
                   typeInfo.IsPublic &&
                   typeInfo.IsClass &&
                   !typeInfo.IsAbstract &&
                   !typeInfo.IsGenericType;
        }

        /// <summary>
        /// 获取服务名称（移除后缀）
        /// </summary>
        /// <param name="serviceName">原始服务名称</param>
        /// <returns>处理后的服务名称</returns>
        public string GetServiceName(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                return serviceName;
            }

            // 如果有自定义处理函数，优先使用
            if (_options.GetServiceName != null)
            {
                return _options.GetServiceName(serviceName);
            }

            foreach (var postfix in GetOrderedPostfixes(_options.RemoveServicePostfixes))
            {
                if (serviceName.EndsWith(postfix, StringComparison.OrdinalIgnoreCase))
                {
                    return serviceName.Substring(0, serviceName.Length - postfix.Length);
                }
            }

            return serviceName;
        }

        /// <summary>
        /// 获取方法名称（移除后缀）
        /// </summary>
        /// <param name="methodName">原始方法名称</param>
        /// <returns>处理后的方法名称</returns>
        public string GetMethodName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return methodName;
            }

            // 如果有自定义处理函数，优先使用
            if (_options.GetMethodName != null)
            {
                return _options.GetMethodName(methodName);
            }

            foreach (var postfix in GetOrderedPostfixes(_options.RemoveMethodPostfixes))
            {
                if (methodName.EndsWith(postfix, StringComparison.OrdinalIgnoreCase))
                {
                    return methodName.Substring(0, methodName.Length - postfix.Length);
                }
            }

            return methodName;
        }

        /// <summary>
        /// 清除类型缓存（用于测试或动态加载程序集场景）
        /// </summary>
        public static void ClearCache()
        {
            _serviceCache.Clear();
        }

        private static IEnumerable<string> GetOrderedPostfixes(IEnumerable<string> postfixes)
        {
            return postfixes
                .Where(postfix => !string.IsNullOrWhiteSpace(postfix))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(postfix => postfix.Length);
        }
    }
}
