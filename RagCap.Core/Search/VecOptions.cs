namespace RagCap.Core.Search
{
    public class VecOptions
    {
        public string? Path { get; set; }
        public string? Module { get; set; } = "vec0";
        public bool ForceReindex { get; set; }

        public static VecOptions FromEnvironment()
        {
            return new VecOptions
            {
                Path = System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VEC_PATH"),
                Module = System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VEC_MODULE") ?? "vec0",
                ForceReindex = (System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VEC_REINDEX") == "1"),
            };
        }
    }
}
