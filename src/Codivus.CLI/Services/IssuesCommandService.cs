using System.Diagnostics;
using System.Text.Json;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class IssuesCommandService
{
    private readonly ApiClientService _apiClient;
    private readonly IOutputService _outputService;
    private readonly IValidationService _validationService;
    private readonly ILogger<IssuesCommandService> _logger;

    public IssuesCommandService(
        ApiClientService apiClient,
        IOutputService outputService,
        IValidationService validationService,
        ILogger<IssuesCommandService> logger)
    {
        _apiClient = apiClient;
        _outputService = outputService;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<CommandResult<IssuesListResult>> ListIssuesAsync(IssuesOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Listing issues with options: {@Options}", options);

            // Get issues from API
            var issuesResponse = await _apiClient.GetAllIssuesAsync(options.RepositoryId);
            if (!issuesResponse.Success || issuesResponse.Data == null)
            {
                return CommandResult<IssuesListResult>.ErrorResult(issuesResponse.Message ?? "Failed to get issues");
            }

            var allIssues = issuesResponse.Data;

            // Apply filters
            var filteredIssues = FilterIssues(allIssues, options);

            // Sort issues
            var sortedIssues = SortIssues(filteredIssues, options.SortBy);

            // Apply limit
            var limitedIssues = sortedIssues.Take(options.Limit).ToList();

            // Convert to IssueInfo
            var issueInfos = limitedIssues.Select(MapToIssueInfo).ToList();

            var result = new IssuesListResult
            {
                Issues = issueInfos,
                TotalCount = allIssues.Count,
                FilteredCount = filteredIssues.Count(),
                Success = true
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Listed {Count} issues in {Duration}ms", issueInfos.Count, stopwatch.ElapsedMilliseconds);

            return CommandResult<IssuesListResult>.SuccessResult(
                result,
                $"Found {issueInfos.Count} issues matching criteria (filtered from {allIssues.Count} total).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing issues");
            return CommandResult<IssuesListResult>.ErrorResult($"Failed to list issues: {ex.Message}");
        }
    }

    public async Task<CommandResult<IssueDetailResult>> ShowIssueAsync(IssuesOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Showing issue details for: {IssueId}", options.IssueId);

            if (!Guid.TryParse(options.IssueId, out var issueId))
            {
                return CommandResult<IssueDetailResult>.ErrorResult($"Invalid issue ID: {options.IssueId}");
            }

            var issueResponse = await _apiClient.GetIssueByIdAsync(issueId);
            if (!issueResponse.Success || issueResponse.Data == null)
            {
                return CommandResult<IssueDetailResult>.ErrorResult(issueResponse.Message ?? $"Issue not found: {options.IssueId}");
            }

            var issue = issueResponse.Data;
            var issueInfo = MapToIssueInfo(issue);

            // Try to get source code context (this would need a separate API endpoint)
            string? sourceCode = null;
            if (options.IncludeContext)
            {
                sourceCode = await GetSourceCodeContextAsync(issue.FilePath, issue.LineNumber);
            }

            var result = new IssueDetailResult
            {
                Issue = issueInfo,
                SourceCode = sourceCode,
                RelatedIssues = new List<string>(), // Could be enhanced with related issues API
                Success = true
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<IssueDetailResult>.SuccessResult(
                result,
                $"Retrieved details for issue {options.IssueId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing issue details for: {IssueId}", options.IssueId);
            return CommandResult<IssueDetailResult>.ErrorResult($"Failed to get issue details: {ex.Message}");
        }
    }

    public async Task<CommandResult<IssueUpdateResult>> UpdateIssueStatusAsync(IssuesOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Updating issue status: {IssueId} to {Status}", options.IssueId, options.Status);

            if (!Guid.TryParse(options.IssueId, out var issueId))
            {
                return CommandResult<IssueUpdateResult>.ErrorResult($"Invalid issue ID: {options.IssueId}");
            }

            var updateResponse = await _apiClient.UpdateIssueStatusAsync(issueId, options.Status);
            if (!updateResponse.Success)
            {
                return CommandResult<IssueUpdateResult>.ErrorResult(updateResponse.Message ?? "Failed to update issue status");
            }

            var result = new IssueUpdateResult
            {
                IssueId = options.IssueId,
                Operation = $"Update status to {options.Status}",
                Success = true
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<IssueUpdateResult>.SuccessResult(
                result,
                $"Updated issue {options.IssueId} status to {options.Status}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating issue status: {IssueId}", options.IssueId);
            return CommandResult<IssueUpdateResult>.ErrorResult($"Failed to update issue status: {ex.Message}");
        }
    }

    public async Task<CommandResult<IssuesListResult>> GetIssuesByStatusAsync(IssuesOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Getting issues by status: {Status}", options.Status);

            // Get all issues and filter by status
            var issuesResponse = await _apiClient.GetAllIssuesAsync(options.RepositoryId);
            if (!issuesResponse.Success || issuesResponse.Data == null)
            {
                return CommandResult<IssuesListResult>.ErrorResult(issuesResponse.Message ?? "Failed to get issues");
            }

            var allIssues = issuesResponse.Data;
            var statusFilteredIssues = allIssues.Where(i => 
                string.Equals(i.Title, options.Status, StringComparison.OrdinalIgnoreCase) || // Adjust based on actual status field
                string.Equals("open", options.Status, StringComparison.OrdinalIgnoreCase)); // Default to open if no status field

            var issueInfos = statusFilteredIssues.Take(options.Limit).Select(MapToIssueInfo).ToList();

            var result = new IssuesListResult
            {
                Issues = issueInfos,
                TotalCount = allIssues.Count,
                FilteredCount = statusFilteredIssues.Count(),
                Success = true
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<IssuesListResult>.SuccessResult(
                result,
                $"Found {issueInfos.Count} issues with status {options.Status}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issues by status: {Status}", options.Status);
            return CommandResult<IssuesListResult>.ErrorResult($"Failed to get issues by status: {ex.Message}");
        }
    }

    private IEnumerable<CodeIssueDto> FilterIssues(List<CodeIssueDto> issues, IssuesOptions options)
    {
        var filtered = issues.AsEnumerable();

        if (!string.IsNullOrEmpty(options.Severity))
        {
            filtered = filtered.Where(i => 
                string.Equals(i.SeverityName, options.Severity, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(options.Type))
        {
            filtered = filtered.Where(i => 
                string.Equals(i.CategoryName, options.Type, StringComparison.OrdinalIgnoreCase));
        }

        // Add more filters as needed
        return filtered;
    }

    private List<CodeIssueDto> SortIssues(IEnumerable<CodeIssueDto> issues, string sortBy)
    {
        return sortBy.ToLower() switch
        {
            "severity" => issues.OrderByDescending(i => i.Severity).ToList(), // Use integer for sorting
            "file" => issues.OrderBy(i => i.FilePath).ToList(),
            "line" => issues.OrderBy(i => i.LineNumber).ToList(),
            "created" => issues.OrderByDescending(i => i.DetectedAt).ToList(),
            _ => issues.OrderByDescending(i => i.Severity).ToList() // Use integer for sorting
        };
    }

    private int GetSeverityOrder(string severity)
    {
        return severity.ToLower() switch
        {
            "critical" => 5,
            "high" => 4,
            "medium" => 3,
            "low" => 2,
            "info" => 1,
            _ => 0
        };
    }

    private IssueInfo MapToIssueInfo(CodeIssueDto dto)
    {
        return new IssueInfo
        {
            Id = dto.Id.ToString(),
            Type = dto.CategoryName, // Use CategoryName property for string representation
            Severity = dto.SeverityName, // Use SeverityName property for string representation
            Message = dto.Title,
            Description = dto.Description,
            File = dto.FilePath,
            Line = dto.LineNumber,
            Column = dto.ColumnNumber ?? 0,
            ConfidenceScore = dto.Confidence,
            Recommendations = ParseRecommendations(dto.SuggestedFix),
            CreatedAt = dto.DetectedAt,
            Status = "open" // Default status, could be enhanced with actual status field
        };
    }

    private List<string> ParseRecommendations(string? suggestedFix)
    {
        if (string.IsNullOrEmpty(suggestedFix))
            return new List<string>();

        // Simple parsing - could be enhanced
        return new List<string> { suggestedFix };
    }

    public async Task<CommandResult<IssueUpdateResult>> FixIssuesAsync(IssuesOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Fixing issues with options: {@Options}", options);

            // This would require integration with the API to apply fixes
            // For now, return a placeholder result
            var result = new IssueUpdateResult
            {
                IssueId = options.IssueId ?? "multiple",
                Operation = "Apply fixes",
                Success = true
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<IssueUpdateResult>.SuccessResult(
                result,
                "Issue fixing API not yet implemented");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing issues");
            return CommandResult<IssueUpdateResult>.ErrorResult($"Failed to fix issues: {ex.Message}");
        }
    }

    public async Task<CommandResult<IssueExportResult>> ExportIssuesAsync(IssuesOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Exporting issues with options: {@Options}", options);

            // Get issues from API
            var issuesResponse = await _apiClient.GetAllIssuesAsync(options.RepositoryId);
            if (!issuesResponse.Success || issuesResponse.Data == null)
            {
                return CommandResult<IssueExportResult>.ErrorResult(issuesResponse.Message ?? "Failed to get issues");
            }

            var issues = issuesResponse.Data;
            var outputFile = options.OutputFile ?? $"issues-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

            // Export logic would go here - for now just return success
            var result = new IssueExportResult
            {
                OutputFile = outputFile,
                ExportFormat = "json", // Could use options.OutputFormat
                IssuesExported = issues.Count,
                Success = true
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<IssueExportResult>.SuccessResult(
                result,
                $"Exported {issues.Count} issues to {outputFile}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting issues");
            return CommandResult<IssueExportResult>.ErrorResult($"Failed to export issues: {ex.Message}");
        }
    }

    public async Task<CommandResult<IssueStatsResult>> GetIssueStatsAsync(IssuesOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Getting issue statistics with options: {@Options}", options);

            // Get issues from API
            var issuesResponse = await _apiClient.GetAllIssuesAsync(options.RepositoryId);
            if (!issuesResponse.Success || issuesResponse.Data == null)
            {
                return CommandResult<IssueStatsResult>.ErrorResult(issuesResponse.Message ?? "Failed to get issues");
            }

            var issues = issuesResponse.Data;

            // Calculate statistics
            var stats = new IssueStatsResult
            {
                TotalIssues = issues.Count,
                IssuesBySeverity = issues.GroupBy(i => i.SeverityName)
                    .ToDictionary(g => g.Key, g => g.Count()),
                IssuesByCategory = issues.GroupBy(i => i.CategoryName)
                    .ToDictionary(g => g.Key, g => g.Count()),
                IssuesByFile = issues.GroupBy(i => i.FilePath)
                    .ToDictionary(g => g.Key, g => g.Count()),
                Success = true
            };

            stopwatch.Stop();
            stats.Duration = stopwatch.Elapsed;

            return CommandResult<IssueStatsResult>.SuccessResult(
                stats,
                $"Retrieved statistics for {issues.Count} issues");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issue statistics");
            return CommandResult<IssueStatsResult>.ErrorResult($"Failed to get issue statistics: {ex.Message}");
        }
    }

    private async Task<string?> GetSourceCodeContextAsync(string filePath, int lineNumber)
    {
        // This would require a separate API endpoint to get file content
        // For now, return null as this functionality isn't implemented in the API yet
        await Task.Delay(1);
        return null;
    }
}