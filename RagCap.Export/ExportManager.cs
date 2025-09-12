
using RagCap.Core.Capsule;
using System;
using System.Threading.Tasks;

namespace RagCap.Export;

public class ExportManager
{
    public async Task ExportAsync(string capsuleFilePath, string outputFilePath, string format)
    {
        IExporter exporter = format.ToLowerInvariant() switch
        {
            "parquet" => new ParquetExporter(),
            "faiss" => new FaissExporter(),
            "hnsw" => new HnswExporter(),
            _ => throw new ArgumentException($"Unsupported export format: {format}", nameof(format)),
        };

        await exporter.ExportAsync(capsuleFilePath, outputFilePath);

        await UpdateManifestAsync(capsuleFilePath, outputFilePath, format);
    }

    private async Task UpdateManifestAsync(string capsuleFilePath, string outputFilePath, string format)
    {
        var manifest = await Manifest.ReadAsync(capsuleFilePath);
        manifest.ExportHistory ??= new List<ExportRecord>();
        manifest.ExportHistory.Add(new ExportRecord
        {
            Format = format,
            FilePath = outputFilePath,
            Timestamp = DateTime.UtcNow
        });
        await manifest.WriteAsync(capsuleFilePath);
    }
}
