using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using SyZero.Application.Attributes;
using SyZero.Application.Routing;
using SyZero.Application.Service;
using SyZero.DynamicWebApi.Attributes;
using SyZero.DynamicWebApi.Helpers;
using SyZero.Extension;

namespace SyZero.DynamicWebApi
{
    /// <summary>
    /// 动态 WebApi 约定
    /// 用于配置控制器和 Action 的路由、HTTP 方法等
    /// </summary>
    public class DynamicWebApiConvention : IApplicationModelConvention
    {
        private readonly DynamicWebApiOptions _options;

        // 缓存属性查找结果，提升性能
        private static readonly ConcurrentDictionary<Type, DynamicApiAttribute> _DynamicWebApiAttrCache = new();
        private static readonly ConcurrentDictionary<MemberInfo, bool> _nonDynamicMethodCache = new();

        /// <summary>
        /// 使用默认选项初始化
        /// </summary>
        public DynamicWebApiConvention() : this(new DynamicWebApiOptions())
        {
        }

        /// <summary>
        /// 使用指定选项初始化
        /// </summary>
        /// <param name="options">Dynamic WebApi 选项</param>
        public DynamicWebApiConvention(DynamicWebApiOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 应用约定到应用程序模型
        /// </summary>
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                ConfigureController(controller);
            }
        }

        /// <summary>
        /// 配置控制器
        /// </summary>
        private void ConfigureController(ControllerModel controller)
        {
            var type = controller.ControllerType.AsType();
            var DynamicWebApiAttr = GetDynamicApiAttribute(type.GetTypeInfo());

            // 检查是否为动态 WebApi 类型
            var isDynamicWebApi = typeof(IDynamicApi).GetTypeInfo().IsAssignableFrom(type);

            if (isDynamicWebApi || DynamicWebApiAttr != null)
            {
                if (isDynamicWebApi)
                {
                    controller.ControllerName = GetControllerName(controller.ControllerName);
                }

                ConfigureArea(controller, DynamicWebApiAttr);
                ConfigureDynamicWebApi(controller, DynamicWebApiAttr);
            }
        }

        /// <summary>
        /// 获取控制器名称（移除后缀）
        /// </summary>
        private string GetControllerName(string controllerName)
        {
            if (string.IsNullOrEmpty(controllerName))
            {
                return controllerName;
            }

            foreach (var postfix in _options.RemoveControllerPostfixes)
            {
                if (controllerName.EndsWith(postfix, StringComparison.OrdinalIgnoreCase))
                {
                    return controllerName.Substring(0, controllerName.Length - postfix.Length);
                }
            }

            return controllerName;
        }

        /// <summary>
        /// 配置区域
        /// </summary>
        private void ConfigureArea(ControllerModel controller, DynamicApiAttribute attr)
        {
            if (attr == null)
            {
                throw new ArgumentNullException(nameof(attr));
            }

            if (controller.RouteValues.ContainsKey("area"))
            {
                return;
            }

            // 优先使用特性指定的模块名，其次使用默认区域名
            var areaName = !string.IsNullOrEmpty(attr.Module)
                ? attr.Module
                : _options.DefaultAreaName;

            if (!string.IsNullOrEmpty(areaName))
            {
                controller.RouteValues["area"] = areaName;
            }
        }

        private void ConfigureDynamicWebApi(ControllerModel controller, DynamicApiAttribute controllerAttr)
        {
            ConfigureApiExplorer(controller);
            ConfigureSelector(controller, controllerAttr);
            ConfigureParameters(controller);
        }


        /// <summary>
        /// 配置 Action 参数绑定
        /// </summary>
        private void ConfigureParameters(ControllerModel controller)
        {
            foreach (var action in controller.Actions)
            {
                if (IsNonMappedMethod(action))
                {
                    continue;
                }

                foreach (var parameter in action.Parameters)
                {
                    ConfigureParameter(action, parameter);
                }
            }
        }

        /// <summary>
        /// 配置单个参数
        /// </summary>
        private void ConfigureParameter(ActionModel action, ParameterModel parameter)
        {
            // 如果已有绑定信息，跳过
            if (parameter.BindingInfo != null)
            {
                return;
            }

            // 原始类型不需要 FromBody
            if (TypeHelper.IsPrimitiveExtendedIncludingNullable(parameter.ParameterInfo.ParameterType))
            {
                return;
            }

            // 检查是否可以使用 FromBody 绑定
            if (CanUseFormBodyBinding(action, parameter))
            {
                parameter.BindingInfo = BindingInfo.GetBindingInfo(new[] { new FromBodyAttribute() });
            }
        }


        /// <summary>
        /// 判断是否可以使用 FormBody 绑定
        /// </summary>
        private bool CanUseFormBodyBinding(ActionModel action, ParameterModel parameter)
        {
            // 检查是否为忽略类型
            var parameterType = parameter.ParameterInfo.ParameterType;
            if (_options.FormBodyBindingIgnoredTypes.Any(t => t.IsAssignableFrom(parameterType)))
            {
                return false;
            }

            // GET、DELETE、TRACE、HEAD 请求不支持 Body
            foreach (var selector in action.Selectors)
            {
                if (selector.ActionConstraints == null)
                {
                    continue;
                }

                foreach (var constraint in selector.ActionConstraints)
                {
                    if (constraint is HttpMethodActionConstraint httpConstraint &&
                        httpConstraint.HttpMethods.All(m => m.IsIn("GET", "DELETE", "TRACE", "HEAD")))
                    {
                        return false;
                    }
                }
            }

            return true;
        }


        #region Api Explorer 配置

        /// <summary>
        /// 配置 API Explorer（用于 Swagger 等文档生成）
        /// </summary>
        private void ConfigureApiExplorer(ControllerModel controller)
        {
            if (controller.ApiExplorer.GroupName.IsNullOrEmpty())
            {
                controller.ApiExplorer.GroupName = controller.ControllerName;
            }

            controller.ApiExplorer.IsVisible ??= true;

            foreach (var action in controller.Actions)
            {
                if (!IsNonMappedMethod(action))
                {
                    action.ApiExplorer.IsVisible ??= true;
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查是否为不映射的方法
        /// </summary>
        private bool IsNonMappedMethod(ActionModel action)
        {
            var memberInfo = action.ActionMethod.GetInterfaceMemberInfo();

            if (memberInfo == null)
            {
                memberInfo = action.ActionMethod;
            }

            return _nonDynamicMethodCache.GetOrAdd(memberInfo, mi =>
            {
                // 检查 NonDynamicMethodAttribute
                var noMapMethod = ReflectionHelper.GetSingleAttributeOrDefault<NonDynamicMethodAttribute>(mi);
                if (noMapMethod != null)
                {
                    action.ApiExplorer.IsVisible = false;
                    return true;
                }

                // 检查 NonWebApiMethodAttribute
                var noWebApiMethod = ReflectionHelper.GetSingleAttributeOrDefault<NonWebApiMethodAttribute>(mi);
                if (noWebApiMethod != null)
                {
                    action.ApiExplorer.IsVisible = false;
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// 获取 DynamicApiAttribute（带缓存）
        /// </summary>
        private static DynamicApiAttribute GetDynamicApiAttribute(TypeInfo typeInfo)
        {
            return _DynamicWebApiAttrCache.GetOrAdd(typeInfo.AsType(), _ =>
                ReflectionHelper.GetSingleAttributeOrDefaultByFullSearch<DynamicApiAttribute>(typeInfo));
        }

        /// <summary>
        /// 清除缓存（用于测试或动态加载程序集场景）
        /// </summary>
        public static void ClearCache()
        {
            _DynamicWebApiAttrCache.Clear();
            _nonDynamicMethodCache.Clear();
        }

        #endregion
        #region 路由选择器配置

        /// <summary>
        /// 配置路由选择器
        /// </summary>
        private void ConfigureSelector(ControllerModel controller, DynamicApiAttribute controllerAttr)
        {
            // 如果已有路由特性，跳过
            if (controller.Selectors.Any(s => s.AttributeRouteModel != null))
            {
                return;
            }

            var areaName = controllerAttr?.Module ?? _options.DefaultAreaName ?? string.Empty;
            var customApiName = controller.ControllerType.GetSingleAttributeOrDefaultByFullSearch<ApiAttribute>();
            var controllerName = customApiName?.Name ?? controller.ControllerName;

            // 设置控制器路由
            var controllerRoute = BuildControllerRoute(areaName, controllerName);
            controller.Selectors[0].AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(controllerRoute));

            // 配置每个 Action
            foreach (var action in controller.Actions)
            {
                if (!IsNonMappedMethod(action))
                {
                    ConfigureActionSelector(areaName, controllerName, action);
                }
            }
        }

        /// <summary>
        /// 构建控制器路由
        /// </summary>
        private string BuildControllerRoute(string areaName, string controllerName)
        {
            var apiPrefix = _options.DefaultApiPrefix ?? AppConsts.DefaultApiPrefix;
            var route = $"{apiPrefix}/{areaName}/{GetControllerName(controllerName)}".Replace("//", "/");
            return _options.EnableLowerCaseRoutes ? route.ToLowerInvariant() : route;
        }

        /// <summary>
        /// 配置 Action 选择器
        /// </summary>
        private void ConfigureActionSelector(string areaName, string controllerName, ActionModel action)
        {
            // 检查是否有 NonDynamicApiAttribute
            var nonAttr = ReflectionHelper.GetSingleAttributeOrDefault<NonDynamicApiAttribute>(action.ActionMethod);
            if (nonAttr != null)
            {
                return;
            }

            // 如果没有选择器或没有约束，添加默认选择器
            if (action.Selectors.IsNullOrEmpty() || action.Selectors.Any(s => s.ActionConstraints.IsNullOrEmpty()))
            {
                if (!IsNonMappedMethod(action))
                {
                    AddDefaultSelector(areaName, controllerName, action);
                }
            }
            else
            {
                NormalizeSelectorRoutes(areaName, controllerName, action);
            }
        }

        /// <summary>
        /// 添加默认选择器
        /// </summary>
        private static void AddDefaultSelector(string areaName, string controllerName, ActionModel action)
        {
            var template = RoutingHelper.GetHttpTemplateV2(action.ActionMethod);
            var verb = RoutingHelper.GetHttpVerbV2(action.ActionMethod);

            var selector = action.Selectors[0];

            selector.AttributeRouteModel ??= CreateActionRouteModel(areaName, controllerName, action);

            if (!string.IsNullOrEmpty(template))
            {
                selector.AttributeRouteModel.Template = template;
            }

            if (!selector.ActionConstraints.Any())
            {
                selector.ActionConstraints.Add(new HttpMethodActionConstraint(new[] { verb.ToString() }));
            }
        }

        /// <summary>
        /// 规范化选择器路由
        /// </summary>
        private static void NormalizeSelectorRoutes(string areaName, string controllerName, ActionModel action)
        {
            foreach (var selector in action.Selectors)
            {
                var actionRoute = CreateActionRouteModel(areaName, controllerName, action);

                selector.AttributeRouteModel = selector.AttributeRouteModel == null
                    ? actionRoute
                    : AttributeRouteModel.CombineAttributeRouteModel(actionRoute, selector.AttributeRouteModel);
            }
        }

        /// <summary>
        /// 创建 Action 路由模型
        /// </summary>
        private static AttributeRouteModel CreateActionRouteModel(string areaName, string controllerName, ActionModel action)
        {
            var template = RoutingHelper.GetHttpTemplateV2(action.ActionMethod);
            return new AttributeRouteModel(new RouteAttribute(template ?? action.ActionName));
        }

        #endregion
    }
}