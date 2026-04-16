#if NET9_0_OR_GREATER
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Swagger 导出扩展方法。
    /// </summary>
    public static class SwaggerExportExtensions
    {
        private const string SwaggerOutputOptionName = "--swagger-output";

        /// <summary>
        /// 注册 Swagger 中间件；若当前进程以 Swagger 导出模式启动，则导出后直接退出。
        /// </summary>
        public static WebApplication UseSwagger(this WebApplication app, Action<Swashbuckle.AspNetCore.Swagger.SwaggerOptions> setupAction = null, string documentName = "v1")
        {
            if (app.TryExportSwaggerAsync(GetApplicationArguments(), documentName).GetAwaiter().GetResult())
            {
                Environment.Exit(0);
            }

            SwaggerBuilderExtensions.UseSwagger(app, setupAction);
            return app;
        }

        /// <summary>
        /// 检查命令行参数，若包含 Swagger 输出参数则导出文档并返回 true。
        /// </summary>
        public static async Task<bool> TryExportSwaggerAsync(this WebApplication app, string[] args, string documentName = "v1")
        {
            if (!TryGetSwaggerOutputPath(args, out var swaggerOutputPath))
            {
                return false;
            }

            await ExportSwaggerAsync(app, swaggerOutputPath, documentName);
            return true;
        }

        /// <summary>
        /// 从命令行参数中解析 Swagger 输出路径。
        /// </summary>
        public static bool TryGetSwaggerOutputPath(string[] args, out string swaggerOutputPath)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (string.Equals(arg, SwaggerOutputOptionName, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        throw new InvalidOperationException($"{SwaggerOutputOptionName} requires a file path.");
                    }

                    swaggerOutputPath = args[i + 1];
                    return true;
                }

                if (arg.StartsWith($"{SwaggerOutputOptionName}=", StringComparison.OrdinalIgnoreCase))
                {
                    swaggerOutputPath = arg[(SwaggerOutputOptionName.Length + 1)..];
                    return true;
                }
            }

            swaggerOutputPath = string.Empty;
            return false;
        }

        /// <summary>
        /// 导出 Swagger JSON 到指定路径。
        /// </summary>
        public static async Task ExportSwaggerAsync(this WebApplication app, string swaggerOutputPath, string documentName = "v1")
        {
            var resolvedOutputPath = Path.GetFullPath(swaggerOutputPath, app.Environment.ContentRootPath);
            var outputDirectory = Path.GetDirectoryName(resolvedOutputPath);

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
            var swaggerDocument = swaggerProvider.GetSwagger(documentName);

            await using var outputStream = File.Create(resolvedOutputPath);
            using var outputWriter = new StreamWriter(outputStream);
            var jsonWriter = new OpenApiJsonWriter(outputWriter);

            swaggerDocument.SerializeAsV3(jsonWriter);
            await outputWriter.FlushAsync();
        }

        private static string[] GetApplicationArguments()
        {
            return Environment.GetCommandLineArgs().Skip(1).ToArray();
        }
    }
}
#endif
