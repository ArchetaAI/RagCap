using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace RagCap.Core.Generation
{
    public class ApiAnswerGenerator : IAnswerGenerator
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly string? _apiVersion;
        private readonly bool _isAzure;

        public ApiAnswerGenerator(HttpClient httpClient, string apiKey, string model, string? endpoint = null, string? apiVersion = null)
        {
            _httpClient = httpClient;
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

        public async Task<string> GenerateAsync(string query, IEnumerable<string> context)
        {
            var messages = new List<object>
            {
                new { role = "system", content = "You are a helpful assistant. Answer the user's query based on the provided context. Keep your answer concise and directly related to the query." }
            };
            messages.AddRange(context.Select(c => new { role = "user", content = string.Format("---\n{0}", c) }));
            messages.Add(new { role = "user", content = string.Format("---\n{0}", query) });

            object payload;
            string requestUri;

            if (_isAzure)
            {
                requestUri = $"openai/deployments/{_model}/chat/completions?api-version={_apiVersion}";
                payload = new
                {
                    messages = messages,
                    temperature = 0.7,
                    max_tokens = 1024
                };
            }
            else
            {
                requestUri = "chat/completions"; // Standard OpenAI chat completions endpoint
                payload = new
                {
                    model = _model,
                    messages = messages,
                    temperature = 0.7,
                    max_tokens = 1024
                };
            }

            var httpResponse = await _httpClient.PostAsJsonAsync(requestUri, payload);
            httpResponse.EnsureSuccessStatusCode();

            using var jsonDocument = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync());

            if (jsonDocument.RootElement.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }

            if (jsonDocument.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var errorMessage))
            {
                throw new InvalidOperationException($"API Error: {errorMessage.GetString()}");
            }

            throw new InvalidOperationException("Unrecognized API response.");
        }
    }
}