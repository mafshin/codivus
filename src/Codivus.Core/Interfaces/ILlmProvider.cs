using Codivus.Core.Models;

namespace Codivus.Core.Interfaces;

/// <summary>
/// Interface for LLM providers
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Gets the provider type
    /// </summary>
    LlmProviderType ProviderType { get; }
    
    /// <summary>
    /// Gets the available models
    /// </summary>
    /// <returns>Collection of available model names</returns>
    Task<IEnumerable<string>> GetAvailableModelsAsync();
    
    /// <summary>
    /// Checks if the provider is available
    /// </summary>
    /// <returns>True if available, false otherwise</returns>
    Task<bool> IsAvailableAsync();
    
    /// <summary>
    /// Analyzes code using the LLM
    /// </summary>
    /// <param name="code">Code to analyze</param>
    /// <param name="filePath">Path to the file</param>
    /// <param name="configuration">Scan configuration</param>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Collection of detected issues</returns>
    Task<IEnumerable<CodeIssue>> AnalyzeCodeAsync(string code, string filePath, ScanConfiguration configuration, Guid scanId);
    
    /// <summary>
    /// Generates a fix suggestion for an issue
    /// </summary>
    /// <param name="issue">Code issue to fix</param>
    /// <param name="code">Original code</param>
    /// <returns>Suggested fix</returns>
    Task<string> GenerateFixSuggestionAsync(CodeIssue issue, string code);
}
