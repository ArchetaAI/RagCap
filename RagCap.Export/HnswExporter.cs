
using System;
using System.Threading.Tasks;

namespace RagCap.Export;

public class HnswExporter : ExporterBase
{
    public override Task ExportAsync(string capsuleFilePath, string outputFilePath)
    {
        throw new NotImplementedException("HNSW export is not yet supported.");
    }
}
