using System;
using System.Collections.Generic;

namespace SyZero.Swagger
{
    /// <summary>
    /// Swagger 配置选项
    /// </summary>
    public class SwaggerOptions
    {
        /// <summary>
        /// 配置节名称
        /// </summary>
        public const string SectionName = "Swagger";

        /// <summary>
        /// 是否启用 Swagger，默认为 true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 文档版本，默认为 "v1"
        /// </summary>
        public string Version { get; set; } = "v1";

        /// <summary>
        /// 文档标题，为 null 时使用 "{ServerName}接口文档"
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 文档描述，为 null 时使用 "RESTful API for {ServerName}"
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 联系人名称
        /// </summary>
        public string ContactName { get; set; }

        /// <summary>
        /// 联系人邮箱
        /// </summary>
        public string ContactEmail { get; set; }

        /// <summary>
        /// 联系人网址
        /// </summary>
        public string ContactUrl { get; set; }

        /// <summary>
        /// 许可证名称
        /// </summary>
        public string LicenseName { get; set; }

        /// <summary>
        /// 许可证网址
        /// </summary>
        public string LicenseUrl { get; set; }

        /// <summary>
        /// 服务条款网址
        /// </summary>
        public string TermsOfServiceUrl { get; set; }

        /// <summary>
        /// 是否启用 JWT 认证，默认为 true
        /// </summary>
        public bool EnableJwtAuth { get; set; } = true;

        /// <summary>
        /// JWT 认证描述
        /// </summary>
        public string JwtAuthDescription { get; set; } = "在下框中输入请求头中需要添加Jwt授权Authorization:Bearer Token";

        /// <summary>
        /// 是否加载 XML 注释文档，默认为 true
        /// </summary>
        public bool IncludeXmlComments { get; set; } = true;

        /// <summary>
        /// XML 注释文件路径列表，为空时自动扫描 BaseDirectory 下的所有 XML 文件
        /// </summary>
        public List<string> XmlCommentFiles { get; set; } = new List<string>();

        /// <summary>
        /// 路由前缀
        /// </summary>
        public string RoutePrefix { get; set; } = "swagger";

        /// <summary>
        /// 验证配置
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Version))
            {
                throw new ArgumentException("Version 不能为空", nameof(Version));
            }
        }
    }
}
