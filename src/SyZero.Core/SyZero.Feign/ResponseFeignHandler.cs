using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.Feign
{
    public class ResponseFeignHandler : DelegatingHandler
    {
        public ResponseFeignHandler(string serverName, HttpMessageHandler innerHandler = null) : base(innerHandler ?? new HttpClientHandler())
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.Content == null)
            {
                return response;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return response;
            }

            try
            {
                using var jsonDocument = JsonDocument.Parse(jsonString);
                var root = jsonDocument.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var codeElement))
                {
                    return response;
                }

                if (codeElement.GetInt32() == (int)SyMessageBoxStatus.Success)
                {
                    if (root.TryGetProperty("data", out var dataElement))
                    {
                        response.Content = CreateJsonContent(dataElement.GetRawText());
                    }
                }
                else
                {
                    response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                }
            }
            catch (JsonException)
            {
                return response;
            }

            return response;
        }

        private static StringContent CreateJsonContent(string json)
        {
            return new StringContent(json, Encoding.UTF8, "application/json");
        }
    }
}
