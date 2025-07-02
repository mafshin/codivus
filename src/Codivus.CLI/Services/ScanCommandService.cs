using System.Diagnostics;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class ScanCommandService
{
    private readonly IRepositoryService _repositoryService;
    private readonly IScanningService _scanningService;
    private readonly IOutputService _outputService;
    private readonly IValidationService _validationService;
    private readonly ILogger<ScanCommandService> _logger;

    public ScanCommandService(
        IRepositoryService repositoryService,
        IScanningService scanningService,
        IOutputService outputService,
        IValidationService validationService,
        ILogger<ScanCommandService> logger)
    {
        _repositoryService = repositoryService;
        _scanningService = scanningService;
        _outputService = outputService;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<CommandResult<ScanStartResult>> StartScanAsync(ScanOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting scan for repository: {RepositoryId}", options.RepositoryId);

            // Resolve repository
            var repository = await ResolveRepositoryAsync(options.RepositoryId!);
            if (repository == null)
            {
                return CommandResult<ScanStartResult>.ErrorResult($"Repository '{options.RepositoryId}' not found");
            }

            // Create scan configuration
            var scanConfig = CreateScanConfiguration(options, repository);

            // Start the scan using the backend scanning service
            var scanProgress = await _scanningService.StartScanAsync(repository.Id, scanConfig);

            stopwatch.Stop();

            var result = new ScanStartResult
            {
                ScanId = scanProgress.Id.ToString(),
                RepositoryId = repository.Id.ToString(),
                RepositoryName = repository.Name,
                Status = scanProgress.Status.ToString(),
                FilesTotal = scanProgress.TotalFiles,
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<ScanStartResult>.SuccessResult(
                result,
                $"Scan started successfully. Scan ID: {scanProgress.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting scan for repository: {RepositoryId}", options.RepositoryId);
            return CommandResult<ScanStartResult>.ErrorResult($"Failed to start scan: {ex.Message}");
        }
    }

    public async Task<CommandResult<ScanStatusResult>> GetScanStatusAsync(ScanOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            if (!string.IsNullOrEmpty(options.ScanId))
            {
                // Get status for specific scan
                var scanProgress = await _scanningService.GetScanProgressAsync(Guid.Parse(options.ScanId));
                if (scanProgress == null)
                {
                    return CommandResult<ScanStatusResult>.ErrorResult($"Scan '{options.ScanId}' not found");
                }

                stopwatch.Stop();

                var result = new ScanStatusResult
                {
                    Scans = new List<Models.ScanInfo>
                    {
                        new Models.ScanInfo
                        {
                            ScanId = scanProgress.Id.ToString(),
                            RepositoryId = scanProgress.RepositoryId.ToString(),
                            Status = scanProgress.Status.ToString(),
                            Progress = CalculateProgress(scanProgress),
                            FilesProcessed = scanProgress.ScannedFiles,
                            FilesTotal = scanProgress.TotalFiles,
                            IssuesFound = scanProgress.IssuesFound,
                            CurrentFile = scanProgress.CurrentFile,
                            StartedAt = scanProgress.StartedAt ?? DateTime.UtcNow,
                            EstimatedCompletion = CalculateEstimatedCompletion(scanProgress)
                        }
                    },
                    Success = true,
                    Duration = stopwatch.Elapsed
                };

                return CommandResult<ScanStatusResult>.SuccessResult(result, "Scan status retrieved successfully");
            }
            else
            {
                // Get status for all scans or filtered by repository
                var scans = new List<Models.ScanInfo>();
                
                // TODO: Implement GetAllScansAsync in scanning service
                // For now, return empty list
                
                stopwatch.Stop();

                var result = new ScanStatusResult
                {
                    Scans = scans,
                    Success = true,
                    Duration = stopwatch.Elapsed
                };

                return CommandResult<ScanStatusResult>.SuccessResult(result, $"Found {scans.Count} scans");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan status");
            return CommandResult<ScanStatusResult>.ErrorResult($"Failed to get scan status: {ex.Message}");
        }
    }

    public async Task<CommandResult<ScanResultsResult>> GetScanResultsAsync(ScanOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Getting scan results for scan: {ScanId}", options.ScanId);

            var scanId = Guid.Parse(options.ScanId!);
            
            // Get scan progress to verify it exists
            var scanProgress = await _scanningService.GetScanProgressAsync(scanId);
            if (scanProgress == null)
            {
                return CommandResult<ScanResultsResult>.ErrorResult($"Scan '{options.ScanId}' not found");
            }

            // Get issues for the scan
            var issues = await _scanningService.GetScanIssuesAsync(scanId);

            // Apply filters
            var filteredIssues = issues.AsEnumerable();
            
            if (!string.IsNullOrEmpty(options.IssueType))
            {
                filteredIssues = filteredIssues.Where(i => 
                    string.Equals(i.Category.ToString(), options.IssueType, StringComparison.OrdinalIgnoreCase));
            }
            
            if (!string.IsNullOrEmpty(options.Severity))
            {
                filteredIssues = filteredIssues.Where(i => 
                    string.Equals(i.Severity.ToString(), options.Severity, StringComparison.OrdinalIgnoreCase));
            }

            var resultIssues = filteredIssues
                .Take(options.Limit)
                .Select(issue => new IssueInfo
                {
                    Id = issue.Id.ToString(),
                    Type = issue.Category.ToString(),
                    Severity = issue.Severity.ToString(),
                    Message = issue.Title,
                    Description = issue.Description,
                    File = issue.FilePath,
                    Line = issue.LineNumber,
                    Column = issue.ColumnNumber ?? 0,
                    ConfidenceScore = issue.Confidence,
                    Recommendations = ParseRecommendations(issue.SuggestedFix)
                })
                .ToList();

            stopwatch.Stop();

            var result = new ScanResultsResult
            {
                ScanId = options.ScanId,
                Issues = resultIssues,
                TotalIssues = issues.Count(),
                FilteredIssues = resultIssues.Count,
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<ScanResultsResult>.SuccessResult(
                result,
                $"Retrieved {resultIssues.Count} issues (filtered from {filteredIssues.Count()} total)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan results for scan: {ScanId}", options.ScanId);
            return CommandResult<ScanResultsResult>.ErrorResult($"Failed to get scan results: {ex.Message}");
        }
    }

    public async Task<CommandResult<ScanOperationResult>> PauseScanAsync(ScanOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Pausing scan: {ScanId}", options.ScanId);

            var scanId = Guid.Parse(options.ScanId!);
            await _scanningService.PauseScanAsync(scanId);

            stopwatch.Stop();

            var result = new ScanOperationResult
            {
                ScanId = options.ScanId,
                Operation = "Pause",
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<ScanOperationResult>.SuccessResult(result, $"Scan '{options.ScanId}' paused successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing scan: {ScanId}", options.ScanId);
            return CommandResult<ScanOperationResult>.ErrorResult($"Failed to pause scan: {ex.Message}");
        }
    }

    public async Task<CommandResult<ScanOperationResult>> ResumeScanAsync(ScanOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Resuming scan: {ScanId}", options.ScanId);

            var scanId = Guid.Parse(options.ScanId!);
            await _scanningService.ResumeScanAsync(scanId);

            stopwatch.Stop();

            var result = new ScanOperationResult
            {
                ScanId = options.ScanId,
                Operation = "Resume",
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<ScanOperationResult>.SuccessResult(result, $"Scan '{options.ScanId}' resumed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming scan: {ScanId}", options.ScanId);
            return CommandResult<ScanOperationResult>.ErrorResult($"Failed to resume scan: {ex.Message}");
        }
    }

    public async Task<CommandResult<ScanOperationResult>> CancelScanAsync(ScanOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Cancelling scan: {ScanId}", options.ScanId);

            var scanId = Guid.Parse(options.ScanId!);
            await _scanningService.CancelScanAsync(scanId);

            stopwatch.Stop();

            var result = new ScanOperationResult
            {
                ScanId = options.ScanId,
                Operation = "Cancel",
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<ScanOperationResult>.SuccessResult(result, $"Scan '{options.ScanId}' cancelled successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling scan: {ScanId}", options.ScanId);
            return CommandResult<ScanOperationResult>.ErrorResult($"Failed to cancel scan: {ex.Message}");
        }
    }

    public async Task<CommandResult<ScanListResult>> ListScansAsync(ScanOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Listing scans");

            // TODO: Implement GetAllScansAsync in scanning service
            // For now, return empty list
            var scans = new List<Models.ScanInfo>();

            stopwatch.Stop();

            var result = new ScanListResult
            {
                Scans = scans,
                TotalCount = scans.Count,
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<ScanListResult>.SuccessResult(result, $"Found {scans.Count} scans");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing scans");
            return CommandResult<ScanListResult>.ErrorResult($"Failed to list scans: {ex.Message}");
        }
    }

    private async Task<Repository?> ResolveRepositoryAsync(string repositoryId)
    {
        // Try to parse as GUID first
        if (Guid.TryParse(repositoryId, out var repoId))
        {
            return await _repositoryService.GetRepositoryByIdAsync(repoId);
        }

        // Search by name
        var repositories = await _repositoryService.GetAllRepositoriesAsync();
        return repositories.FirstOrDefault(r => 
            string.Equals(r.Name, repositoryId, StringComparison.OrdinalIgnoreCase));
    }

    private ScanConfiguration CreateScanConfiguration(ScanOptions options, Repository repository)
    {
        var config = new ScanConfiguration
        {
            Id = Guid.NewGuid(),
            Name = options.ConfigurationName ?? $"CLI-Scan-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            RepositoryId = repository.Id,
            Branch = options.Branch,
            MaxConcurrentTasks = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Convert file patterns to extensions
        if (options.FilePatterns != null && options.FilePatterns.Any())
        {
            config.IncludeExtensions = options.FilePatterns
                .Where(p => p.StartsWith("*."))
                .Select(p => p.Substring(1))
                .ToList();
        }

        // Convert exclude patterns
        if (options.ExcludePatterns != null && options.ExcludePatterns.Any())
        {
            var excludeExtensions = options.ExcludePatterns
                .Where(p => p.StartsWith("*."))
                .Select(p => p.Substring(1))
                .ToList();
            
            var excludeDirs = options.ExcludePatterns
                .Where(p => !p.StartsWith("*."))
                .ToList();

            config.ExcludeExtensions = excludeExtensions;
            config.ExcludeDirectories = excludeDirs;
        }

        // Set LLM provider if specified
        if (!string.IsNullOrEmpty(options.LLMProvider))
        {
            if (Enum.TryParse<LlmProviderType>(options.LLMProvider, true, out var provider))
            {
                config.LlmProvider = provider;
            }
        }

        if (!string.IsNullOrEmpty(options.Model))
        {
            config.LlmModel = options.Model;
        }

        return config;
    }

    private double CalculateProgress(ScanProgress scanProgress)
    {
        if (scanProgress.TotalFiles == 0) return 0;
        return (double)scanProgress.ScannedFiles / scanProgress.TotalFiles * 100;
    }

    private DateTime? CalculateEstimatedCompletion(ScanProgress scanProgress)
    {
        if (scanProgress.EstimatedRemainingSeconds.HasValue && scanProgress.EstimatedRemainingSeconds > 0)
        {
            return DateTime.UtcNow.AddSeconds(scanProgress.EstimatedRemainingSeconds.Value);
        }
        return null;
    }

    private List<string> ParseRecommendations(string? suggestedFix)
    {
        if (string.IsNullOrEmpty(suggestedFix))
            return new List<string>();

        // Simple parsing - could be enhanced
        return new List<string> { suggestedFix };
    }
}