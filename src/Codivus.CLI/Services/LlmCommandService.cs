using System.Diagnostics;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class LlmCommandService
{
    private readonly ApiClientService _apiClient;
    private readonly IOutputService _outputService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmCommandService> _logger;

    public LlmCommandService(
        ApiClientService apiClient,
        IOutputService outputService,
        IConfiguration configuration,
        ILogger<LlmCommandService> logger)
    {
        _apiClient = apiClient;
        _outputService = outputService;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<CommandResult<LlmProvidersResult>> ListProvidersAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Listing available LLM providers");

            // List all supported LLM providers
            var providers = new List<LlmProviderInfo>
            {
                new LlmProviderInfo
                {
                    Name = "Ollama",
                    Type = "Ollama",
                    Endpoint = _configuration["LlmProviders:Ollama:Endpoint"] ?? "http://localhost:11434",
                    Status = "Supported",
                    IsAvailable = true
                },
                new LlmProviderInfo
                {
                    Name = "LM Studio",
                    Type = "LmStudio",
                    Endpoint = _configuration["LlmProviders:LmStudio:Endpoint"] ?? "http://localhost:1234",
                    Status = "Supported",
                    IsAvailable = true
                }
            };

            stopwatch.Stop();

            var result = new LlmProvidersResult
            {
                Providers = providers,
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return Task.FromResult(CommandResult<LlmProvidersResult>.SuccessResult(
                result,
                $"Found {providers.Count} supported LLM provider types"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing LLM providers");
            return Task.FromResult(CommandResult<LlmProvidersResult>.ErrorResult($"Failed to list providers: {ex.Message}"));
        }
    }

    public async Task<CommandResult<LlmModelsResult>> ListModelsAsync(string providerType)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Getting available models for provider: {ProviderType}", providerType);

            // Validate provider type
            if (!IsValidProviderType(providerType))
            {
                return CommandResult<LlmModelsResult>.ErrorResult($"Invalid provider type: {providerType}. Valid types are: Ollama, LmStudio");
            }

            // Get models from API
            var modelsResponse = await _apiClient.GetAvailableModelsAsync(providerType);
            
            if (!modelsResponse.Success)
            {
                return CommandResult<LlmModelsResult>.ErrorResult(modelsResponse.Message ?? "Failed to get models");
            }

            var models = modelsResponse.Data ?? new List<string>();
            var isAvailable = models.Any();

            stopwatch.Stop();

            var result = new LlmModelsResult
            {
                Provider = providerType,
                Models = models,
                IsProviderAvailable = isAvailable,
                Success = true,
                Duration = stopwatch.Elapsed
            };

            var message = isAvailable 
                ? $"Found {models.Count} available models for {providerType}"
                : $"No models found for {providerType} (provider may not be running)";

            return CommandResult<LlmModelsResult>.SuccessResult(result, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting models for provider: {ProviderType}", providerType);
            return CommandResult<LlmModelsResult>.ErrorResult($"Failed to get models: {ex.Message}");
        }
    }

    public async Task<CommandResult<LlmTestResult>> TestConnectivityAsync(string providerType)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Testing connectivity for provider: {ProviderType}", providerType);

            // Validate provider type
            if (!IsValidProviderType(providerType))
            {
                return CommandResult<LlmTestResult>.ErrorResult($"Invalid provider type: {providerType}. Valid types are: Ollama, LmStudio");
            }

            // Get models to test connectivity
            var modelsResponse = await _apiClient.GetAvailableModelsAsync(providerType);
            
            var isAvailable = modelsResponse.Success && modelsResponse.Data != null && modelsResponse.Data.Any();
            var models = modelsResponse.Data ?? new List<string>();
            
            string status;
            string message;
            
            if (isAvailable)
            {
                status = "Connected";
                message = $"Successfully connected to {providerType}. Found {models.Count} available models.";
            }
            else
            {
                status = "Not Available";
                message = modelsResponse.Success 
                    ? $"{providerType} is not running or has no models available"
                    : $"Failed to connect to {providerType}: {modelsResponse.Message}";
            }

            stopwatch.Stop();

            var result = new LlmTestResult
            {
                Provider = providerType,
                IsAvailable = isAvailable,
                Status = status,
                Message = message,
                AvailableModels = models.Take(5).ToList(), // Show first 5 models
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<LlmTestResult>.SuccessResult(result, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connectivity for provider: {ProviderType}", providerType);
            return CommandResult<LlmTestResult>.ErrorResult($"Failed to test connectivity: {ex.Message}");
        }
    }

    private bool IsValidProviderType(string providerType)
    {
        return providerType.Equals("Ollama", StringComparison.OrdinalIgnoreCase) ||
               providerType.Equals("LmStudio", StringComparison.OrdinalIgnoreCase);
    }
}