using Codivus.API.Data;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.API.Services;

public class ScanningService : IScanningService
{
    private readonly ILogger<ScanningService> _logger;
    private readonly IRepositoryService _repositoryService;
    private readonly JsonDataStore _dataStore;
    private readonly ILlmProvider _llmProvider;

    public ScanningService(
        ILogger<ScanningService> logger,
        IRepositoryService repositoryService,
        JsonDataStore dataStore,
        ILlmProvider llmProvider)
    {
        _logger = logger;
        _repositoryService = repositoryService;
        _dataStore = dataStore;
        _llmProvider = llmProvider;
    }

    public async Task<ScanProgress> StartScanAsync(Guid repositoryId, ScanConfiguration configuration)
    {
        var repository = await _repositoryService.GetRepositoryByIdAsync(repositoryId);
        if (repository == null)
        {
            throw new ArgumentException($"Repository with ID {repositoryId} not found");
        }

        // Make sure the configuration has a valid Id
        if (configuration.Id == Guid.Empty)
        {
            configuration.Id = Guid.NewGuid();
            configuration.CreatedAt = DateTime.UtcNow;
            configuration.UpdatedAt = DateTime.UtcNow;
            
            await _dataStore.AddConfigurationAsync(configuration);
        }
        else
        {
            // Ensure we update the existing configuration
            var existingConfig = await _dataStore.GetConfigurationAsync(configuration.Id);
            if (existingConfig == null)
            {
                // If not found, create a new one
                await _dataStore.AddConfigurationAsync(configuration);
            }
            else
            {
                // Update the existing one
                configuration.UpdatedAt = DateTime.UtcNow;
                await _dataStore.UpdateConfigurationAsync(configuration);
            }
        }

        // Create scan progress with default values
        var scanProgress = new ScanProgress
        {
            Id = Guid.NewGuid(),
            RepositoryId = repositoryId,
            ConfigurationId = configuration.Id,
            Status = ScanStatus.Initializing,
            TotalFiles = 0,
            ScannedFiles = 0,
            IssuesFound = 0,
            StartedAt = DateTime.UtcNow,
            IssuesByCategory = new Dictionary<IssueCategory, int>(),
            IssuesBySeverity = new Dictionary<IssueSeverity, int>()
        };

        await _dataStore.AddScanAsync(scanProgress);

        // In a real application, we would start the scan in the background
        // For this demo, we'll simulate scan progress
        _ = Task.Run(() => SimulateScanProgressAsync(scanProgress, configuration));

        _logger.LogInformation("Scan started for repository {RepositoryName} with ID {RepositoryId}", repository.Name, repository.Id);
        return scanProgress;
    }

    public async Task<ScanProgress> GetScanProgressAsync(Guid scanId)
    {
        var scanProgress = await _dataStore.GetScanAsync(scanId);
        if (scanProgress == null)
        {
            throw new ArgumentException($"Scan with ID {scanId} not found");
        }
        return scanProgress;
    }

    public async Task<IEnumerable<ScanProgress>> GetScansByRepositoryAsync(Guid repositoryId)
    {
        return await _dataStore.GetScansByRepositoryAsync(repositoryId);
    }

    public async Task<bool> PauseScanAsync(Guid scanId)
    {
        var scanProgress = await _dataStore.GetScanAsync(scanId);
        if (scanProgress == null)
        {
            return false;
        }

        if (scanProgress.Status != ScanStatus.InProgress)
        {
            return false;
        }

        scanProgress.Status = ScanStatus.Paused;
        await _dataStore.UpdateScanAsync(scanProgress);
        
        _logger.LogInformation("Scan {ScanId} paused", scanId);
        return true;
    }

    public async Task<bool> ResumeScanAsync(Guid scanId)
    {
        var scanProgress = await _dataStore.GetScanAsync(scanId);
        if (scanProgress == null)
        {
            return false;
        }

        if (scanProgress.Status != ScanStatus.Paused)
        {
            return false;
        }

        scanProgress.Status = ScanStatus.InProgress;
        await _dataStore.UpdateScanAsync(scanProgress);
        
        // Resume scan in background (in a real app)
        // For this demo, we'll just log it
        _logger.LogInformation("Scan {ScanId} resumed", scanId);
        return true;
    }

    public async Task<bool> CancelScanAsync(Guid scanId)
    {
        var scanProgress = await _dataStore.GetScanAsync(scanId);
        if (scanProgress == null)
        {
            return false;
        }

        if (scanProgress.Status != ScanStatus.InProgress && scanProgress.Status != ScanStatus.Paused)
        {
            return false;
        }

        scanProgress.Status = ScanStatus.Canceled;
        scanProgress.CompletedAt = DateTime.UtcNow;
        
        await _dataStore.UpdateScanAsync(scanProgress);
        
        _logger.LogInformation("Scan {ScanId} canceled", scanId);
        return true;
    }

    public async Task<bool> DeleteScanAsync(Guid scanId)
    {
        var scanProgress = await _dataStore.GetScanAsync(scanId);
        if (scanProgress == null)
        {
            _logger.LogWarning("Attempt to delete non-existent scan {ScanId}", scanId);
            return false;
        }

        // Prevent deletion of active scans
        if (scanProgress.Status == ScanStatus.InProgress || scanProgress.Status == ScanStatus.Initializing)
        {
            _logger.LogWarning("Cannot delete scan {ScanId} - scan is currently {Status}", scanId, scanProgress.Status);
            return false;
        }

        // Delete the scan and all its related issues
        var result = await _dataStore.DeleteScanWithIssuesAsync(scanId);
        
        if (result)
        {
            _logger.LogInformation("Successfully deleted scan {ScanId} for repository {RepositoryId}", 
                scanId, scanProgress.RepositoryId);
        }
        else
        {
            _logger.LogError("Failed to delete scan {ScanId}", scanId);
        }
        
        return result;
    }

    public async Task<IEnumerable<CodeIssue>> GetScanIssuesAsync(Guid scanId)
    {
        return await _dataStore.GetIssuesByScanAsync(scanId);
    }

    public async Task<IEnumerable<ScanConfiguration>> GetScanConfigurationsAsync(Guid repositoryId)
    {
        return await _dataStore.GetConfigurationsByRepositoryAsync(repositoryId);
    }

    public async Task<ScanConfiguration?> GetScanConfigurationByIdAsync(Guid configurationId)
    {
        return await _dataStore.GetConfigurationAsync(configurationId);
    }

    public async Task<ScanConfiguration> CreateScanConfigurationAsync(ScanConfiguration configuration)
    {
        if (configuration.Id == Guid.Empty)
        {
            configuration.Id = Guid.NewGuid();
        }

        configuration.CreatedAt = DateTime.UtcNow;
        configuration.UpdatedAt = DateTime.UtcNow;

        var addedConfig = await _dataStore.AddConfigurationAsync(configuration);

        _logger.LogInformation("Scan configuration created for repository {RepositoryId}", configuration.RepositoryId);
        return addedConfig;
    }

    public async Task<ScanConfiguration> UpdateScanConfigurationAsync(ScanConfiguration configuration)
    {
        var existingConfig = await _dataStore.GetConfigurationAsync(configuration.Id);
        if (existingConfig == null)
        {
            throw new ArgumentException($"Configuration with ID {configuration.Id} not found");
        }

        configuration.UpdatedAt = DateTime.UtcNow;
        
        var updatedConfig = await _dataStore.UpdateConfigurationAsync(configuration);

        _logger.LogInformation("Scan configuration {ConfigurationId} updated", configuration.Id);
        return updatedConfig;
    }

    public async Task<bool> DeleteScanConfigurationAsync(Guid configurationId)
    {
        var result = await _dataStore.DeleteConfigurationAsync(configurationId);
        if (result)
        {
            _logger.LogInformation("Scan configuration {ConfigurationId} deleted", configurationId);
        }
        
        return result;
    }

    private async Task SimulateScanProgressAsync(ScanProgress scanProgress, ScanConfiguration configuration)
    {
        try
        {
            var repository = await _repositoryService.GetRepositoryByIdAsync(scanProgress.RepositoryId);
            if (repository == null)
            {
                scanProgress.Status = ScanStatus.Failed;
                scanProgress.ErrorMessage = "Repository not found";
                scanProgress.CompletedAt = DateTime.UtcNow;
                await _dataStore.UpdateScanAsync(scanProgress);
                return;
            }

            // Get repository structure
            RepositoryFile? repoStructure = null;
            try
            {
                repoStructure = await _repositoryService.GetRepositoryStructureAsync(scanProgress.RepositoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repository structure for repository {RepositoryId}", scanProgress.RepositoryId);
                repoStructure = new RepositoryFile
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = scanProgress.RepositoryId,
                    Name = repository.Name,
                    Path = repository.Location,
                    IsDirectory = true,
                    Children = new List<RepositoryFile>()
                };
            }

            // Collect all files to scan based on configuration
            var filesToScan = CollectFilesToScanAsync(repoStructure, configuration);
            
            // Update total files count
            scanProgress.TotalFiles = filesToScan.Count;
            scanProgress.Status = ScanStatus.InProgress;
            await _dataStore.UpdateScanAsync(scanProgress);

            _logger.LogInformation("Starting scan of {FileCount} files for repository {RepositoryId}", 
                filesToScan.Count, scanProgress.RepositoryId);

            // If there are no files to scan, complete immediately
            if (filesToScan.Count == 0)
            {
                scanProgress.Status = ScanStatus.Completed;
                scanProgress.CompletedAt = DateTime.UtcNow;
                scanProgress.CurrentFile = null;
                scanProgress.EstimatedRemainingSeconds = 0;
                await _dataStore.UpdateScanAsync(scanProgress);

                // Update repository last scan time
                if (repository != null)
                {
                    repository.LastScanAt = DateTime.UtcNow;
                    await _repositoryService.UpdateRepositoryAsync(repository);
                }

                _logger.LogInformation("Scan {ScanId} completed for repository {RepositoryId} with 0 issues found (no files to scan)", 
                    scanProgress.Id, scanProgress.RepositoryId);
                
                return;
            }

            // Use semaphore to limit concurrent file analysis
            using var semaphore = new SemaphoreSlim(configuration.MaxConcurrentTasks);
            var tasks = new List<Task>();
            var fileIndex = 0;

            // Scan files in batches with limited concurrency
            foreach (var file in filesToScan)
            {
                // Check if the scan was canceled or paused
                var latestStatus = await _dataStore.GetScanAsync(scanProgress.Id);
                if (latestStatus?.Status == ScanStatus.Canceled)
                {
                    _logger.LogInformation("Scan {ScanId} was canceled, stopping scanning", scanProgress.Id);
                    break;
                }

                if (latestStatus?.Status == ScanStatus.Paused)
                {
                    // Wait until scan is resumed
                    _logger.LogInformation("Scan {ScanId} is paused, waiting for resume", scanProgress.Id);
                    while (true)
                    {
                        await Task.Delay(1000);
                        var currentStatus = await _dataStore.GetScanAsync(scanProgress.Id);
                        
                        // If resumed, break and continue scanning
                        if (currentStatus?.Status == ScanStatus.InProgress)
                        {
                            _logger.LogInformation("Scan {ScanId} resumed, continuing scanning", scanProgress.Id);
                            break;
                        }
                        
                        // If canceled while paused, exit
                        if (currentStatus?.Status == ScanStatus.Canceled)
                        {
                            _logger.LogInformation("Scan {ScanId} was canceled while paused, stopping scanning", scanProgress.Id);
                            return;
                        }
                    }
                }

                await semaphore.WaitAsync();
                
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var currentIndex = Interlocked.Increment(ref fileIndex);
                        
                        // Update progress
                        scanProgress.ScannedFiles = currentIndex;
                        scanProgress.CurrentFile = file.Path;
                        
                        // Calculate estimated time remaining based on average time per file so far
                        if (scanProgress.StartedAt.HasValue)
                        {
                            var elapsedSeconds = (DateTime.UtcNow - scanProgress.StartedAt.Value).TotalSeconds;
                            var secondsPerFile = currentIndex > 0 ? elapsedSeconds / currentIndex : 0;
                            scanProgress.EstimatedRemainingSeconds = (scanProgress.TotalFiles - currentIndex) * secondsPerFile;
                        }
                        
                        await _dataStore.UpdateScanAsync(scanProgress);
                        
                        // Analyze file
                        try
                        {
                            string fileContent = "";
                            try
                            {
                                fileContent = await _repositoryService.GetFileContentAsync(scanProgress.RepositoryId, file.Path);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error getting content for file {FilePath}", file.Path);
                                fileContent = "// Error getting file content\n// This is a placeholder for scanning purposes";
                            }
                            
                            var fileIssues = await AnalyzeFileAsync(file, fileContent, configuration, scanProgress.Id);
                            
                            if (fileIssues.Any())
                            {
                                foreach (var issue in fileIssues)
                                {
                                    // Add issue to database
                                    await _dataStore.AddIssueAsync(issue);
                                    
                                    // Track issue in scan progress
                                    lock (scanProgress)
                                    {
                                        scanProgress.IssuesFound++;
                                        
                                        // Update issues by category
                                        if (!scanProgress.IssuesByCategory.ContainsKey(issue.Category))
                                        {
                                            scanProgress.IssuesByCategory[issue.Category] = 0;
                                        }
                                        scanProgress.IssuesByCategory[issue.Category]++;
                                        
                                        // Update issues by severity
                                        if (!scanProgress.IssuesBySeverity.ContainsKey(issue.Severity))
                                        {
                                            scanProgress.IssuesBySeverity[issue.Severity] = 0;
                                        }
                                        scanProgress.IssuesBySeverity[issue.Severity]++;
                                    }
                                }
                                
                                // Update scan progress in database with new issue counts
                                await _dataStore.UpdateScanAsync(scanProgress);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error analyzing file {FilePath}", file.Path);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                
                tasks.Add(task);
                
                // For large number of files, avoid creating too many tasks at once
                if (tasks.Count > configuration.MaxConcurrentTasks * 5)
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCompleted);
                }
            }
            
            // Wait for all remaining tasks to complete
            await Task.WhenAll(tasks);

            // Mark scan as completed
            scanProgress.Status = ScanStatus.Completed;
            scanProgress.CompletedAt = DateTime.UtcNow;
            scanProgress.CurrentFile = null;
            scanProgress.EstimatedRemainingSeconds = 0;
            await _dataStore.UpdateScanAsync(scanProgress);

            // Update repository last scan time
            if (repository != null)
            {
                repository.LastScanAt = DateTime.UtcNow;
                await _repositoryService.UpdateRepositoryAsync(repository);
            }

            _logger.LogInformation("Scan {ScanId} completed for repository {RepositoryId} with {IssueCount} issues found", 
                scanProgress.Id, scanProgress.RepositoryId, scanProgress.IssuesFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scan {ScanId}", scanProgress.Id);
            
            scanProgress.Status = ScanStatus.Failed;
            scanProgress.ErrorMessage = ex.Message;
            scanProgress.CompletedAt = DateTime.UtcNow;
            await _dataStore.UpdateScanAsync(scanProgress);
        }
    }
    
    /// <summary>
    /// Convert a file path to a relative path from repository root
    /// </summary>
    private async Task<string> GetRelativeFilePath(string fullPath, Guid repositoryId)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return fullPath;
        }
        
        try
        {
            // Get the repository to find the root path
            var repository = await _repositoryService.GetRepositoryByIdAsync(repositoryId);
            if (repository == null || string.IsNullOrWhiteSpace(repository.Location))
            {
                return fullPath;
            }
            
            var rootPath = repository.Location.TrimEnd('/', '\\');
            var normalizedFullPath = fullPath.Replace('\\', '/');
            var normalizedRootPath = rootPath.Replace('\\', '/');
            
            // Remove the repository root path from the file path
            if (normalizedFullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = normalizedFullPath.Substring(normalizedRootPath.Length).TrimStart('/', '\\');
                return string.IsNullOrWhiteSpace(relativePath) ? fullPath : relativePath;
            }
            
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting relative path for {FilePath}", fullPath);
            return fullPath;
        }
    }
    
    /// <summary>
    /// Collect all files to scan based on configuration
    /// </summary>
    private List<RepositoryFile> CollectFilesToScanAsync(RepositoryFile repoStructure, ScanConfiguration configuration)
    {
        var result = new List<RepositoryFile>();
        if (repoStructure != null)
        {
            CollectFilesRecursiveAsync(repoStructure, configuration, result);
        }
        
        // If no files were found, add at least one placeholder file for testing
        if (result.Count == 0 && repoStructure != null)
        {
            _logger.LogWarning("No files found to scan in repository {RepositoryId}. Adding placeholder file for testing.", repoStructure.RepositoryId);
            result.Add(new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repoStructure.RepositoryId,
                Name = "placeholder.cs",
                Path = "src/placeholder.cs",
                Extension = ".cs",
                SizeInBytes = 100,
                LastModified = DateTime.UtcNow,
                IsDirectory = false
            });
        }
        
        return result;
    }
    
    /// <summary>
    /// Recursively collect files that match the scan configuration
    /// </summary>
    private void CollectFilesRecursiveAsync(RepositoryFile current, ScanConfiguration configuration, List<RepositoryFile> result)
    {
    // Skip if null or not accessible
    if (current == null || current.Children == null)
    {
    _logger.LogWarning("Skipping null or inaccessible directory in CollectFilesRecursiveAsync");
        return;
        }
        
        foreach (var child in current.Children)
        {
            // For directories, check if we should include it based on configuration
            if (child.IsDirectory)
            {
                var dirName = Path.GetFileName(child.Path);
                
                // Skip common directories to ignore
                if (dirName == ".git" || dirName == "node_modules" || dirName == "bin" || dirName == "obj")
                {
                    continue;
                }
                
                // Skip directories in exclude list
                if (configuration.ExcludeDirectories.Any(excludeDir => 
                    child.Path.EndsWith(excludeDir, StringComparison.OrdinalIgnoreCase) ||
                    child.Path.Contains(Path.DirectorySeparatorChar + excludeDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                
                // Include specific directories if specified
                var shouldInclude = configuration.IncludeDirectories.Count == 0 || // Include all if no specific includes
                                    configuration.IncludeDirectories.Any(includeDir => 
                                        child.Path.EndsWith(includeDir, StringComparison.OrdinalIgnoreCase) ||
                                        child.Path.Contains(Path.DirectorySeparatorChar + includeDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                
                if (shouldInclude)
                {
                    CollectFilesRecursiveAsync(child, configuration, result);
                }
            }
            // For files, check if we should include it based on configuration
            else
            {
                var extension = Path.GetExtension(child.Path);
                
                // Skip files that are too large
                if (child.SizeInBytes > configuration.MaxFileSizeBytes)
                {
                    continue;
                }
                
                // Skip files with excluded extensions
                if (configuration.ExcludeExtensions.Any(ext => 
                    extension.Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                    (ext.StartsWith(".") && extension.Equals(ext, StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }
                
                // Include files with specific extensions if specified
                var shouldInclude = configuration.IncludeExtensions.Count == 0 || // Include all if no specific includes
                                    configuration.IncludeExtensions.Any(ext => 
                                        extension.Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                                        (ext.StartsWith(".") && extension.Equals(ext, StringComparison.OrdinalIgnoreCase)));
                
                if (shouldInclude)
                {
                    result.Add(child);
                }
            }
        }
    }
    
    /// <summary>
    /// Analyze a file to find code issues using the configured LLM provider
    /// </summary>
    private async Task<IEnumerable<CodeIssue>> AnalyzeFileAsync(RepositoryFile file, string content, ScanConfiguration configuration, Guid scanId)
    {
        var issues = new List<CodeIssue>();
        
        // Safety check for file
        if (file == null)
        {
            _logger.LogWarning("Null file passed to AnalyzeFileAsync");
            return issues;
        }
        
        // Skip empty files
        if (string.IsNullOrWhiteSpace(content))
        {
            return issues;
        }
        
        try
        {

            // Check if LLM provider is available
            if (_llmProvider == null || !await _llmProvider.IsAvailableAsync())
            {
                _logger.LogWarning("LLM provider is not available for file {FilePath}", file.Path);
                return await Task.FromResult(Enumerable.Empty<CodeIssue>());
            }
            
            _logger.LogDebug("Starting LLM analysis for file {FilePath}", file.Path);
            
            // Analyze file using LLM provider
            var detectedIssues = await _llmProvider.AnalyzeCodeAsync(content, file.Path, configuration, scanId);
            
            // Convert to list and ensure all issues have correct metadata
            var issuesList = detectedIssues.ToList();
            foreach (var issue in issuesList)
            {
                // Ensure issue has correct scan and repository IDs
                issue.ScanId = scanId;
                issue.RepositoryId = file.RepositoryId;
                // Store relative path instead of full path
                issue.FilePath = await GetRelativeFilePath(file.Path, file.RepositoryId);
                issue.DetectedAt = DateTime.UtcNow;
                
                // Set detection method to AI if not already set
                if (issue.DetectionMethod == default)
                {
                    issue.DetectionMethod = IssueDetectionMethod.AiAnalysis;
                }
            }
            
            // Filter issues by minimum severity
            var filteredIssues = issuesList.Where(issue => issue.Severity >= configuration.MinimumSeverity).ToList();
            
            // Generate suggested fixes if enabled and issues were found
            if (configuration.SuggestFixes && filteredIssues.Any())
            {
                _logger.LogDebug("Generating fix suggestions for {IssueCount} issues in file {FilePath}", 
                    filteredIssues.Count, file.Path);
                
                foreach (var issue in filteredIssues)
                {
                    // Only generate fix if not already provided by the LLM
                    if (string.IsNullOrWhiteSpace(issue.SuggestedFix))
                    {
                        try
                        {
                            issue.SuggestedFix = await _llmProvider.GenerateFixSuggestionAsync(issue, content);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to generate fix suggestion for issue {IssueId} in file {FilePath}", 
                                issue.Id, file.Path);
                            // Keep the issue but without a suggested fix
                        }
                    }
                }
            }
            
            issues.AddRange(filteredIssues);
            
            _logger.LogDebug("Completed LLM analysis for file {FilePath}, found {IssueCount} issues", 
                file.Path, filteredIssues.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LLM analysis of file {FilePath}", file.Path);
        }
        
        return issues;
    }
}