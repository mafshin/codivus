using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.API.LLM;

/// <summary>
/// Factory for creating and managing LLM providers
/// </summary>
public class LlmProviderFactory
{
    private readonly IEnumerable<ILlmProvider> _providers;
    private readonly ILogger<LlmProviderFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the LlmProviderFactory class
    /// </summary>
    /// <param name="providers">Collection of available LLM providers</param>
    /// <param name="logger">Logger</param>
    public LlmProviderFactory(
        IEnumerable<ILlmProvider> providers,
        ILogger<LlmProviderFactory> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// Gets an LLM provider by type
    /// </summary>
    /// <param name="providerType">Provider type to get</param>
    /// <returns>LLM provider if available, default provider otherwise</returns>
    public ILlmProvider GetProvider(LlmProviderType providerType)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderType == providerType);
        
        if (provider == null)
        {
            _logger.LogWarning("Provider type {ProviderType} not available. Using default provider.", providerType);
            provider = _providers.First();
        }
        
        return provider;
    }

    /// <summary>
    /// Gets all available providers
    /// </summary>
    /// <returns>Collection of available providers</returns>
    public async Task<IEnumerable<ILlmProvider>> GetAvailableProvidersAsync()
    {
        var availableProviders = new List<ILlmProvider>();
        
        foreach (var provider in _providers)
        {
            try
            {
                if (await provider.IsAvailableAsync())
                {
                    availableProviders.Add(provider);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking availability of provider {ProviderType}", provider.ProviderType);
            }
        }
        
        return availableProviders;
    }

    /// <summary>
    /// Gets the default provider (first available)
    /// </summary>
    /// <returns>Default provider if any available, null otherwise</returns>
    public async Task<ILlmProvider?> GetDefaultProviderAsync()
    {
        var availableProviders = await GetAvailableProvidersAsync();
        return availableProviders.FirstOrDefault();
    }

    /// <summary>
    /// Gets all available provider types
    /// </summary>
    /// <returns>Collection of available provider types</returns>
    public async Task<IEnumerable<LlmProviderType>> GetAvailableProviderTypesAsync()
    {
        var availableProviders = await GetAvailableProvidersAsync();
        return availableProviders.Select(p => p.ProviderType);
    }

    /// <summary>
    /// Gets all available models for a provider
    /// </summary>
    /// <param name="providerType">Provider type</param>
    /// <returns>Collection of available model names</returns>
    public async Task<IEnumerable<string>> GetAvailableModelsAsync(LlmProviderType providerType)
    {
        var provider = GetProvider(providerType);
        
        try
        {
            if (await provider.IsAvailableAsync())
            {
                return await provider.GetAvailableModelsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models for provider {ProviderType}", providerType);
        }
        
        return new List<string>();
    }
}
