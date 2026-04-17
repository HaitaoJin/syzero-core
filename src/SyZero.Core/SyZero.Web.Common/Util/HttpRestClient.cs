using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Client;
using SyZero.Util;

namespace SyZero.Web.Common.Util
{
    /// <summary>
    /// HttpRestClient class
    /// </summary>
    public class HttpRestClient : IClient
    {
        public async Task<ResponseTemplate<T>> ExecuteAsync<T>(RequestTemplate requestTemplate, CancellationToken cancellationToken)
        {
            if (requestTemplate == null)
            {
                throw new ArgumentNullException(nameof(requestTemplate));
            }

            var client = SyZeroUtil.GetService<RestClient>();
            var method = GetMethod(requestTemplate);
            var requset = new RestRequest(requestTemplate.Url, method);
            AddHeaders(requset, requestTemplate.Headers);
            AddRequestBody(requset, requestTemplate, method);
            foreach (var item in requestTemplate.QueryValue ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                requset.AddQueryParameter(item.Key, item.Value);
            }
            var response = await client.ExecuteAsync(requset, cancellationToken);
            return GetResponseTemplate<T>(response);
        }


        private Method GetMethod(RequestTemplate requestTemplate) {
            Method method = Method.Get;
            if (requestTemplate.HttpMethod == HttpMethod.Post)
            {
                method = Method.Post;
            }
            else if (requestTemplate.HttpMethod == HttpMethod.Put)
            {
                method = Method.Put;
            }
            else if (requestTemplate.HttpMethod == HttpMethod.Delete)
            {
                method = Method.Delete;
            }
            else if (requestTemplate.HttpMethod == HttpMethod.Get)
            {
                method = Method.Get;
            }
            return method;
        }

        private ResponseTemplate<T> GetResponseTemplate<T>(RestResponse response)
        {
            var responseTemplate = new ResponseTemplate<T>();
            responseTemplate.HttpStatusCode = response.StatusCode;
            if (!response.IsSuccessful)
            {
                responseTemplate.Msg = response.ErrorMessage ?? response.Content;
                return responseTemplate;
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                return responseTemplate;
            }

            try
            {
                var payload = JObject.Parse(response.Content);
                responseTemplate.Code = (SyMessageBoxStatus?)(payload["code"]?.Value<int?>()) ?? default;
                if (responseTemplate.Code == SyMessageBoxStatus.Success)
                {
                    var dataToken = payload["data"];
                    if (dataToken == null || dataToken.Type == JTokenType.Null)
                    {
                        return responseTemplate;
                    }

                    if (typeof(T) == typeof(string))
                    {
                        var value = dataToken.Type == JTokenType.String
                            ? dataToken.Value<string>()
                            : dataToken.ToString(Formatting.None);
                        responseTemplate.Body = (T)(object)value;
                    }
                    else
                    {
                        responseTemplate.Body = JsonConvert.DeserializeObject<T>(dataToken.ToString(Formatting.None));
                    }
                }
                else
                {
                    responseTemplate.Msg = payload["msg"]?.Value<string>() ?? payload["message"]?.Value<string>() ?? response.Content;
                }
            }
            catch (JsonException)
            {
                responseTemplate.Msg = response.Content;
            }

            return responseTemplate;
        }

        private static void AddHeaders(RestRequest request, IDictionary<string, string> headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (var header in headers)
            {
                request.AddHeader(header.Key, header.Value);
            }
        }

        private static void AddRequestBody(RestRequest request, RequestTemplate requestTemplate, Method method)
        {
            if (method == Method.Get)
            {
                return;
            }

            if (requestTemplate.IsForm)
            {
                foreach (var item in requestTemplate.FormValue ?? Enumerable.Empty<KeyValuePair<string, string>>())
                {
                    request.AddParameter(item.Key, item.Value);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(requestTemplate.Body))
            {
                return;
            }

            request.AddStringBody(requestTemplate.Body, DataFormat.Json);
        }
    }
}
