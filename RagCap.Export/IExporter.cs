
using RagCap.Core.Capsule;

namespace RagCap.Export;

public interface IExporter
{
    Task ExportAsync(string capsuleFilePath, string outputFilePath);
}
