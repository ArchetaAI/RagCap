
using System;

namespace RagCap.Core.Capsule;

public class ExportRecord
{
    public string Format { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
