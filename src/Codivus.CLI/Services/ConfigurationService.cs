using System.Text.Json;
using Codivus.CLI.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _configDirectory = GetConfigDirectory();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<T?> GetConfigurationAsync<T>(string key) where T : class
    {
        try
        {
            var filePath = Path.Combine(_configDirectory, $"{key}.json");
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read configuration for key: {Key}", key);
            return null;
        }
    }

    public async Task SetConfigurationAsync<T>(string key, T value) where T : class
    {
        try
        {
            await EnsureConfigDirectoryExistsAsync();
            
            var filePath = Path.Combine(_configDirectory, $"{key}.json");
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation("Configuration saved for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration for key: {Key}", key);
            throw;
        }
    }

    public async Task<string> GetConfigurationPathAsync()
    {
        await EnsureConfigDirectoryExistsAsync();
        return _configDirectory;
    }

    public async Task InitializeConfigurationAsync()
    {
        try
        {
            await EnsureConfigDirectoryExistsAsync();
            
            // Create default configuration files if they don't exist
            await CreateDefaultConfigurationAsync("scan", new
            {
                DefaultFilePatterns = new[] { "*.cs", "*.js", "*.ts", "*.py", "*.java", "*.cpp", "*.h" },
                ExcludePatterns = new[] { "bin/**", "obj/**", "node_modules/**", ".git/**", "*.min.js" },
                MaxFileSize = 1048576,
                EnableParallelProcessing = true,
                MaxConcurrency = 4
            });

            await CreateDefaultConfigurationAsync("graph", new
            {
                Enabled = false,
                Neo4j = new
                {
                    Uri = "bolt://localhost:7687",
                    Username = "neo4j",
                    Password = "pass12345678",
                    Database = "neo4j",
                    MaxConnectionPoolSize = 50,
                    ConnectionAcquisitionTimeout = "00:01:00",
                    ConnectionTimeout = "00:00:30",
                    EnableEncryption = false,
                    TrustStrategy = "TrustAllCertificates"
                }
            });

            await CreateDefaultConfigurationAsync("llm", new
            {
                DefaultProvider = "Ollama",
                Providers = new
                {
                    Ollama = new
                    {
                        BaseUrl = "http://localhost:11434",
                        DefaultModel = "codellama:7b",
                        Timeout = 300
                    },
                    LMStudio = new
                    {
                        BaseUrl = "http://localhost:1234",
                        DefaultModel = "codellama-7b-instruct",
                        Timeout = 300
                    }
                }
            });

            _logger.LogInformation("Configuration initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize configuration");
            throw;
        }
    }

    public async Task<bool> ConfigurationExistsAsync()
    {
        await EnsureConfigDirectoryExistsAsync();
        return Directory.GetFiles(_configDirectory, "*.json").Any();
    }

    private async Task EnsureConfigDirectoryExistsAsync()
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
            _logger.LogDebug("Created configuration directory: {Directory}", _configDirectory);
        }
    }

    private async Task CreateDefaultConfigurationAsync(string key, object defaultConfig)
    {
        var filePath = Path.Combine(_configDirectory, $"{key}.json");
        if (!File.Exists(filePath))
        {
            var json = JsonSerializer.Serialize(defaultConfig, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Created default configuration file: {FilePath}", filePath);
        }
    }

    private string GetConfigDirectory()
    {
        // Try to get from configuration first
        var configDir = _configuration.GetValue<string>("Codivus:ConfigDirectory");
        if (!string.IsNullOrEmpty(configDir))
        {
            return Path.GetFullPath(configDir);
        }

        // Fall back to user profile directory
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".codivus");
    }
}