
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RagCap.Core.Embeddings
{
    public class ApiEmbeddingProvider : IEmbeddingProvider
    {
        // TODO: Reuse HttpClient to avoid socket exhaustion in high-load scenarios.
        private readonly HttpClient _httpClient;
        private readonly string _apiProvider;
        private readonly string _apiKey;

        #region DTOs
        private class EmbeddingResponse { public DataItem[] data { get; set; } }
        private class DataItem { public float[] embedding { get; set; } }
        private class ApiErrorResponse { public ApiError error { get; set; } }
        private class ApiError { public string message { get; set; } public string type { get; set; } }
        #endregion

        public ApiEmbeddingProvider(string apiProvider, string apiKey)
        {
            _apiProvider = apiProvider?.ToLower() ?? throw new ArgumentNullException(nameof(apiProvider));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = new HttpClient();
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return _apiProvider switch
            {
                "openai" => await GenerateOpenAiEmbeddingAsync(text, cancellationToken),
                "azure" => await GenerateAzureOpenAiEmbeddingAsync(text, cancellationToken),
                _ => throw new NotSupportedException($"API provider '{_apiProvider}' is not supported."),
            };
        }

        private async Task<float[]> GenerateOpenAiEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            var requestBody = new { input = text, model = GetModelName() };
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            return await HandleApiResponseAsync(response);
        }

        private async Task<float[]> GenerateAzureOpenAiEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            var endpoint = Environment.GetEnvironmentVariable("RAGCAP_AZURE_ENDPOINT");
            var deployment = Environment.GetEnvironmentVariable("RAGCAP_AZURE_DEPLOYMENT");
            var apiVersion = Environment.GetEnvironmentVariable("RAGCAP_AZURE_API_VERSION") ?? "2023-05-15";

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deployment))
            {
                throw new InvalidOperationException("RAGCAP_AZURE_ENDPOINT and RAGCAP_AZURE_DEPLOYMENT environment variables must be set for the Azure provider.");
            }

            var requestUrl = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/embeddings?api-version={apiVersion}";
            var requestBody = new { input = text };

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("api-key", _apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            return await HandleApiResponseAsync(response);
        }

        private async Task<float[]> HandleApiResponseAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(errorContent);
                    throw new HttpRequestException($"API call to {response.RequestMessage.RequestUri} failed with status code {response.StatusCode}: {errorResponse?.error?.message}");
                }
                catch (JsonException)
                {
                    throw new HttpRequestException($"API call to {response.RequestMessage.RequestUri} failed with status code {response.StatusCode} and non-JSON response: {errorContent}");
                }
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<EmbeddingResponse>(jsonResponse);

            return result?.data?.FirstOrDefault()?.embedding;
        }

        public string GetModelName()
        {
            return _apiProvider switch
            {
                "openai" => "text-embedding-3-small",
                "azure" => Environment.GetEnvironmentVariable("RAGCAP_AZURE_DEPLOYMENT") ?? "unknown",
                _ => "unknown"
            };
        }
    }
}
