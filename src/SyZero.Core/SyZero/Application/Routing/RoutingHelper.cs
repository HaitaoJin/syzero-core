using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using SyZero.Extension;

namespace SyZero.Application.Routing
{
    public class RoutingHelper
    {
        public const string ApiUrlPre = "/API";

        private readonly static List<string> RemoveControllerPostfixes = new List<string>() { "AppService", "ApplicationService" };

        public static System.Net.Http.HttpMethod GetHttpVerbV2(MemberInfo member)
        {
            MemberInfo method = member.GetInterfaceMemberInfo();

            // 如果未找到接口成员，回退到使用原始成员
            if (method == null)
            {
                method = member;
            }

            var apiMethod = method.GetCustomAttribute<HttpMethodAttribute>();

            return apiMethod?.Method ?? System.Net.Http.HttpMethod.Get;
        }

        public static string GetHttpTemplateV2(MemberInfo member)
        {
            MemberInfo method = member.GetInterfaceMemberInfo();

            // 如果未找到接口成员，回退到使用原始成员
            if (method == null)
            {
                method = member;
            }

            var apiMethod = method.GetCustomAttribute<HttpMethodAttribute>();

            return apiMethod?.Path.RemovePreFix(RoutingHelper.ApiUrlPre);
        }


        public static string GetApiPreFix()
        {
            return "api";
        }

        public static string GetcontrollerRouteUrl(string areaName, string controllerName)
        {
            var apiPreFix = GetApiPreFix();
            return $"{apiPreFix}/{areaName}/{GetControllerName(controllerName)}".Replace("//", "/");
        }

        public static string GetRouteUrl(string areaName, string controllerName, MemberInfo action)
        {
            var apiPreFix = GetApiPreFix();
            return $"{apiPreFix}/{areaName}/{GetControllerName(controllerName)}/{action.Name}".Replace("//", "/");
        }

        public static string GetRouteUrlByInterface(string areaName, MemberInfo action)
        {
            if (string.IsNullOrEmpty(areaName))
            {
                areaName = action.ReflectedType.Assembly.GetName().Name.Replace(".IApplication","");
            }
            string cname = GetControllerName(action.DeclaringType.FullName.Split('.')[action.DeclaringType.FullName.Split('.').Length - 1]);
            var apiPreFix = GetApiPreFix();

            var template = action.GetCustomAttribute<HttpMethodAttribute>().Path;

            return $"{apiPreFix}/{areaName}/{cname.Substring(1)}/{template ?? action.Name}".Replace("//", "/");
        }

        public static string GetRouteUrlByInterface(string endPoint,string areaName, MemberInfo action)
        {
            return $"{endPoint}/{GetRouteUrlByInterface(areaName, action)}";
        }

        public static string GetControllerName(string controllerName)
        {
            return controllerName.RemovePostFix(RemoveControllerPostfixes.ToArray());
        }
    }
}
