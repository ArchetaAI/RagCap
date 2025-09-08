using System.Security.Cryptography;
using System.Text;

namespace RagCap.Core.Utils
{
    public static class HashUtils
    {
        public static string GetSha256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
