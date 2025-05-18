using Codivus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codivus.API.Data;

/// <summary>
/// A JSON file-based data store for repositories and scans
/// </summary>
public class JsonDataStore
{
    private readonly ILogger<JsonDataStore> _logger;
    private readonly string _dataDirectory;
    private readonly string _repositoriesFilePath;
    private readonly string _scansFilePath;
    private readonly string _issuesFilePath;
    private readonly string _configurationsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    // In-memory cache of data
    private List<Repository> _repositories = new();
    private List<ScanProgress> _scans = new();
    private List<CodeIssue> _issues = new();
    private List<ScanConfiguration> _configurations = new();
    
    // Using a single lock object for file operations
    private readonly object _saveLock = new();

    public JsonDataStore(ILogger<JsonDataStore> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _repositoriesFilePath = Path.Combine(_dataDirectory, "repositories.json");
        _scansFilePath = Path.Combine(_dataDirectory, "scans.json");
        _issuesFilePath = Path.Combine(_dataDirectory, "issues.json");
        _configurationsFilePath = Path.Combine(_dataDirectory, "configurations.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        
        EnsureDataDirectory();
        LoadData();
    }
    
    private void EnsureDataDirectory()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
            _logger.LogInformation("Created data directory: {DataDirectory}", _dataDirectory);
        }
    }

    private void LoadData()
    {
        try
        {
            // Load repositories
            if (File.Exists(_repositoriesFilePath))
            {
                var json = File.ReadAllText(_repositoriesFilePath);
                _repositories = JsonSerializer.Deserialize<List<Repository>>(json, _jsonOptions) ?? new List<Repository>();
                _logger.LogInformation("Loaded {Count} repositories from file", _repositories.Count);
            }
            
            // Load scans
            if (File.Exists(_scansFilePath))
            {
                var json = File.ReadAllText(_scansFilePath);
                _scans = JsonSerializer.Deserialize<List<ScanProgress>>(json, _jsonOptions) ?? new List<ScanProgress>();
                _logger.LogInformation("Loaded {Count} scans from file", _scans.Count);
            }
            
            // Load issues
            if (File.Exists(_issuesFilePath))
            {
                var json = File.ReadAllText(_issuesFilePath);
                _issues = JsonSerializer.Deserialize<List<CodeIssue>>(json, _jsonOptions) ?? new List<CodeIssue>();
                _logger.LogInformation("Loaded {Count} issues from file", _issues.Count);
            }
            
            // Load configurations
            if (File.Exists(_configurationsFilePath))
            {
                var json = File.ReadAllText(_configurationsFilePath);
                _configurations = JsonSerializer.Deserialize<List<ScanConfiguration>>(json, _jsonOptions) ?? new List<ScanConfiguration>();
                _logger.LogInformation("Loaded {Count} configurations from file", _configurations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading data from JSON files");
        }
    }
    
    private Task SaveRepositories()
    {
        return Task.Run(() => SaveRepositoriesInternal());
    }
    
    private void SaveRepositoriesInternal()
    {
        try
        {
            // Create a snapshot of the data for serialization
            var reposCopy = new List<Repository>(_repositories);
            
            // Serialize and save using a lock only for the file write
            var json = JsonSerializer.Serialize(reposCopy, _jsonOptions);
            lock (_saveLock)
            {
                File.WriteAllText(_repositoriesFilePath, json);
            }
            _logger.LogInformation("Saved {Count} repositories to file", reposCopy.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving repositories to JSON file");
        }
    }
    
    // This method is used by both async and sync code paths
    public Task SaveScans()
    {
        return Task.Run(() => SaveScansInternal());
    }

    private void SaveScansInternal()
    {
        try
        {
            // Create a snapshot of the data for serialization
            var scansCopy = new List<ScanProgress>(_scans);
            
            // Serialize and save using a lock only for the file write
            var json = JsonSerializer.Serialize(scansCopy, _jsonOptions);
            lock (_saveLock)
            {
                File.WriteAllText(_scansFilePath, json);
            }
            _logger.LogInformation("Saved {Count} scans to file", scansCopy.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving scans to JSON file");
        }
    }
    
    private Task SaveIssues()
    {
        return Task.Run(() => SaveIssuesInternal());
    }
    
    private void SaveIssuesInternal()
    {
        try
        {
            // Create a snapshot of the data for serialization
            var issuesCopy = new List<CodeIssue>(_issues);
            
            // Serialize and save using a lock only for the file write
            var json = JsonSerializer.Serialize(issuesCopy, _jsonOptions);
            lock (_saveLock)
            {
                File.WriteAllText(_issuesFilePath, json);
            }
            _logger.LogInformation("Saved {Count} issues to file", issuesCopy.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving issues to JSON file");
        }
    }
    
    private Task SaveConfigurations()
    {
        return Task.Run(() => SaveConfigurationsInternal());
    }
    
    private void SaveConfigurationsInternal()
    {
        try
        {
            // Create a snapshot of the data for serialization
            var configsCopy = new List<ScanConfiguration>(_configurations);
            
            // Serialize and save using a lock only for the file write
            var json = JsonSerializer.Serialize(configsCopy, _jsonOptions);
            lock (_saveLock)
            {
                File.WriteAllText(_configurationsFilePath, json);
            }
            _logger.LogInformation("Saved {Count} configurations to file", configsCopy.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configurations to JSON file");
        }
    }

    // Repository methods
    
    public Task<IEnumerable<Repository>> GetRepositoriesAsync()
    {
        // Create and return a copy to avoid modification issues
        return Task.FromResult<IEnumerable<Repository>>(new List<Repository>(_repositories));
    }
    
    public Task<Repository?> GetRepositoryAsync(Guid id)
    {
        _logger.LogInformation("DataStore: Looking for repository with ID: {RepositoryId}", id);
        var repository = _repositories.FirstOrDefault(r => r.Id == id);
        
        if (repository != null)
        {
            _logger.LogInformation("DataStore: Found repository: {RepositoryName}", repository.Name);
        }
        else
        {
            _logger.LogWarning("DataStore: Repository with ID {RepositoryId} not found in collection of {Count} repositories", id, _repositories.Count);
            // Log all repository IDs for debugging
            _logger.LogInformation("DataStore: Available repository IDs: {RepositoryIds}", string.Join(", ", _repositories.Select(r => r.Id)));
        }
        
        return Task.FromResult(repository);
    }
    
    public async Task<Repository> AddRepositoryAsync(Repository repository)
    {
        // Set ID and timestamp
        if (repository.Id == Guid.Empty)
        {
            repository.Id = Guid.NewGuid();
        }
        repository.AddedAt = DateTime.UtcNow;
        
        // Add to collection
        _repositories.Add(repository);
        
        // Save asynchronously
        await SaveRepositories();
        return repository;
    }
    
    public async Task<Repository> UpdateRepositoryAsync(Repository repository)
    {
        // Find and update
        var existingIndex = _repositories.FindIndex(r => r.Id == repository.Id);
        if (existingIndex == -1)
        {
            throw new ArgumentException($"Repository with ID {repository.Id} not found");
        }
        
        _repositories[existingIndex] = repository;
        
        // Save asynchronously
        await SaveRepositories();
        return repository;
    }
    
    public async Task<bool> DeleteRepositoryAsync(Guid id)
    {
        // Find and remove
        var repository = _repositories.FirstOrDefault(r => r.Id == id);
        if (repository == null)
        {
            return false;
        }

        // Cascade delete: Remove all related data
        await DeleteRepositoryCascadeAsync(id);
        
        bool removed = _repositories.Remove(repository);
        
        // Only save if we actually removed something
        if (removed)
        {
            await SaveRepositories();
        }
        
        return removed;
    }
    
    /// <summary>
    /// Cascade delete all data related to a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID to delete data for</param>
    private async Task DeleteRepositoryCascadeAsync(Guid repositoryId)
    {
        _logger.LogInformation("Starting cascade delete for repository {RepositoryId}", repositoryId);
        
        // 1. Delete all issues related to the repository
        var repositoryIssues = _issues.Where(i => i.RepositoryId == repositoryId).ToList();
        _logger.LogInformation("Found {IssueCount} issues to delete for repository {RepositoryId}", 
            repositoryIssues.Count, repositoryId);
        
        foreach (var issue in repositoryIssues)
        {
            _issues.Remove(issue);
        }
        
        // Save issues if any were removed
        if (repositoryIssues.Any())
        {
            await SaveIssues();
        }
        
        // 2. Delete all scans related to the repository
        var repositoryScans = _scans.Where(s => s.RepositoryId == repositoryId).ToList();
        _logger.LogInformation("Found {ScanCount} scans to delete for repository {RepositoryId}", 
            repositoryScans.Count, repositoryId);
        
        foreach (var scan in repositoryScans)
        {
            _scans.Remove(scan);
        }
        
        // Save scans if any were removed
        if (repositoryScans.Any())
        {
            await SaveScans();
        }
        
        // 3. Delete all configurations related to the repository
        var repositoryConfigurations = _configurations.Where(c => c.RepositoryId == repositoryId).ToList();
        _logger.LogInformation("Found {ConfigCount} configurations to delete for repository {RepositoryId}", 
            repositoryConfigurations.Count, repositoryId);
        
        foreach (var config in repositoryConfigurations)
        {
            _configurations.Remove(config);
        }
        
        // Save configurations if any were removed
        if (repositoryConfigurations.Any())
        {
            await SaveConfigurations();
        }
        
        _logger.LogInformation("Cascade delete completed for repository {RepositoryId}. Deleted {IssueCount} issues, {ScanCount} scans, {ConfigCount} configurations", 
            repositoryId, repositoryIssues.Count, repositoryScans.Count, repositoryConfigurations.Count);
    }
    
    // Scan methods
    
    public Task<IEnumerable<ScanProgress>> GetScansAsync()
    {
        return Task.FromResult<IEnumerable<ScanProgress>>(new List<ScanProgress>(_scans));
    }
    
    public Task<IEnumerable<ScanProgress>> GetScansByRepositoryAsync(Guid repositoryId)
    {
        return Task.FromResult<IEnumerable<ScanProgress>>(_scans.Where(s => s.RepositoryId == repositoryId).ToList());
    }
    
    public Task<ScanProgress?> GetScanAsync(Guid id)
    {
        return Task.FromResult(_scans.FirstOrDefault(s => s.Id == id));
    }
    
    public async Task<ScanProgress> AddScanAsync(ScanProgress scan)
    {
        // Set ID
        if (scan.Id == Guid.Empty)
        {
            scan.Id = Guid.NewGuid();
        }
        
        // Add to collection
        _scans.Add(scan);
        
        // Save asynchronously
        await SaveScans();
        return scan;
    }
    
    public async Task<ScanProgress> UpdateScanAsync(ScanProgress scan)
    {
        // Find and update
        var existingIndex = _scans.FindIndex(s => s.Id == scan.Id);
        if (existingIndex == -1)
        {
            throw new ArgumentException($"Scan with ID {scan.Id} not found");
        }
        
        _scans[existingIndex] = scan;
        
        // Save asynchronously
        await SaveScans();
        return scan;
    }
    
    public async Task<bool> DeleteScanAsync(Guid id)
    {
        // Find and remove
        var scan = _scans.FirstOrDefault(s => s.Id == id);
        if (scan == null)
        {
            return false;
        }
        
        bool removed = _scans.Remove(scan);
        
        // Only save if we actually removed something
        if (removed)
        {
            await SaveScans();
        }
        
        return removed;
    }
    
    // Issue methods
    
    public Task<IEnumerable<CodeIssue>> GetIssuesAsync()
    {
        return Task.FromResult<IEnumerable<CodeIssue>>(new List<CodeIssue>(_issues));
    }
    
    public Task<IEnumerable<CodeIssue>> GetIssuesByRepositoryAsync(Guid repositoryId)
    {
        return Task.FromResult<IEnumerable<CodeIssue>>(_issues.Where(i => i.RepositoryId == repositoryId).ToList());
    }
    
    public Task<IEnumerable<CodeIssue>> GetIssuesByScanAsync(Guid scanId)
    {
        return Task.FromResult<IEnumerable<CodeIssue>>(_issues.Where(i => i.ScanId == scanId).ToList());
    }
    
    public Task<CodeIssue?> GetIssueAsync(Guid id)
    {
        return Task.FromResult(_issues.FirstOrDefault(i => i.Id == id));
    }
    
    public async Task<CodeIssue> AddIssueAsync(CodeIssue issue)
    {
        // Set ID
        if (issue.Id == Guid.Empty)
        {
            issue.Id = Guid.NewGuid();
        }
        
        // Add to collection
        _issues.Add(issue);
        
        // Save asynchronously
        await SaveIssues();
        return issue;
    }
    
    public async Task<CodeIssue> UpdateIssueAsync(CodeIssue issue)
    {
        // Find and update
        var existingIndex = _issues.FindIndex(i => i.Id == issue.Id);
        if (existingIndex == -1)
        {
            throw new ArgumentException($"Issue with ID {issue.Id} not found");
        }
        
        _issues[existingIndex] = issue;
        
        // Save asynchronously
        await SaveIssues();
        return issue;
    }
    
    public async Task<bool> DeleteIssueAsync(Guid id)
    {
        // Find and remove
        var issue = _issues.FirstOrDefault(i => i.Id == id);
        if (issue == null)
        {
            return false;
        }
        
        bool removed = _issues.Remove(issue);
        
        // Only save if we actually removed something
        if (removed)
        {
            await SaveIssues();
        }
        
        return removed;
    }
    
    // Configuration methods
    
    public Task<IEnumerable<ScanConfiguration>> GetConfigurationsAsync()
    {
        return Task.FromResult<IEnumerable<ScanConfiguration>>(new List<ScanConfiguration>(_configurations));
    }
    
    public Task<IEnumerable<ScanConfiguration>> GetConfigurationsByRepositoryAsync(Guid repositoryId)
    {
        return Task.FromResult<IEnumerable<ScanConfiguration>>(_configurations.Where(c => c.RepositoryId == repositoryId).ToList());
    }
    
    public Task<ScanConfiguration?> GetConfigurationAsync(Guid id)
    {
        return Task.FromResult(_configurations.FirstOrDefault(c => c.Id == id));
    }
    
    public async Task<ScanConfiguration> AddConfigurationAsync(ScanConfiguration configuration)
    {
        // Set ID and timestamps
        if (configuration.Id == Guid.Empty)
        {
            configuration.Id = Guid.NewGuid();
        }
        
        configuration.CreatedAt = DateTime.UtcNow;
        configuration.UpdatedAt = DateTime.UtcNow;
        
        // Add to collection
        _configurations.Add(configuration);
        
        // Save asynchronously
        await SaveConfigurations();
        return configuration;
    }
    
    public async Task<ScanConfiguration> UpdateConfigurationAsync(ScanConfiguration configuration)
    {
        // Update timestamp
        configuration.UpdatedAt = DateTime.UtcNow;
        
        // Find and update
        var existingIndex = _configurations.FindIndex(c => c.Id == configuration.Id);
        if (existingIndex == -1)
        {
            throw new ArgumentException($"Configuration with ID {configuration.Id} not found");
        }
        
        _configurations[existingIndex] = configuration;
        
        // Save asynchronously
        await SaveConfigurations();
        return configuration;
    }
    
    public async Task<bool> DeleteConfigurationAsync(Guid id)
    {
        // Find and remove
        var configuration = _configurations.FirstOrDefault(c => c.Id == id);
        if (configuration == null)
        {
            return false;
        }
        
        bool removed = _configurations.Remove(configuration);
        
        // Only save if we actually removed something
        if (removed)
        {
            await SaveConfigurations();
        }
        
        return removed;
    }
    
    // Utility methods for repository relationships
    
    /// <summary>
    /// Get count of scans for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Number of scans for the repository</returns>
    public Task<int> GetScanCountByRepositoryAsync(Guid repositoryId)
    {
        var count = _scans.Count(s => s.RepositoryId == repositoryId);
        return Task.FromResult(count);
    }
    
    /// <summary>
    /// Get count of issues for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Number of issues for the repository</returns>
    public Task<int> GetIssueCountByRepositoryAsync(Guid repositoryId)
    {
        var count = _issues.Count(i => i.RepositoryId == repositoryId);
        return Task.FromResult(count);
    }
    
    /// <summary>
    /// Get count of configurations for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Number of configurations for the repository</returns>
    public Task<int> GetConfigurationCountByRepositoryAsync(Guid repositoryId)
    {
        var count = _configurations.Count(c => c.RepositoryId == repositoryId);
        return Task.FromResult(count);
    }
    
    /// <summary>
    /// Check if a repository has active scans
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>True if repository has active (InProgress or Paused) scans</returns>
    public Task<bool> HasActiveScansAsync(Guid repositoryId)
    {
        var hasActiveScans = _scans.Any(s => s.RepositoryId == repositoryId && 
            (s.Status == ScanStatus.InProgress || s.Status == ScanStatus.Paused));
        return Task.FromResult(hasActiveScans);
    }
}
