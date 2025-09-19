# Third-Party Licenses

This document summarizes third‑party components used by RagCap and their licenses. It is provided for convenience only — please refer to each upstream project for the authoritative license text.

If a packaged binary contains third‑party code, the corresponding license files are distributed alongside the package where required.

## Models and Native Extensions

- Local embedding model: `all-MiniLM-L6-v2`
  - License: Apache License 2.0 (per upstream project)
  - Notes: The ONNX model and vocab distributed under `models/all-MiniLM-L6-v2/` are provided for local embedding. See the upstream repository for details, notices, and authorship.

- sqlite-vec (vector extension for SQLite)
  - License: Apache License 2.0 (per upstream project)
  - Notes: This is an optional native dependency used when building/querying sqlite-vec indices.

## NuGet Dependencies (RagCap.Core)

- HtmlAgilityPack — MIT
- LanguageDetection.Ai — See upstream package license on NuGet
- Markdig — BSD 2‑Clause
- Microsoft.Data.Sqlite.Core — MIT
- Microsoft.ML.OnnxRuntime — MIT
- OllamaSharp — MIT (per upstream)
- UglyToad.PdfPig — Apache 2.0
- Spectre.Console — MIT
- YamlDotNet — MIT

## NuGet Dependencies (RagCap.Export)

- Microsoft.Data.Sqlite — MIT
- Parquet.Net — Apache 2.0

## NuGet Dependencies (RagCap.CLI)

- Microsoft.Extensions.DependencyInjection — MIT
- Microsoft.Extensions.Hosting — MIT
- SQLitePCLRaw.bundle_e_sqlite3 — MIT
- System.CommandLine — MIT
- Spectre.Console.Cli — MIT
- YamlDotNet — MIT

## Additional Notes

- ONNX Runtime native binaries may be present via the `Microsoft.ML.OnnxRuntime` package; those binaries are under MIT and include their own notices.
- SQLite native binaries for bundled e_sqlite3 are provided via `SQLitePCLRaw.bundle_e_sqlite3` under MIT.
- This file may not be exhaustive. If you believe an attribution is missing or inaccurate, please open an issue or contact the maintainers.
