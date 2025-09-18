using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RagCap.Core.Utils
{
    /// <summary>
    /// Represents the global configuration for RagCap.
    /// </summary>
    public class Config
    {
        /// <summary>
        /// Gets or sets the API configuration.
        /// </summary>
        public ApiConfig? Api { get; set; }

        /// <summary>
        /// Gets or sets the embedding configuration.
        /// </summary>
        public EmbeddingConfig? Embedding { get; set; }

        /// <summary>
        /// Gets or sets the answer generation configuration.
        /// </summary>
        public AnswerConfig? Answer { get; set; }
    }

    /// <summary>
    /// Represents the API configuration.
    /// </summary>
    public class ApiConfig
    {
        /// <summary>
        /// Gets or sets the API key.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the API endpoint.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the API version.
        /// </summary>
        public string? ApiVersion { get; set; }
    }

    /// <summary>
    /// Represents the embedding configuration.
    /// </summary>
    public class EmbeddingConfig
    {
        /// <summary>
        /// Gets or sets the embedding provider.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the embedding model.
        /// </summary>
        public string? Model { get; set; }
    }

    /// <summary>
    /// Represents the answer generation configuration.
    /// </summary>
    public class AnswerConfig
    {
        /// <summary>
        /// Gets or sets the answer generation provider.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the answer generation model.
        /// </summary>
        public string? Model { get; set; }
    }

    /// <summary>
    /// Manages the global configuration for RagCap.
    /// </summary>
    public static class ConfigManager
    {
        private static readonly string ConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ragcap", "config.yml");
        private static Config? _config;

        /// <summary>
        /// Gets the global configuration.
        /// </summary>
        /// <returns>The global configuration.</returns>
        public static Config GetConfig()
        {
            if (_config == null)
            {
                if (File.Exists(ConfigFilePath))
                {
                    var yamlContent = File.ReadAllText(ConfigFilePath);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
                    _config = deserializer.Deserialize<Config>(yamlContent);
                }
                else
                {
                    _config = new Config();
                }
            }
            return _config;
        }
    }
}