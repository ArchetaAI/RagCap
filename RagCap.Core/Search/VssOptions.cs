namespace RagCap.Core.Search
{
    public class VssOptions
    {
        public string? Path { get; set; }
        public string? Module { get; set; }
        public string? SearchFunction { get; set; }
        public string? FromBlobFunction { get; set; }
        public bool ForceReindex { get; set; }

        public static VssOptions FromEnvironment()
        {
            return new VssOptions
            {
                Path = System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_PATH"),
                Module = System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_MODULE"),
                SearchFunction = System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_SEARCH"),
                FromBlobFunction = System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_FROMBLOB"),
                ForceReindex = (System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VSS_REINDEX") == "1")
            };
        }
    }
}
