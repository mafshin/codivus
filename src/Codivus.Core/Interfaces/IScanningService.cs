using Codivus.Core.Models;

namespace Codivus.Core.Interfaces;

/// <summary>
/// Interface for scanning-related operations
/// </summary>
public interface IScanningService
{
    /// <summary>
    /// Starts a new scan for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID to scan</param>
    /// <param name="configuration">Scan configuration</param>
    /// <returns>Scan progress object</returns>
    Task<ScanProgress> StartScanAsync(Guid repositoryId, ScanConfiguration configuration);
    
    /// <summary>
    /// Gets the current scan progress
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Current scan progress</returns>
    Task<ScanProgress> GetScanProgressAsync(Guid scanId);
    
    /// <summary>
    /// Gets all scans for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Collection of scan progress objects</returns>
    Task<IEnumerable<ScanProgress>> GetScansByRepositoryAsync(Guid repositoryId);
    
    /// <summary>
    /// Pauses an in-progress scan
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> PauseScanAsync(Guid scanId);
    
    /// <summary>
    /// Resumes a paused scan
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> ResumeScanAsync(Guid scanId);
    
    /// <summary>
    /// Cancels an in-progress or paused scan
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> CancelScanAsync(Guid scanId);
    
    /// <summary>
    /// Deletes a scan and all its related issues
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteScanAsync(Guid scanId);
    
    /// <summary>
    /// Gets issues found in a scan
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Collection of code issues</returns>
    Task<IEnumerable<CodeIssue>> GetScanIssuesAsync(Guid scanId);
    
    /// <summary>
    /// Gets scan configurations for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Collection of scan configurations</returns>
    Task<IEnumerable<ScanConfiguration>> GetScanConfigurationsAsync(Guid repositoryId);
    
    /// <summary>
    /// Gets a scan configuration by ID
    /// </summary>
    /// <param name="configurationId">Configuration ID</param>
    /// <returns>Scan configuration if found, null otherwise</returns>
    Task<ScanConfiguration?> GetScanConfigurationByIdAsync(Guid configurationId);
    
    /// <summary>
    /// Creates a new scan configuration
    /// </summary>
    /// <param name="configuration">Scan configuration to create</param>
    /// <returns>Created scan configuration</returns>
    Task<ScanConfiguration> CreateScanConfigurationAsync(ScanConfiguration configuration);
    
    /// <summary>
    /// Updates an existing scan configuration
    /// </summary>
    /// <param name="configuration">Scan configuration to update</param>
    /// <returns>Updated scan configuration</returns>
    Task<ScanConfiguration> UpdateScanConfigurationAsync(ScanConfiguration configuration);
    
    /// <summary>
    /// Deletes a scan configuration
    /// </summary>
    /// <param name="configurationId">Configuration ID to delete</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteScanConfigurationAsync(Guid configurationId);
}
