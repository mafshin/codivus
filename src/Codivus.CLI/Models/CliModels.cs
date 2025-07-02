namespace Codivus.CLI.Models;

public class CommandResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public TimeSpan Duration { get; set; }

    public static CommandResult<T> SuccessResult(T data, string? message = null)
    {
        return new CommandResult<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static CommandResult<T> ErrorResult(string error)
    {
        return new CommandResult<T>
        {
            Success = false,
            Errors = { error }
        };
    }
}

public class ProgressReport
{
    public string Message { get; set; } = "";
    public double Percentage { get; set; }
    public string? CurrentItem { get; set; }
    public int? ItemsProcessed { get; set; }
    public int? TotalItems { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static ValidationResult Valid() => new() { IsValid = true };
    public static ValidationResult Invalid(string error) => new() { IsValid = false, Errors = { error } };
}

public class ScanOptions
{
    // Repository identification
    public string? RepositoryId { get; set; }
    public string? Path { get; set; }
    public string? RepositoryUrl { get; set; }
    
    // Scan identification
    public string? ScanId { get; set; }
    
    // Configuration
    public string? ConfigurationName { get; set; }
    public string Branch { get; set; } = "main";
    public bool IncludeTests { get; set; }
    public bool EnableGraph { get; set; } = true;
    
    // LLM settings
    public string? LLMProvider { get; set; }
    public string? Model { get; set; }
    
    // File filtering
    public List<string> FilePatterns { get; set; } = new();
    public List<string> ExcludePatterns { get; set; } = new();
    
    // Result filtering
    public string? IssueType { get; set; }
    public string? Severity { get; set; }
    public string? Status { get; set; }
    public int Limit { get; set; } = 100;
    
    // Output
    public string? OutputFile { get; set; }
    public bool Verbose { get; set; }
    public string OutputFormat { get; set; } = "console";
}

public class GraphOptions
{
    public string? RepositoryId { get; set; }
    public string? Query { get; set; }
    public string? NodeId { get; set; }
    public int MaxDepth { get; set; } = 3;
    public int Limit { get; set; } = 100;
    public string? OutputFile { get; set; }
    public string OutputFormat { get; set; } = "console";
    public bool IncludeMetrics { get; set; }
    
    // Graph scanning options
    public string? ScanMode { get; set; } = "full";
    public int BatchSize { get; set; } = 100;
}

public class IssueFilterOptions
{
    public string? Severity { get; set; }
    public string? Type { get; set; }
    public string? File { get; set; }
    public int? MinLine { get; set; }
    public int? MaxLine { get; set; }
    public bool IncludeResolved { get; set; }
}

public class ConfigurationOptions
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public bool Global { get; set; }
    public bool List { get; set; }
    public bool Reset { get; set; }
}

public class ScanResult
{
    public string RepositoryId { get; set; } = "";
    public string Path { get; set; } = "";
    public int FilesScanned { get; set; }
    public int IssuesFound { get; set; }
    public TimeSpan Duration { get; set; }
    public List<IssueInfo> Issues { get; set; } = new();
    public GraphMetrics? GraphMetrics { get; set; }
}

public class IssueInfo
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Message { get; set; } = "";
    public string Description { get; set; } = "";
    public string File { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "open";
}

public class GraphMetrics
{
    public long TotalNodes { get; set; }
    public long TotalRelationships { get; set; }
    public Dictionary<string, long> NodesByType { get; set; } = new();
    public Dictionary<string, long> RelationshipsByType { get; set; } = new();
    public double AverageComplexity { get; set; }
    public double AverageCoupling { get; set; }
}

public class IssuesOptions
{
    public string? RepositoryId { get; set; }
    public string? IssueId { get; set; }
    public string? Severity { get; set; }
    public string? Type { get; set; }
    public string Status { get; set; } = "all";
    public int Limit { get; set; } = 100;
    public string SortBy { get; set; } = "severity";
    public string? OutputFile { get; set; }
    public string OutputFormat { get; set; } = "console";
    public bool IncludeFixes { get; set; } = true;
    public bool IncludeContext { get; set; } = true;
    public bool DryRun { get; set; }
    public bool CreateBackup { get; set; } = true;
    public bool IncludeDismissed { get; set; }
    public bool IncludeFixed { get; set; }
    public string? Template { get; set; }
    public string TimeRange { get; set; } = "30d";
    public string GroupBy { get; set; } = "severity";
    public bool IncludeCharts { get; set; }
}

public class SettingsOptions
{
    public string? Section { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public string ValueType { get; set; } = "string";
    public string? DefaultValue { get; set; }
    public bool Force { get; set; }
    public bool Confirm { get; set; }
    public bool Fix { get; set; }
    public bool IncludeDefaults { get; set; }
    public bool Merge { get; set; } = true;
    public bool DryRun { get; set; }
    public string? Template { get; set; }
    public string? InputFile { get; set; }
    public string? OutputFile { get; set; }
    public string OutputFormat { get; set; } = "console";
}

public class InitOptions
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Template { get; set; } = "basic";
    public bool Force { get; set; }
    public bool InitializeGit { get; set; } = true;
    public string OutputFormat { get; set; } = "console";
}

public class StatusOptions
{
    public string? RepositoryId { get; set; }
    public bool IncludeSystemHealth { get; set; }
    public bool Detailed { get; set; }
    public bool Refresh { get; set; }
    public string? OutputFile { get; set; }
    public string OutputFormat { get; set; } = "console";
}

// New scan result models
public class ScanStartResult
{
    public string ScanId { get; set; } = "";
    public string RepositoryId { get; set; } = "";
    public string RepositoryName { get; set; } = "";
    public string Status { get; set; } = "";
    public int FilesTotal { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ScanStatusResult
{
    public List<ScanInfo> Scans { get; set; } = new();
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ScanInfo
{
    public string ScanId { get; set; } = "";
    public string RepositoryId { get; set; } = "";
    public string Status { get; set; } = "";
    public double Progress { get; set; }
    public int FilesProcessed { get; set; }
    public int FilesTotal { get; set; }
    public int IssuesFound { get; set; }
    public string? CurrentFile { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EstimatedCompletion { get; set; }
}

public class ScanResultsResult
{
    public string ScanId { get; set; } = "";
    public List<IssueInfo> Issues { get; set; } = new();
    public int TotalIssues { get; set; }
    public int FilteredIssues { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ScanOperationResult
{
    public string ScanId { get; set; } = "";
    public string Operation { get; set; } = "";
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ScanListResult
{
    public List<ScanInfo> Scans { get; set; } = new();
    public int TotalCount { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}