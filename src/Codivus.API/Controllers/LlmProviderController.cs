using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codivus.API.Controllers;

/// <summary>
/// Controller for managing LLM provider operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LlmProviderController : ControllerBase
{
    private readonly IEnumerable<ILlmProvider> _llmProviders;
    private readonly ILogger<LlmProviderController> _logger;

    /// <summary>
    /// Initializes a new instance of the LlmProviderController class
    /// </summary>
    /// <param name="llmProviders">Available LLM providers</param>
    /// <param name="logger">Logger</param>
    public LlmProviderController(
        IEnumerable<ILlmProvider> llmProviders,
        ILogger<LlmProviderController> logger)
    {
        _llmProviders = llmProviders;
        _logger = logger;
    }

    /// <summary>
    /// Gets the available models for a specific LLM provider
    /// </summary>
    /// <param name="providerType">Provider type</param>
    /// <returns>List of available models</returns>
    [HttpGet("models")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableModels([FromQuery] string providerType)
    {
        _logger.LogInformation("Getting available models for provider: {ProviderType}", providerType);
        
        if (string.IsNullOrEmpty(providerType))
        {
            return BadRequest("Provider type is required");
        }
        
        // Parse the provider type
        if (!Enum.TryParse<LlmProviderType>(providerType, true, out var type))
        {
            return BadRequest($"Invalid provider type: {providerType}");
        }
        
        // Find the provider
        var provider = _llmProviders.FirstOrDefault(p => p.ProviderType == type);
        if (provider == null)
        {
            return NotFound($"Provider {providerType} not found");
        }
        
        try
        {
            // Check if the provider is available
            var isAvailable = false;
            try
            {
                isAvailable = await provider.IsAvailableAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking availability for provider {ProviderType}, assuming unavailable", providerType);
                isAvailable = false;
            }
            
            // Get available models (even if provider is not available, return default models)
            var models = new List<string>();
            try
            {
                models = (await provider.GetAvailableModelsAsync()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models for provider {ProviderType}, using defaults", providerType);
                
                // Add some default models based on provider type
                if (type == LlmProviderType.Ollama)
                {
                    models.Add("codellama:7b-instruct");
                    models.Add("llama2:7b");
                }
                else if (type == LlmProviderType.LmStudio)
                {
                    models.Add("default");
                }
            }
            
            // Return even if provider is not available
            // This allows the user to select models even if the provider is not running
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models for provider {ProviderType}", providerType);
            return StatusCode(500, "Error retrieving available models");
        }
    }
}
