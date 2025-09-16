namespace RagCap.Core.Search
{
    public class VecOptions
    {
        public string? Path { get; set; }
        public string? Module { get; set; } = "vec0";

        public static VecOptions FromEnvironment()
        {
            return new VecOptions
            {
                Path = System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VEC_PATH"),
                Module = System.Environment.GetEnvironmentVariable("RAGCAP_SQLITE_VEC_MODULE") ?? "vec0",
            };
        }
    }
}
