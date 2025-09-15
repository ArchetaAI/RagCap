using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RagCap.Core.Embeddings
{
    public class ApiEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string? _model;
        private readonly string? _apiVersion;
        private readonly bool _isAzure;

        #region DTOs
        private class EmbeddingResponse { public DataItem[]? data { get; set; } }
        private class DataItem { public float[]? embedding { get; set; } }
        private class ApiErrorResponse { public ApiError? error { get; set; } }
        private class ApiError { public string? message { get; set; } public string? type { get; set; } }
        #endregion

        public ApiEmbeddingProvider(string apiKey, string? model = null, string? endpoint = null, string? apiVersion = null)
        {
            _httpClient = new HttpClient();
            _model = model;
            var effectiveEndpoint = endpoint ?? Environment.GetEnvironmentVariable("RAGCAP_AZURE_ENDPOINT");

            if (string.IsNullOrEmpty(effectiveEndpoint) || effectiveEndpoint.Contains("openai.com"))
            {
                // Standard OpenAI
                _isAzure = false;
                _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
            else
            {
                // Azure OpenAI
                _isAzure = true;
                _httpClient.BaseAddress = new Uri(effectiveEndpoint);
                _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
                _apiVersion = apiVersion ?? Environment.GetEnvironmentVariable("RAGCAP_AZURE_API_VERSION") ?? "2024-02-15-preview"; // Default Azure API version
            }
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            object requestBody;
            string requestUri;

            if (_isAzure)
            {
                requestUri = $"openai/deployments/{_model}/embeddings?api-version={_apiVersion}";
                requestBody = new { input = text };
            }
            else
            {
                requestUri = "embeddings";
                requestBody = new { input = text, model = _model ?? "text-embedding-3-small" };
            }

            var httpResponse = await _httpClient.PostAsJsonAsync(requestUri, requestBody, cancellationToken);

            return await HandleApiResponseAsync(httpResponse);
        }

        private async Task<float[]> HandleApiResponseAsync(HttpResponseMessage response)
        {
#nullable disable
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(errorContent);
                    throw new HttpRequestException($"API call to {response.RequestMessage.RequestUri} failed with status code {response.StatusCode}: {errorResponse?.error?.message ?? "Unknown error"}");
                }
                catch (JsonException)
                {
                    throw new HttpRequestException($"API call to {response.RequestMessage.RequestUri} failed with status code {response.StatusCode} and non-JSON response: {errorContent}");
                }
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<EmbeddingResponse>(jsonResponse);

            return result?.data?.FirstOrDefault()?.embedding ?? Array.Empty<float>();
#nullable enable
        }

        public string GetModelName()
        {
            if(!string.IsNullOrEmpty(_model)) return _model;
            if(_isAzure) return "unknown";
            return "text-embedding-3-small";
        }
    }
}