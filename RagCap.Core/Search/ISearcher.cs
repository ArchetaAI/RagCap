
using RagCap.Core.Pipeline;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagCap.Core.Search
{
    public interface ISearcher
    {
        Task<IEnumerable<SearchResult>> SearchAsync(string query, int topK);
    }
}
