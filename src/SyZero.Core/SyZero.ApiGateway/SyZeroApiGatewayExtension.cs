using CacheManager.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MMLib.SwaggerForOcelot.DependencyInjection;
using MMLib.SwaggerForOcelot.Middleware;
using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Polly;
using SyZero.ApiGateway;
using System;
using System.Threading.Tasks;

namespace SyZero
{
    public static class SyZeroApiGatewayExtension
    {
        public static IServiceCollection AddSyZeroApiGateway(this IServiceCollection services, IConfiguration configuration = null)
        {
            return AddSyZeroApiGateway(services, _ => { }, configuration);
        }

        public static IServiceCollection AddSyZeroApiGateway(
            this IServiceCollection services,
            Action<SyZeroApiGatewayOptions> optionsAction,
            IConfiguration configuration = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var config = configuration
                ?? throw new InvalidOperationException("Configuration is required before registering SyZero.ApiGateway.");

            var options = SyZeroApiGatewayOptions.CreateDefault(config);
            optionsAction?.Invoke(options);
            options.Validate();

            services.AddSingleton(options);
            services.AddHttpContextAccessor();
            services.AddCors(corsOptions =>
            {
                corsOptions.AddPolicy(options.CorsPolicyName, policy => ConfigureCors(policy, options));
            });

            var ocelotBuilder = services.AddOcelot(config);
            if (options.EnableConsul)
            {
                ocelotBuilder = options.UseConsulServiceAddress
                    ? ocelotBuilder.AddConsul<SyZeroConsulServiceBuilder>()
                    : ocelotBuilder.AddConsul();

                if (options.EnableConfigStoredInConsul)
                {
                    ocelotBuilder = ocelotBuilder.AddConfigStoredInConsul();
                }
            }

            if (options.EnableCacheManager)
            {
                ocelotBuilder = ocelotBuilder.AddCacheManager(settings => settings.WithDictionaryHandle());
            }

            if (options.EnablePolly)
            {
                ocelotBuilder = ocelotBuilder.AddPolly();
            }

            if (options.EnableSwagger)
            {
                services.AddSwaggerForOcelot(config);

                if (options.EnableSwaggerGen)
                {
                    services.AddSwaggerGen(swagger =>
                    {
                        swagger.SwaggerDoc(options.SwaggerDocumentName, new OpenApiInfo
                        {
                            Title = options.SwaggerTitle,
                            Version = options.SwaggerDocumentName
                        });
                    });
                }
            }

            return services;
        }

        public static async Task<IApplicationBuilder> UseSyZeroApiGatewayAsync(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var options = app.ApplicationServices.GetRequiredService<SyZeroApiGatewayOptions>();
            if (options.EnableSwagger)
            {
                app.UseSwaggerForOcelotUI(swagger =>
                {
                    swagger.PathToSwaggerGenerator = options.SwaggerGeneratorPath;
                });
            }

            app.UseWebSockets();
            await app.UseOcelot();

            return app;
        }

        private static void ConfigureCors(CorsPolicyBuilder policy, SyZeroApiGatewayOptions options)
        {
            if (options.AllowedOrigins.Length > 0)
            {
                policy.WithOrigins(options.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                return;
            }

            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    }
}
