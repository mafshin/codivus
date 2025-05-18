using Codivus.Core.Models;

namespace Codivus.Core.Interfaces;

/// <summary>
/// Interface for the IssueHunter analyzer
/// </summary>
public interface IIssueHunterAnalyzer
{
    /// <summary>
    /// Analyzes a file for issues
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="content">Content of the file</param>
    /// <param name="repositoryId">Repository ID</param>
    /// <param name="configuration">Scan configuration</param>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Collection of detected issues</returns>
    Task<IEnumerable<CodeIssue>> AnalyzeFileAsync(string filePath, string content, Guid repositoryId, ScanConfiguration configuration, Guid scanId);
    
    /// <summary>
    /// Gets supported file extensions
    /// </summary>
    /// <returns>Collection of supported file extensions</returns>
    IEnumerable<string> GetSupportedExtensions();
    
    /// <summary>
    /// Gets supported issue categories
    /// </summary>
    /// <returns>Collection of supported issue categories</returns>
    IEnumerable<IssueCategory> GetSupportedCategories();
}
