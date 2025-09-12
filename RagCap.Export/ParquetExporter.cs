using Parquet;
using Parquet.Serialization;
using RagCap.Core.Capsule;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RagCap.Export;

public class ParquetExporter : ExporterBase
{
    public override async Task ExportAsync(string capsuleFilePath, string outputFilePath)
    {
        var (chunks, embeddings) = await ReadCapsuleDataAsync(capsuleFilePath);

        var data = from c in chunks
                   join e in embeddings on c.Id.ToString() equals e.ChunkId
                   select new ParquetData
                   {
                       ChunkId = c.Id,
                       Content = c.Content,
                       Embedding = e.Vector
                   };

        using (Stream fileStream = File.OpenWrite(outputFilePath))
        {
            await ParquetSerializer.SerializeAsync(data.ToList(), fileStream);
        }
    }
}

public class ParquetData
{
    public long ChunkId { get; set; }
    public string? Content { get; set; }
    public float[]? Embedding { get; set; }
}