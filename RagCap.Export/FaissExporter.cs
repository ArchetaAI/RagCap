
using RagCap.Core.Capsule;
using System.IO;
using System.Threading.Tasks;

namespace RagCap.Export;

public class FaissExporter : ExporterBase
{
    public override async Task ExportAsync(string capsuleFilePath, string outputFilePath)
    {
        var (_, embeddings) = await ReadCapsuleDataAsync(capsuleFilePath);

        if (embeddings.Count == 0)
        {
            return;
        }

        var dimension = embeddings[0].Vector?.Length ?? 0;
        var count = embeddings.Count;

        using (var stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(dimension);
                writer.Write((long)count);

                foreach (var embedding in embeddings)
                {
                    if (embedding.Vector != null)
                    {
                        foreach (var value in embedding.Vector)
                        {
                            writer.Write(value);
                        }
                    }
                }
            }
        }
    }
}
