using Newtonsoft.Json;
using Refit;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using SyZero.Client;
using SyZero.Serialization;
using SyZero.Web.Common;

namespace SyZero.Feign.Proxy
{
    /// <summary>
    /// HTTP 协议代理工厂
    /// </summary>
    public class HttpProxyFactory : IFeignProxyFactory
    {
        /// <summary>
        /// 支持的协议类型
        /// </summary>
        public FeignProtocol Protocol => FeignProtocol.Http;

        /// <summary>
        /// 创建 HTTP 代理实例
        /// </summary>
        public object CreateProxy(Type targetType, string endPoint, FeignService feignService, IJsonSerialize jsonSerialize)
        {
            try
            {
                var httpClient = CreateHttpClient(endPoint, feignService);
                var settings = CreateRefitSettings(jsonSerialize);
                return RestService.For(targetType, httpClient, settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP 代理创建失败: EndPoint({endPoint}) {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建 HttpClient
        /// </summary>
        private HttpClient CreateHttpClient(string endPoint, FeignService feignService)
        {
            var handler = CreateHttpMessageHandler(feignService);
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(endPoint),
                Timeout = TimeSpan.FromSeconds(feignService.Timeout)
            };
            return httpClient;
        }

        /// <summary>
        /// 创建 Refit 设置
        /// </summary>
        private RefitSettings CreateRefitSettings(IJsonSerialize jsonSerialize)
        {
            return new RefitSettings
            {
                DeserializationExceptionFactory = (httpResponse, exception) =>
                {
                    Console.WriteLine($"Feign 反序列化错误: {exception.Message}");
                    return Task.FromResult<Exception>(new InvalidOperationException("Feign 响应反序列化失败。", exception));
                },
                ContentSerializer = new NewtonsoftJsonContentSerializer(
                    new JsonSerializerSettings
                    {
                        Converters = { new LongToStrConverter() }
                    }
                ),
                ExceptionFactory = async (httpResponse) =>
                {
                    var jsonString = httpResponse.Content == null
                        ? string.Empty
                        : await httpResponse.Content.ReadAsStringAsync();

                    if (httpResponse.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    {
                        var data = jsonSerialize.JSONToObject<SyMessageBoxModel>(jsonString);
                        if (data != null)
                        {
                            return new SyMessageException(data);
                        }
                    }

                    return new HttpRequestException(
                        $"Feign 请求失败，状态码: {(int)httpResponse.StatusCode} ({httpResponse.StatusCode})。",
                        null,
                        httpResponse.StatusCode);
                }
            };
        }

        /// <summary>
        /// 创建 HTTP 消息处理器链
        /// </summary>
        private HttpMessageHandler CreateHttpMessageHandler(FeignService feignService)
        {
            var responseHandler = new ResponseFeignHandler(feignService.ServiceName)
            {
                InnerHandler = new HttpClientHandler()
            };
            var authenticationHandler = new AuthenticationFeignHandler(feignService.ServiceName, responseHandler);
            var requestHandler = new RequestFeignHandler(feignService.ServiceName, authenticationHandler);
            return requestHandler;
        }
    }
}
