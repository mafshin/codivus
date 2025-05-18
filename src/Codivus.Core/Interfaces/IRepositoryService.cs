using Codivus.Core.Models;

namespace Codivus.Core.Interfaces;

/// <summary>
/// Interface for repository-related operations
/// </summary>
public interface IRepositoryService
{
    /// <summary>
    /// Gets all repositories
    /// </summary>
    /// <returns>Collection of repositories</returns>
    Task<IEnumerable<Repository>> GetAllRepositoriesAsync();
    
    /// <summary>
    /// Gets a repository by ID
    /// </summary>
    /// <param name="id">Repository ID</param>
    /// <returns>Repository if found, null otherwise</returns>
    Task<Repository?> GetRepositoryByIdAsync(Guid id);
    
    /// <summary>
    /// Adds a new repository
    /// </summary>
    /// <param name="repository">Repository to add</param>
    /// <returns>Added repository</returns>
    Task<Repository> AddRepositoryAsync(Repository repository);
    
    /// <summary>
    /// Updates an existing repository
    /// </summary>
    /// <param name="repository">Repository to update</param>
    /// <returns>Updated repository</returns>
    Task<Repository> UpdateRepositoryAsync(Repository repository);
    
    /// <summary>
    /// Deletes a repository and all associated scans and issues
    /// </summary>
    /// <param name="id">Repository ID to delete</param>
    /// <returns>True if successful, false otherwise</returns>
    /// <exception cref="InvalidOperationException">Thrown when repository has active scans</exception>
    Task<bool> DeleteRepositoryAsync(Guid id);
    
    /// <summary>
    /// Gets the count of scans for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Number of scans</returns>
    Task<int> GetScanCountAsync(Guid repositoryId);
    
    /// <summary>
    /// Gets the count of issues for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Number of issues</returns>
    Task<int> GetIssueCountAsync(Guid repositoryId);
    
    /// <summary>
    /// Gets the count of configurations for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Number of configurations</returns>
    Task<int> GetConfigurationCountAsync(Guid repositoryId);
    
    /// <summary>
    /// Checks if a repository has active scans
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>True if has active scans, false otherwise</returns>
    Task<bool> HasActiveScansAsync(Guid repositoryId);
    
    /// <summary>
    /// Gets the file structure of a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Root directory of the repository</returns>
    Task<RepositoryFile> GetRepositoryStructureAsync(Guid repositoryId);
    
    /// <summary>
    /// Gets the content of a file in a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <param name="filePath">Path to the file</param>
    /// <returns>File content as string</returns>
    Task<string> GetFileContentAsync(Guid repositoryId, string filePath);
    
    /// <summary>
    /// Validates if a repository exists at the given location
    /// </summary>
    /// <param name="repositoryLocation">Repository path or URL</param>
    /// <param name="repositoryType">Repository type (Local or GitHub)</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateRepositoryAsync(string repositoryLocation, RepositoryType repositoryType);
}
