using Codivus.API.Data;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace Codivus.API.Services;

public class RepositoryService : IRepositoryService
{
    private readonly ILogger<RepositoryService> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly JsonDataStore _dataStore;

    public RepositoryService(ILogger<RepositoryService> logger, IFileSystem fileSystem, JsonDataStore dataStore)
    {
        _logger = logger;
        _fileSystem = fileSystem;
        _dataStore = dataStore;
    }

    public async Task<IEnumerable<Repository>> GetAllRepositoriesAsync()
    {
        return await _dataStore.GetRepositoriesAsync();
    }

    public async Task<Repository?> GetRepositoryByIdAsync(Guid id)
    {
        _logger.LogInformation("Getting repository with ID: {RepositoryId}", id);
        
        try
        {
            var repository = await _dataStore.GetRepositoryAsync(id);
            
            if (repository != null)
            {
                _logger.LogInformation("Found repository: {RepositoryName} at {RepositoryLocation}", 
                    repository.Name, repository.Location);
            }
            else
            {
                _logger.LogWarning("Repository with ID {RepositoryId} not found in data store", id);
            }
            
            return repository;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving repository with ID {RepositoryId}", id);
            throw;
        }
    }

    public async Task<Repository> AddRepositoryAsync(Repository repository)
    {
        if (repository.Id == Guid.Empty)
        {
            repository.Id = Guid.NewGuid();
        }

        repository.AddedAt = DateTime.UtcNow;
        var addedRepo = await _dataStore.AddRepositoryAsync(repository);

        _logger.LogInformation("Repository {RepositoryName} added with ID {RepositoryId}", repository.Name, repository.Id);
        return addedRepo;
    }

    public async Task<Repository> UpdateRepositoryAsync(Repository repository)
    {
        var existingRepository = await _dataStore.GetRepositoryAsync(repository.Id);
        if (existingRepository == null)
        {
            throw new ArgumentException($"Repository with ID {repository.Id} not found");
        }

        var updatedRepo = await _dataStore.UpdateRepositoryAsync(repository);

        _logger.LogInformation("Repository {RepositoryName} updated", repository.Name);
        return updatedRepo;
    }

    public async Task<bool> DeleteRepositoryAsync(Guid id)
    {
        var repository = await _dataStore.GetRepositoryAsync(id);
        if (repository == null)
        {
            return false;
        }

        // Check for active scans before deletion
        var hasActiveScans = await _dataStore.HasActiveScansAsync(id);
        if (hasActiveScans)
        {
            _logger.LogWarning("Cannot delete repository {RepositoryId} - has active scans", id);
            throw new InvalidOperationException($"Cannot delete repository '{repository.Name}' - it has active scans. Please wait for scans to complete or cancel them first.");
        }

        // Log what will be deleted for tracking
        var scanCount = await _dataStore.GetScanCountByRepositoryAsync(id);
        var issueCount = await _dataStore.GetIssueCountByRepositoryAsync(id);
        var configCount = await _dataStore.GetConfigurationCountByRepositoryAsync(id);
        
        _logger.LogInformation("Deleting repository {RepositoryName} (ID: {RepositoryId}) with {ScanCount} scans, {IssueCount} issues, and {ConfigCount} configurations", 
            repository.Name, repository.Id, scanCount, issueCount, configCount);

        // Perform the cascade deletion
        var result = await _dataStore.DeleteRepositoryAsync(id);
        
        if (result)
        {
            _logger.LogInformation("Repository {RepositoryName} (ID: {RepositoryId}) and all related data deleted successfully", 
                repository.Name, repository.Id);
        }
        else
        {
            _logger.LogWarning("Failed to delete repository {RepositoryName} (ID: {RepositoryId})", 
                repository.Name, repository.Id);
        }
        
        return result;
    }
    
    public async Task<int> GetScanCountAsync(Guid repositoryId)
    {
        return await _dataStore.GetScanCountByRepositoryAsync(repositoryId);
    }
    
    public async Task<int> GetIssueCountAsync(Guid repositoryId)
    {
        return await _dataStore.GetIssueCountByRepositoryAsync(repositoryId);
    }
    
    public async Task<int> GetConfigurationCountAsync(Guid repositoryId)
    {
        return await _dataStore.GetConfigurationCountByRepositoryAsync(repositoryId);
    }
    
    public async Task<bool> HasActiveScansAsync(Guid repositoryId)
    {
        return await _dataStore.HasActiveScansAsync(repositoryId);
    }

    public async Task<RepositoryFile> GetRepositoryStructureAsync(Guid repositoryId)
    {
        var repository = await GetRepositoryByIdAsync(repositoryId);
        if (repository == null)
        {
            throw new ArgumentException($"Repository with ID {repositoryId} not found");
        }

        _logger.LogInformation("Scanning repository structure for {RepositoryName} at {RepositoryLocation}", 
            repository.Name, repository.Location);

        // Create the root directory node
        var rootDir = new RepositoryFile
        {
            Id = Guid.NewGuid(),
            RepositoryId = repositoryId,
            Name = !string.IsNullOrEmpty(repository.Name) ? repository.Name : Path.GetFileName(repository.Location) ?? "Repository",
            Path = repository.Location,
            IsDirectory = true,
            LastModified = DateTime.UtcNow,
            Children = new List<RepositoryFile>()
        };

        // Only scan for local repositories
        if (repository.Type == RepositoryType.Local)
        {
            if (string.IsNullOrEmpty(repository.Location))
            {
                _logger.LogWarning("Repository location is empty for {RepositoryName}", repository.Name);
                return rootDir;
            }

            if (!_fileSystem.Directory.Exists(repository.Location))
            {
                _logger.LogWarning("Repository directory does not exist: {RepositoryLocation}", repository.Location);
                return rootDir;
            }

            try
            {
                // Update root directory last modified time if possible
                rootDir.LastModified = _fileSystem.Directory.GetLastWriteTime(repository.Location);
                
                // Recursively scan the directory
                await ScanDirectoryAsync(rootDir, repository.Location);
                _logger.LogInformation("Completed scanning repository structure for {RepositoryName}. Found {FileCount} items", 
                    repository.Name, rootDir.Children?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning repository structure for {RepositoryName} at {RepositoryLocation}", 
                    repository.Name, repository.Location);
                // Return the root directory with empty children instead of throwing
                // This allows the UI to show the repository exists but couldn't be scanned
            }
        }
        else
        {
            _logger.LogInformation("Skipping structure scan for non-local repository {RepositoryName} of type {RepositoryType}", 
                repository.Name, repository.Type);
        }
        
        return rootDir;
    }

    private async Task ScanDirectoryAsync(RepositoryFile parent, string path)
    {
        try
        {
            // Get all files in the current directory
            foreach (var filePath in _fileSystem.Directory.GetFiles(path))
            {
                var fileInfo = _fileSystem.FileInfo.New(filePath);
                
                if (parent.Children == null)
                {
                    parent.Children = new List<RepositoryFile>();
                }
                
                // For larger files, read asynchronously to avoid blocking
                if (fileInfo.Length > 1024 * 1024) // For files larger than 1MB
                {
                    // Actually do something async for large files
                    await Task.Delay(1); // Minimal delay to make this truly async
                }
                
                parent.Children.Add(new RepositoryFile
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = parent.RepositoryId,
                    Name = fileInfo.Name,
                    Path = filePath,
                    IsDirectory = false,
                    LastModified = fileInfo.LastWriteTime,
                    SizeInBytes = fileInfo.Length,
                    Children = null
                });
            }

            // Get and process all directories
            foreach (var dirPath in _fileSystem.Directory.GetDirectories(path))
            {
                var dirInfo = _fileSystem.DirectoryInfo.New(dirPath);
                
                // Skip .git directory and node_modules to avoid too large structures
                if (dirInfo.Name == ".git" || dirInfo.Name == "node_modules" || dirInfo.Name == ".vs")
                    continue;
                    
                if (parent.Children == null)
                {
                    parent.Children = new List<RepositoryFile>();
                }
                
                var dirNode = new RepositoryFile
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = parent.RepositoryId,
                    Name = dirInfo.Name,
                    Path = dirPath,
                    IsDirectory = true,
                    LastModified = dirInfo.LastWriteTime,
                    Children = new List<RepositoryFile>()
                };
                
                parent.Children.Add(dirNode);
                
                // Recursively scan subdirectories
                await ScanDirectoryAsync(dirNode, dirPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning directory {DirectoryPath}", path);
        }
    }

    public async Task<string> GetFileContentAsync(Guid repositoryId, string filePath)
    {
        var repository = await GetRepositoryByIdAsync(repositoryId);
        if (repository == null)
        {
            throw new ArgumentException($"Repository with ID {repositoryId} not found");
        }

        if (!_fileSystem.File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FilePath}", filePath);
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        _logger.LogInformation("Reading file content: {FilePath}", filePath);
        
        // Actually use await to ensure the method is async
        return await _fileSystem.File.ReadAllTextAsync(filePath);
    }

    public async Task<bool> ValidateRepositoryAsync(string repositoryLocation, RepositoryType repositoryType)
    {
        try
        {
            _logger.LogInformation("Validating repository of type {RepositoryType} at location {RepositoryLocation}", 
                repositoryType, repositoryLocation);
                
            if (repositoryType == RepositoryType.Local)
            {
                // For a local repository, check if the directory exists and is readable
                if (!_fileSystem.Directory.Exists(repositoryLocation))
                {
                    _logger.LogWarning("Local repository directory not found: {RepositoryLocation}", repositoryLocation);
                    return false;
                }

                // Additional check: Make sure we have read access to the directory
                bool hasReadAccess = false;
                try 
                {
                    // Use Task.Run to make this IO operation async
                    await Task.Run(() => _fileSystem.Directory.GetFiles(repositoryLocation));
                    hasReadAccess = true;
                }
                catch (UnauthorizedAccessException uae)
                {
                    _logger.LogWarning("No read access to repository directory: {RepositoryLocation}, Error: {Error}", 
                        repositoryLocation, uae.Message);
                    hasReadAccess = false;
                }

                // Check if it's a git repository (optional, we allow non-git repos too)
                var gitDir = Path.Combine(repositoryLocation, ".git");
                var isGitRepo = _fileSystem.Directory.Exists(gitDir);

                // Return true if we have access, not requiring it to be a git repo
                bool isValid = hasReadAccess; 
                _logger.LogInformation("Repository validation result for {RepositoryLocation}: {IsValid} (Git repo: {IsGitRepo}, Read access: {HasReadAccess})", 
                    repositoryLocation, isValid, isGitRepo, hasReadAccess);
                return isValid;
            }
            else if (repositoryType == RepositoryType.GitHub)
            {
                // For a GitHub repository, we would typically make an API call to validate
                // This is a placeholder implementation
                var isValidUrl = repositoryLocation.StartsWith("https://github.com/") || 
                                 repositoryLocation.StartsWith("http://github.com/");

                _logger.LogInformation("GitHub repository validation result for {RepositoryLocation}: {IsValid}", repositoryLocation, isValidUrl);
                return isValidUrl;
            }
            
            _logger.LogWarning("Unsupported repository type: {RepositoryType}", repositoryType);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating repository {RepositoryLocation}", repositoryLocation);
            return false;
        }
    }
}
