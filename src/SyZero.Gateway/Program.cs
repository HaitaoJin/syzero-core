using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyZero.ApiGateway;
using System.Threading.Tasks;

namespace SyZero.Gateway
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddSyZero();
            builder.Configuration.AddJsonFile("configuration.json", optional: false, reloadOnChange: true);
            builder.WebHost.UseUrls($"{AppConfig.ServerOptions.Protocol}://*:{AppConfig.ServerOptions.Port}");

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            }).AddSyZeroLog4Net();

            builder.Services.AddSyZeroOpenTelemetry();
            builder.Services.AddSyZeroApiGateway(options =>
            {
                options.SwaggerTitle = AppConfig.GetSection("SyZero:Name") ?? "SyZero.Gateway";
            }, builder.Configuration);
            builder.Services.AddControllers();

            var app = builder.Build();
            var gatewayOptions = app.Services.GetRequiredService<SyZeroApiGatewayOptions>();

            app.UseSyZero();
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseCors(gatewayOptions.CorsPolicyName);
            app.UseStaticFiles();
            app.MapControllers();
            await app.UseSyZeroApiGatewayAsync();
            await app.RunAsync();
        }
    }
}
