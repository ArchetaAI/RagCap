
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RagCap.Core.Generation
{
    public class LocalAnswerGenerator : IAnswerGenerator
    {
        private readonly OllamaApiClient _ollama;

        public LocalAnswerGenerator(string uri, string model)
        {
            _ollama = new OllamaApiClient(new Uri(uri), model);
        }

        public async Task<string> GenerateAsync(string query, IEnumerable<string> context)
        {
#nullable disable
            var prompt = BuildPrompt(query, context);

            var responseBuilder = new StringBuilder();
            await foreach (var stream in _ollama!.GenerateAsync(prompt))
            {
                responseBuilder.Append(stream.Response);
            }

            return responseBuilder.ToString();
#nullable enable
        }

        private string BuildPrompt(string query, IEnumerable<string> context)
        {
            var contextString = string.Join("\n\n", context.Select(c => $"--- Context: ---\n{c}"));

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("You are a helpful assistant. Answer the user's query based on the provided context.");
            promptBuilder.AppendLine("Keep your answer concise and directly related to the query.");
            promptBuilder.AppendLine("\n---\n");
            promptBuilder.AppendLine(contextString);
            promptBuilder.AppendLine("\n---\n");
            promptBuilder.AppendLine($"Query: {query}");
            promptBuilder.AppendLine("\nAnswer:");

            return promptBuilder.ToString();
        }
    }
}
