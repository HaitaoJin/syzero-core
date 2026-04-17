using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SyZero.Swagger
{
    public class XmlCommentsMemberNameHelper
    {
        public static string GetMemberNameForMethod(MethodInfo method)
        {
            var builder = new StringBuilder("M:");

            builder.Append(GetQualifiedNameForXml(method.DeclaringType));
            builder.Append($".{method.Name}");

            var parameters = method.GetParameters();
            if (parameters.Any())
            {
                var parametersNames = parameters.Select(p =>
                {
                    return p.ParameterType.IsGenericParameter
                        ? $"`{p.ParameterType.GenericParameterPosition}"
                        : GetQualifiedNameForXml(p.ParameterType, expandGenericArgs: true);
                });
                builder.Append($"({string.Join(",", parametersNames)})");
            }

            return builder.ToString();
        }

        public static string GetMemberNameForInterfaceMethod(MethodInfo method)
        {
            var builder = new StringBuilder("M:");
            foreach (var interfaces in method.DeclaringType.GetInterfaces())
            {
                var tempMethod = interfaces.GetMembers().FirstOrDefault(p => p.MemberType == method.MemberType && p.ToString() == method.ToString());
                if (tempMethod != null) {
                   
                     method = (MethodInfo)tempMethod;
                     break;
                }
            }
          
            builder.Append(GetQualifiedNameForXml(method.DeclaringType));
            builder.Append($".{method.Name}");

            var parameters = method.GetParameters();
            if (parameters.Any())
            {
                var parametersNames = parameters.Select(p =>
                {
                    return p.ParameterType.IsGenericParameter
                        ? $"`{p.ParameterType.GenericParameterPosition}"
                        : GetQualifiedNameForXml(p.ParameterType, expandGenericArgs: true);
                });
                builder.Append($"({string.Join(",", parametersNames)})");
            }

            return builder.ToString();
        }

        public static string GetMemberNameForType(Type type)
        {
            var builder = new StringBuilder("T:");
            builder.Append(GetQualifiedNameForXml(type));

            return builder.ToString();
        }

        public static string GetMemberNameForMember(MemberInfo memberInfo)
        {
            var builder = new StringBuilder(((memberInfo.MemberType & MemberTypes.Field) != 0) ? "F:" : "P:");
            builder.Append(GetQualifiedNameForXml(memberInfo.DeclaringType));
            builder.Append($".{memberInfo.Name}");

            return builder.ToString();
        }

        public static string GetQualifiedNameForXml(Type type, bool expandGenericArgs = false)
        {
            if (type.IsArray)
                return $"{GetQualifiedNameForXml(type.GetElementType(), expandGenericArgs)}[]";

            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(type.Namespace))
                builder.Append($"{type.Namespace}.");

            if (type.IsNested)
                builder.Append($"{type.DeclaringType.Name}.");

            if (type.IsConstructedGenericType && expandGenericArgs)
            {
                var nameSansGenericArgs = type.Name.Split('`').First();
                builder.Append(nameSansGenericArgs);

                var genericArgsNames = type.GetGenericArguments().Select(t =>
                {
                    return t.IsGenericParameter
                        ? $"`{t.GenericParameterPosition}"
                        : GetQualifiedNameForXml(t, true);
                });

                builder.Append($"{{{string.Join(",", genericArgsNames)}}}");
            }
            else
            {
                builder.Append(type.Name);
            }

            return builder.ToString();
        }
    }
}
