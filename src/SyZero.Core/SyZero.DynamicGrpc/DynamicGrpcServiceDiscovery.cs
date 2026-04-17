using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SyZero.DynamicGrpc
{
    /// <summary>
    /// Dynamic gRPC 服务发现辅助类
    /// </summary>
    internal static class DynamicGrpcServiceDiscovery
    {
        /// <summary>
        /// 获取所有可注册的 Dynamic gRPC 服务类型
        /// </summary>
        public static IReadOnlyList<Type> GetServiceTypes(DynamicGrpcOptions options, DynamicGrpcServiceTypeProvider typeProvider = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            typeProvider ??= new DynamicGrpcServiceTypeProvider(options);

            return GetCandidateAssemblies(options)
                .SelectMany(GetLoadableTypes)
                .Where(type => type != null)
                .Select(type => type.GetTypeInfo())
                .Where(typeProvider.IsGrpcService)
                .Select(typeInfo => typeInfo.AsType())
                .Distinct()
                .ToArray();
        }

        private static IEnumerable<Assembly> GetCandidateAssemblies(DynamicGrpcOptions options)
        {
            if (options.AssemblyOptions.Count > 0)
            {
                return options.AssemblyOptions.Keys;
            }

            return Helpers.ReflectionHelper.GetAssemblies();
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.OfType<Type>();
            }
        }
    }
}
