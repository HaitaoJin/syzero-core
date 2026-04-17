using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SyZero.Util;

namespace SyZero.Web.Common
{
    public class RestHelper
    {
        /// <summary>
        /// 通过传入的请求信息访问服务端，并返回结果对象
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="request">外部设定的请求</param>
        /// <returns>返回Jobject通用对象</returns>
        public static JObject Execute(RestRequest request)
        {
            var client = SyZeroUtil.GetService<RestClient>();
            var response = client.Execute(request);
            if (response.ErrorException != null)
            {
                return null;
            }

            return ParseObjectResponse(response);
        }

        /// <summary>
        /// 通过传入的请求信息访问服务端，并返回结果对象
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="request">外部设定的请求</param>
        /// <returns>返回Jobject通用对象</returns>
        public static async Task<JObject> ExecuteAsync(RestRequest request)
        {
            var client = SyZeroUtil.GetService<RestClient>();
            var response = await client.ExecuteAsync(request);
            if (response.ErrorException != null)
            {
                return null;
            }

            return ParseObjectResponse(response);
        }

        /// <summary>
        /// 通过传入的请求信息访问服务端，并返回T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="baseUrl"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public static T Execute<T>(RestRequest request) where T : class, new()
        {
            var client = SyZeroUtil.GetService<RestClient>();
            var response = client.Execute(request);
            if (response.ErrorException != null)
            {
                return null;
            }

            return DeserializeResponse<T>(response.Content);
        }

        /// <summary>
        /// 通过传入的请求信息访问服务端，并返回T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="baseUrl"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<T> ExecuteAsync<T>(RestRequest request) where T : class, new()
        {
            var client = SyZeroUtil.GetService<RestClient>();
            var response = await client.ExecuteAsync(request);
            if (response.ErrorException != null)
            {
                return null;
            }

            return DeserializeResponse<T>(response.Content);
        }

        public static string ExecuteToString(RestRequest request)
        {
            var client = SyZeroUtil.GetService<RestClient>();
            var response = client.Execute(request);
            if (response.ErrorException != null)
            {
                return string.Empty;
            }

            return response.Content ?? string.Empty;
        }

        /// <summary>
        /// PostJson数据 无返回值
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="resource"></param>
        /// <param name="postData"></param>
        public static string PostJson(string url, object body)
        {
            var client = SyZeroUtil.GetService<RestClient>();
            var request = new RestRequest(url, Method.Post);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("Content-Type", "application/json");
            AddRequestBody(request, body);

            var response = client.Execute(request);
            return response.Content ?? string.Empty;
        }

        /// <summary>
        /// PostJson数据，返回值T
        /// </summary>
        /// <returns></returns>
        public static HttpResultMessage<T> PostJson<T>(string url, object body, string token = "")
        {
            var client = SyZeroUtil.GetService<RestClient>();
            var request = new RestRequest(url, Method.Post);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", token);
            AddRequestBody(request, body);

            RestResponse response = client.Execute(request);
            var result = new HttpResultMessage<T>
            {
                IsSucceed = response.IsSuccessful,
                StatusCode = (int)response.StatusCode
            };
            if (result.IsSucceed)
            {
                var content = response.Content;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (typeof(T) == typeof(string))
                    {
                        result.Entity = (T)Convert.ChangeType(content, typeof(T));
                    }
                    else
                    {
                        result.Entity = JsonConvert.DeserializeObject<T>(content);
                    }
                }
            }
            else
            {
                result.Message = response.ErrorMessage ?? response.Content;
            }

            return result;
        }

        public static HttpResultMessage PostJsonAsUrl(string url, object body)
        {
            var client = SyZeroUtil.GetService<RestClient>();
            var request = new RestRequest(url, Method.Post);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("Content-Type", "application/json");
            AddRequestBody(request, body);

            RestResponse response = client.Execute(request);
            if (string.IsNullOrWhiteSpace(response.Content))
            {
                return HttpResultMessage.Create(response.IsSuccessful, response.Content ?? string.Empty, response.ErrorMessage ?? string.Empty);
            }

            return JsonConvert.DeserializeObject<HttpResultMessage>(response.Content);
        }


        public static T EasemobReqUrl<T>(string url, Method method, object body, string token = "")
        {
            try
            {
                var client = SyZeroUtil.GetService<RestClient>();
                var request = new RestRequest(url, method);
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(token) && token.Length > 1) { request.AddHeader("Authorization", "Bearer " + token); }
                if (method != Method.Get)
                {
                    AddRequestBody(request, body);
                }

                RestResponse response = client.Execute(request);
                var resultStr = response.Content;
                if (typeof(T) == typeof(string))
                {
                    return (T)Convert.ChangeType(resultStr, typeof(T));
                }

                return string.IsNullOrWhiteSpace(resultStr) ? default : JsonConvert.DeserializeObject<T>(resultStr);
            }
            catch
            {
                return default;
            }
        }


        public static T WechatPost<T>(string url, object body, string token = "")
        {
            try
            {
                var client = SyZeroUtil.GetService<RestClient>();
                var request = new RestRequest(url, Method.Post);
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(token) && token.Length > 1) { request.AddHeader("Authorization", "Bearer " + token); }
                AddRequestBody(request, body);

                RestResponse response = client.Execute(request);
                var resultStr = response.Content;
                if (typeof(T) == typeof(string))
                {
                    return (T)Convert.ChangeType(resultStr, typeof(T));
                }

                return string.IsNullOrWhiteSpace(resultStr) ? default : JsonConvert.DeserializeObject<T>(resultStr);
            }
            catch
            {
                return default;
            }
        }

        public static async Task<T> WechatGet<T>(string url, string token = "")
        {
            try
            {
                var client = SyZeroUtil.GetService<RestClient>();
                var request = new RestRequest(url, Method.Get);
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(token) && token.Length > 1) { request.AddHeader("Authorization", token); }

                RestResponse response = await client.ExecuteAsync(request);
                var resultStr = response.Content;
                if (typeof(T) == typeof(string))
                {
                    return (T)Convert.ChangeType(resultStr, typeof(T));
                }

                return string.IsNullOrWhiteSpace(resultStr) ? default : JsonConvert.DeserializeObject<T>(resultStr);
            }
            catch
            {
                return default;
            }
        }
        public async static Task<string> RestPost(string url, Dictionary<string, string> header = null, Dictionary<string, string> parameter = null, string body = "")
        {
            return await Request(url, header, parameter, body, Method.Post);
        }
        public async static Task<string> RestGet(string url, Dictionary<string, string> header = null, Dictionary<string, string> parameter = null)
        {
            return await Request(url, header, parameter, string.Empty, Method.Get);
        }
        private async static Task<string> Request(string url, Dictionary<string, string> header, Dictionary<string, string> parameter, string body, Method method)
        {
            RestClient client = new RestClient();
            string str;
            try
            {
                RestRequest request = new RestRequest(new Uri(url), method)
                {
                    RequestFormat = DataFormat.Json
                };
                if (header != null)
                {
                    foreach (var item in header.Keys)
                    {
                        request.AddHeader(item, header[item]);
                    }
                }
                if (parameter != null)
                {
                    foreach (var item in parameter.Keys)
                    {
                        request.AddParameter(item, parameter[item]);
                    }
                }
                if (!string.IsNullOrEmpty(body))
                {
                    AddRequestBody(request, body);
                }
                var response = await client.ExecuteAsync(request);
                str = response.Content;
                if (string.IsNullOrEmpty(str))
                {
                    str = response.ErrorMessage;
                }
            }
            catch
            {
                throw;
            }
            return str;
        }

        private static JObject ParseObjectResponse(RestResponse response)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.Content) || IsHtmlResponse(response))
            {
                return null;
            }

            try
            {
                return JObject.Parse(response.Content);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static T DeserializeResponse<T>(string content) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(content);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static bool IsHtmlResponse(RestResponse response)
        {
            if (!string.IsNullOrWhiteSpace(response.ContentType) &&
                response.ContentType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(response.Content) &&
                   response.Content.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddRequestBody(RestRequest request, object body)
        {
            if (body == null)
            {
                return;
            }

            if (body is string bodyText)
            {
                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    request.AddStringBody(bodyText, DataFormat.Json);
                }

                return;
            }

            request.AddJsonBody(body);
        }
    }
}
