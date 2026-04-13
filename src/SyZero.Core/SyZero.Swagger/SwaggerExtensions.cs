
using SyZero.Cache;
using SyZero.Configurations;
using SyZero.Util;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Xml.XPath;
using SyZero.Swagger;
using SyZero;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Swagger 服务扩展方法
    /// </summary>
    public static class SwaggerExtensions
    {
        /// <summary>
        /// 添加 Swagger 到依赖注入容器（使用默认配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSwagger(this IServiceCollection services)
        {
            return AddSwagger(services, new SwaggerOptions());
        }

        /// <summary>
        /// 添加 Swagger 到依赖注入容器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="options">配置选项</param>
        /// <param name="swaggerGenAction">SwaggerGen 额外配置委托</param>
        /// <returns>服务集合</returns>
        /// <exception cref="ArgumentNullException">options 为 null 时抛出</exception>
        public static IServiceCollection AddSwagger(this IServiceCollection services, SwaggerOptions options, Action<SwaggerGenOptions> swaggerGenAction = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();

            if (!options.Enabled)
            {
                return services;
            }

            services.AddSwaggerGen(swaggerGenOptions =>
            {
                ConfigureSwaggerGen(swaggerGenOptions, options);
                swaggerGenAction?.Invoke(swaggerGenOptions);
            });

            return services;
        }

        /// <summary>
        /// 添加 Swagger 到依赖注入容器（仅配置 SwaggerGenOptions）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="swaggerGenAction">SwaggerGen 配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSwagger(this IServiceCollection services, Action<SwaggerGenOptions> swaggerGenAction)
        {
            return AddSwagger(services, new SwaggerOptions(), swaggerGenAction);
        }

        /// <summary>
        /// 添加 Swagger 到依赖注入容器（从配置读取）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称，默认为 "Swagger"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSwagger(this IServiceCollection services, IConfiguration configuration, string sectionName = SwaggerOptions.SectionName)
        {
            var config = configuration ?? AppConfig.Configuration;
            var options = new SwaggerOptions();
            config.GetSection(sectionName).Bind(options);
            return AddSwagger(services, options);
        }

        /// <summary>
        /// 添加 Swagger 到依赖注入容器（从配置读取，并支持额外配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="optionsAction">额外配置委托（在配置文件配置之后执行）</param>
        /// <param name="configuration">配置，为 null 时使用 AppConfig.Configuration</param>
        /// <param name="sectionName">配置节名称，默认为 "Swagger"</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSwagger(this IServiceCollection services, Action<SwaggerOptions> optionsAction, IConfiguration configuration = null, string sectionName = SwaggerOptions.SectionName)
        {
            var config = configuration ?? AppConfig.Configuration;
            var options = new SwaggerOptions();
            config.GetSection(sectionName).Bind(options);
            optionsAction?.Invoke(options);
            return AddSwagger(services, options);
        }

        /// <summary>
        /// 配置 SwaggerGen
        /// </summary>
        private static void ConfigureSwaggerGen(SwaggerGenOptions swaggerGenOptions, SwaggerOptions options)
        {
            var serverName = AppConfig.ServerOptions?.Name ?? "API";
            var title = string.IsNullOrWhiteSpace(options.Title) ? $"{serverName}接口文档" : options.Title;
            var description = string.IsNullOrWhiteSpace(options.Description) ? $"RESTful API for {serverName}" : options.Description;

            var openApiInfo = new OpenApiInfo
            {
                Version = options.Version,
                Title = title,
                Description = description
            };

            // 配置联系人信息
            if (!string.IsNullOrWhiteSpace(options.ContactName) ||
                !string.IsNullOrWhiteSpace(options.ContactEmail) ||
                !string.IsNullOrWhiteSpace(options.ContactUrl))
            {
                openApiInfo.Contact = new OpenApiContact
                {
                    Name = options.ContactName,
                    Email = options.ContactEmail,
                    Url = string.IsNullOrWhiteSpace(options.ContactUrl) ? null : new Uri(options.ContactUrl)
                };
            }

            // 配置许可证信息
            if (!string.IsNullOrWhiteSpace(options.LicenseName))
            {
                openApiInfo.License = new OpenApiLicense
                {
                    Name = options.LicenseName,
                    Url = string.IsNullOrWhiteSpace(options.LicenseUrl) ? null : new Uri(options.LicenseUrl)
                };
            }

            // 配置服务条款
            if (!string.IsNullOrWhiteSpace(options.TermsOfServiceUrl))
            {
                openApiInfo.TermsOfService = new Uri(options.TermsOfServiceUrl);
            }

            swaggerGenOptions.SwaggerDoc(options.Version, openApiInfo);
            swaggerGenOptions.DocInclusionPredicate((docName, description) => true);

            // 配置 JWT 认证
            if (options.EnableJwtAuth)
            {
                ConfigureJwtAuth(swaggerGenOptions, options);
            }

            // 配置 XML 注释
            if (options.IncludeXmlComments)
            {
                ConfigureXmlComments(swaggerGenOptions, options);
            }
        }

        /// <summary>
        /// 配置 JWT 认证
        /// </summary>
        private static void ConfigureJwtAuth(SwaggerGenOptions swaggerGenOptions, SwaggerOptions options)
        {
            swaggerGenOptions.AddSecurityDefinition("Authorization", new OpenApiSecurityScheme()
            {
                Description = options.JwtAuthDescription,
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });

            swaggerGenOptions.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Authorization"
                        }
                    },
                    new string[] { }
                }
            });
        }

        /// <summary>
        /// 配置 XML 注释
        /// </summary>
        private static void ConfigureXmlComments(SwaggerGenOptions swaggerGenOptions, SwaggerOptions options)
        {
            if (options.XmlCommentFiles != null && options.XmlCommentFiles.Count > 0)
            {
                // 使用指定的 XML 文件
                foreach (var xmlFile in options.XmlCommentFiles)
                {
                    var xmlPath = Path.IsPathRooted(xmlFile)
                        ? xmlFile
                        : Path.Combine(AppContext.BaseDirectory, xmlFile);

                    if (File.Exists(xmlPath))
                    {
                        swaggerGenOptions.OperationFilter<XmlCommentsOperation2Filter>(new XPathDocument(xmlPath));
                    }
                }
            }
            else
            {
                // 自动扫描所有 XML 文件
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                foreach (FileInfo file in dir.EnumerateFiles("*.xml"))
                {
                    swaggerGenOptions.OperationFilter<XmlCommentsOperation2Filter>(new XPathDocument(file.FullName));
                }
            }
        }
    }
}
