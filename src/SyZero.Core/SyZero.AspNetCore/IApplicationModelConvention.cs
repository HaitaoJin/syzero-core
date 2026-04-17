using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using System;
using System.Linq;

namespace SyZero.AspNetCore
{
    /// <summary>
    /// 全局路由前缀配置
    /// </summary>
    public class RouteConvention : IApplicationModelConvention
    {
        /// <summary>
        /// 定义一个路由前缀变量
        /// </summary>
        private readonly AttributeRouteModel _centralPrefix;

        /// <summary>
        /// 调用时传入指定的路由前缀
        /// </summary>
        /// <param name="routeTemplateProvider"></param>
        public RouteConvention(IRouteTemplateProvider routeTemplateProvider)
        {
            ArgumentNullException.ThrowIfNull(routeTemplateProvider);
            _centralPrefix = new AttributeRouteModel(routeTemplateProvider);
        }

        //接口的Apply方法
        public void Apply(ApplicationModel application)
        {
            ArgumentNullException.ThrowIfNull(application);

            //遍历所有的 Controller
            foreach (var controller in application.Controllers)
            {
                var selectors = controller.Selectors.Where(selector => selector != null).ToList();
                if (!selectors.Any())
                {
                    continue;
                }

                foreach (var selectorModel in selectors)
                {
                    selectorModel.AttributeRouteModel = selectorModel.AttributeRouteModel == null
                        ? _centralPrefix
                        : AttributeRouteModel.CombineAttributeRouteModel(_centralPrefix, selectorModel.AttributeRouteModel);
                }
            }
        }
    }
}
