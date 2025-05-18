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

    public ScanningService(
        ILogger<ScanningService> logger,
        IRepositoryService repositoryService,
        JsonDataStore dataStore)
    {
        _logger = logger;
        _repositoryService = repositoryService;
        _dataStore = dataStore;
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
    /// Analyze a file to find code issues
    /// </summary>
    private async Task<List<CodeIssue>> AnalyzeFileAsync(RepositoryFile file, string content, ScanConfiguration configuration, Guid scanId)
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
        
        // Prepare for analysis
        var lines = content.Split('\n');
        var extension = Path.GetExtension(file.Path).ToLowerInvariant();
        
        // Create a list to collect issues
        var detectedIssues = new List<CodeIssue>();
        
        // For large files, do some async work to avoid blocking the thread
        if (content.Length > 100000) // For files larger than ~100KB
        {
            await Task.Delay(1); // Minimal delay to make this truly async
        }
        
        // Perform basic analysis based on file type
        if (extension == ".cs" || extension == ".java" || extension == ".js" || extension == ".ts")
        {
            // Check for code quality issues
            if (configuration.IncludeCategories.Contains(IssueCategory.Quality))
            {
                detectedIssues.AddRange(AnalyzeCodeQuality(file, lines, scanId));
            }
            
            // Check for security issues
            if (configuration.IncludeCategories.Contains(IssueCategory.Security))
            {
                detectedIssues.AddRange(AnalyzeSecurityIssues(file, lines, scanId));
            }
        }
        else if (extension == ".csproj" || extension == ".vbproj" || extension == ".fsproj" || extension == ".xml")
        {
            // Check for dependency issues
            if (configuration.IncludeCategories.Contains(IssueCategory.Dependency))
            {
                detectedIssues.AddRange(AnalyzeDependencyIssues(file, lines, scanId));
            }
        }
        
        // Filter issues by minimum severity
        var filteredIssues = detectedIssues.Where(issue => issue.Severity >= configuration.MinimumSeverity).ToList();
        
        // Generate suggested fixes if enabled
        if (configuration.SuggestFixes && configuration.UseAi && filteredIssues.Any())
        {
            // This would use AI to suggest fixes, but for now we'll just add placeholder suggestions
            foreach (var issue in filteredIssues)
            {
                issue.SuggestedFix = $"// Suggested fix for {issue.Title}\n// Would be generated by AI";
            }
        }
        
        return filteredIssues;
    }
    
    /// <summary>
    /// Analyze code quality issues
    /// </summary>
    private List<CodeIssue> AnalyzeCodeQuality(RepositoryFile file, string[] lines, Guid scanId)
    {
        var issues = new List<CodeIssue>();
        
        // Check for long methods (> 50 lines)
        int methodStartLine = -1;
        int braceCount = 0;
        string methodName = "";
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Look for method declarations
            if ((line.Contains("void ") || line.Contains("async ") || line.Contains("Task ") || 
                 line.Contains("string ") || line.Contains("int ") || line.Contains("bool ")) && 
                line.Contains("(") && !line.Contains(";") && methodStartLine == -1)
            {
                methodStartLine = i;
                methodName = line.Contains("(") ? line.Substring(0, line.IndexOf('(')).Trim().Split(' ').Last() : "Unknown";
            }
            
            // Count braces to detect method boundaries
            if (line.Contains("{"))
            {
                braceCount++;
            }
            
            if (line.Contains("}"))
            {
                braceCount--;
                
                // Method ended, check if it was too long
                if (braceCount == 0 && methodStartLine != -1)
                {
                    int methodLength = i - methodStartLine + 1;
                    if (methodLength > 50)
                    {
                        issues.Add(new CodeIssue
                        {
                            Id = Guid.NewGuid(),
                            ScanId = scanId,
                            RepositoryId = file.RepositoryId,
                            FilePath = file.Path,
                            LineNumber = methodStartLine + 1, // 1-based for display
                            ColumnNumber = 1,
                            LineSpan = methodLength,
                            Title = $"Method '{methodName}' is too long ({methodLength} lines)",
                            Description = $"Long methods are harder to understand and maintain. Consider refactoring '{methodName}' into smaller, focused methods.",
                            Severity = IssueSeverity.Medium,
                            Category = IssueCategory.Quality,
                            Confidence = 0.9,
                            CodeSnippet = string.Join("\n", lines.Skip(methodStartLine).Take(Math.Min(15, methodLength))),
                            DetectionMethod = IssueDetectionMethod.Static,
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    
                    methodStartLine = -1;
                    methodName = "";
                }
            }
            
            // Check for magic numbers
            if (line.Contains("= ") && !line.Contains("==") && !line.Contains(">=") && !line.Contains("<="))
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length > 1)
                {
                    var value = parts[1].Trim().TrimEnd(';', ')');
                    if (int.TryParse(value, out int number) && number != 0 && number != 1 && number != -1)
                    {
                        issues.Add(new CodeIssue
                        {
                            Id = Guid.NewGuid(),
                            ScanId = scanId,
                            RepositoryId = file.RepositoryId,
                            FilePath = file.Path,
                            LineNumber = i + 1, // 1-based for display
                            ColumnNumber = line.IndexOf('=') + 1,
                            LineSpan = 1,
                            Title = $"Magic number detected: {number}",
                            Description = $"Using unnamed magic numbers makes code harder to understand and maintain. Consider defining a named constant instead of '{number}'.",
                            Severity = IssueSeverity.Low,
                            Category = IssueCategory.Quality,
                            Confidence = 0.7,
                            CodeSnippet = line,
                            DetectionMethod = IssueDetectionMethod.Static,
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        
        return issues;
    }
    
    /// <summary>
    /// Analyze security issues
    /// </summary>
    private List<CodeIssue> AnalyzeSecurityIssues(RepositoryFile file, string[] lines, Guid scanId)
    {
        var issues = new List<CodeIssue>();
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Check for hard-coded credentials
            if ((line.Contains("password") || line.Contains("passwd") || line.Contains("pwd") || 
                 line.Contains("secret") || line.Contains("token")) && 
                line.Contains("=") && (line.Contains("\"") || line.Contains("'")))
            {
                issues.Add(new CodeIssue
                {
                    Id = Guid.NewGuid(),
                    ScanId = scanId,
                    RepositoryId = file.RepositoryId,
                    FilePath = file.Path,
                    LineNumber = i + 1, // 1-based for display
                    ColumnNumber = 1,
                    LineSpan = 1,
                    Title = "Possible hard-coded credentials",
                    Description = "Hard-coded credentials are a security risk. Consider using environment variables, a secure credential store, or a secret management service.",
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Security,
                    Confidence = 0.8,
                    CodeSnippet = line,
                    DetectionMethod = IssueDetectionMethod.Static,
                    DetectedAt = DateTime.UtcNow
                });
            }
            
            // Check for SQL injection
            if ((line.Contains("ExecuteQuery") || line.Contains("ExecuteSql") || line.Contains("SqlCommand")) && 
                line.Contains("+") && !line.Contains("Parameters"))
            {
                issues.Add(new CodeIssue
                {
                    Id = Guid.NewGuid(),
                    ScanId = scanId,
                    RepositoryId = file.RepositoryId,
                    FilePath = file.Path,
                    LineNumber = i + 1, // 1-based for display
                    ColumnNumber = 1,
                    LineSpan = 1,
                    Title = "Potential SQL injection vulnerability",
                    Description = "String concatenation in SQL queries can lead to SQL injection vulnerabilities. Use parameterized queries instead.",
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Confidence = 0.85,
                    CodeSnippet = line,
                    DetectionMethod = IssueDetectionMethod.Static,
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        
        return issues;
    }
    
    /// <summary>
    /// Analyze dependency issues
    /// </summary>
    private List<CodeIssue> AnalyzeDependencyIssues(RepositoryFile file, string[] lines, Guid scanId)
    {
        var issues = new List<CodeIssue>();
        
        // Look for old package references
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            if (line.Contains("<PackageReference") && line.Contains("Version="))
            {
                // Extract package name and version
                string packageName = "";
                string version = "";
                
                var includeMatch = System.Text.RegularExpressions.Regex.Match(line, "Include=\"([^\"]+)\"");
                if (includeMatch.Success)
                {
                    packageName = includeMatch.Groups[1].Value;
                }
                
                var versionMatch = System.Text.RegularExpressions.Regex.Match(line, "Version=\"([^\"]+)\"");
                if (versionMatch.Success)
                {
                    version = versionMatch.Groups[1].Value;
                }
                
                // Flag old versions of Microsoft packages
                if (!string.IsNullOrEmpty(packageName) && !string.IsNullOrEmpty(version) && 
                    packageName.StartsWith("Microsoft.") && version.StartsWith("1.") && !version.StartsWith("1.9"))
                {
                    issues.Add(new CodeIssue
                    {
                        Id = Guid.NewGuid(),
                        ScanId = scanId,
                        RepositoryId = file.RepositoryId,
                        FilePath = file.Path,
                        LineNumber = i + 1, // 1-based for display
                        ColumnNumber = 1,
                        LineSpan = 1,
                        Title = $"Outdated package reference: {packageName} {version}",
                        Description = $"The package {packageName} is using an outdated version ({version}). Consider updating to a newer version for bug fixes, security updates, and improved features.",
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.Dependency,
                        Confidence = 0.9,
                        CodeSnippet = line,
                        DetectionMethod = IssueDetectionMethod.Static,
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
        }
        
        return issues;
    }

    /// <summary>
    /// Generates a random issue for testing purposes
    /// </summary>
    private CodeIssue GenerateRandomIssue(Guid repositoryId, Guid scanId)
    {
        var random = new Random();
        var categories = Enum.GetValues<IssueCategory>();
        var severities = Enum.GetValues<IssueSeverity>();
        var detectionMethods = Enum.GetValues<IssueDetectionMethod>();

        var category = categories[random.Next(categories.Length)];
        var severity = severities[random.Next(severities.Length)];
        var method = detectionMethods[random.Next(detectionMethods.Length)];

        return new CodeIssue
        {
            Id = Guid.NewGuid(),
            ScanId = scanId,
            RepositoryId = repositoryId,
            FilePath = $"src/file{random.Next(1, 100)}.cs",
            LineNumber = random.Next(1, 500),
            ColumnNumber = random.Next(1, 80),
            LineSpan = random.Next(1, 10),
            Title = GetRandomIssueTitle(category),
            Description = "This is a simulated issue description for testing purposes.",
            Severity = severity,
            Category = category,
            Confidence = random.NextDouble() * 0.5 + 0.5, // 0.5 to 1.0
            CodeSnippet = "// Code snippet would be shown here",
            SuggestedFix = "// Suggested fix would be shown here",
            DetectionMethod = method,
            DetectedAt = DateTime.UtcNow
        };
    }

    private string GetRandomIssueTitle(IssueCategory category)
    {
        var random = new Random();
        
        switch (category)
        {
            case IssueCategory.Security:
                var securityTitles = new[]
                {
                    "Potential SQL Injection vulnerability",
                    "Insecure cryptographic algorithm usage",
                    "Cross-site scripting (XSS) vulnerability",
                    "Hard-coded credentials",
                    "Insecure random number generation"
                };
                return securityTitles[random.Next(securityTitles.Length)];
                
            case IssueCategory.Performance:
                var performanceTitles = new[]
                {
                    "Inefficient loop implementation",
                    "Excessive memory allocation",
                    "Redundant computation in hot path",
                    "Unnecessary object creation",
                    "Inefficient string concatenation"
                };
                return performanceTitles[random.Next(performanceTitles.Length)];
                
            case IssueCategory.Quality:
                var qualityTitles = new[]
                {
                    "Method is too complex (high cyclomatic complexity)",
                    "Class has too many responsibilities",
                    "Duplicated code block",
                    "Magic number usage",
                    "Inconsistent naming convention"
                };
                return qualityTitles[random.Next(qualityTitles.Length)];
                
            case IssueCategory.Architecture:
                var architectureTitles = new[]
                {
                    "Circular dependency detected",
                    "Violation of dependency inversion principle",
                    "Service has too many dependencies",
                    "Excessive use of static methods",
                    "Interface segregation principle violation"
                };
                return architectureTitles[random.Next(architectureTitles.Length)];
                
            case IssueCategory.Dependency:
                var dependencyTitles = new[]
                {
                    "Outdated package with security vulnerabilities",
                    "Conflicting package versions",
                    "Multiple packages with similar functionality",
                    "Unused dependency",
                    "Transitive dependency with potential license issue"
                };
                return dependencyTitles[random.Next(dependencyTitles.Length)];
                
            case IssueCategory.Testing:
                var testingTitles = new[]
                {
                    "Insufficient test coverage",
                    "Test depends on external resources",
                    "Flaky test detected",
                    "Test logic is too complex",
                    "Missing assertion in test"
                };
                return testingTitles[random.Next(testingTitles.Length)];
                
            case IssueCategory.Documentation:
                var documentationTitles = new[]
                {
                    "Missing XML documentation for public API",
                    "Outdated documentation",
                    "Documentation doesn't match implementation",
                    "Unclear parameter description",
                    "Missing exception documentation"
                };
                return documentationTitles[random.Next(documentationTitles.Length)];
                
            case IssueCategory.Accessibility:
                var accessibilityTitles = new[]
                {
                    "Missing ARIA attributes",
                    "Insufficient color contrast",
                    "No alt text for image",
                    "Keyboard navigation not supported",
                    "Missing screen reader support"
                };
                return accessibilityTitles[random.Next(accessibilityTitles.Length)];
                
            default:
                var otherTitles = new[]
                {
                    "Potential issue detected",
                    "Code smell detected",
                    "Unusual pattern usage",
                    "Possible bug detected",
                    "Optimization opportunity"
                };
                return otherTitles[random.Next(otherTitles.Length)];
        }
    }
}