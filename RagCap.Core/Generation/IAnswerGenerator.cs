
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagCap.Core.Generation
{
    /// <summary>
    /// Interface for answer generation from a query and context.
    /// </summary>
    public interface IAnswerGenerator
    {
        /// <summary>
        /// Generates an answer based on a query and a set of context documents.
        /// </summary>
        /// <param name="query">The user's query.</param>
        /// <param name="context">A collection of context strings (e.g., retrieved document chunks).</param>
        /// <returns>The generated answer as a string.</returns>
        Task<string> GenerateAsync(string query, IEnumerable<string> context);
    }
}
