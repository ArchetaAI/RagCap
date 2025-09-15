
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace RagCap.Core.Capsule;

public class Manifest
{
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ExportRecord> ExportHistory { get; set; } = new List<ExportRecord>();

    public static async Task<Manifest?> ReadAsync(string capsuleFilePath)
    {
        var directoryName = Path.GetDirectoryName(capsuleFilePath) ?? string.Empty;
        var manifestPath = Path.Combine(directoryName, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            // This is a fallback for older capsules that don't have a manifest file.
            // We can try to get the creation time from the file system.
            return new Manifest
            {
                Version = 1,
                CreatedAt = File.GetCreationTimeUtc(capsuleFilePath),
                ExportHistory = new List<ExportRecord>()
            };
        }

        var json = await File.ReadAllTextAsync(manifestPath);
        return JsonSerializer.Deserialize<Manifest>(json);
    }

    public async Task WriteAsync(string capsuleFilePath)
    {
        var directoryName = Path.GetDirectoryName(capsuleFilePath) ?? string.Empty;
        var manifestPath = Path.Combine(directoryName, "manifest.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        await File.WriteAllTextAsync(manifestPath, json);
    }
}
