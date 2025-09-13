using System.Collections.Generic;
using System.Threading.Tasks;

namespace RagCap.Core.Pipeline
{
    public interface IBuildPipeline
    {
        Task RunAsync(string inputPath, List<string> sourcesFromRecipe = null);
    }
}
