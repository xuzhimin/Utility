using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace Power.UrlToPDF.API
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API 配置和服务

            // Web API 路由
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "Index",
                routeTemplate: "{id}.html",
                defaults: new { id = "index" }
                );
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
            config.Routes.MapHttpRoute(
                name: "DefaultApi2",
                routeTemplate: "api/{controller}/{formid}/{keyvalue}",
                defaults: new { formid = RouteParameter.Optional,keyvalue=RouteParameter.Optional }
            );
        }
    }
}
