namespace Codivus.Core.Models;

/// <summary>
/// Represents an issue detected during code scanning
/// </summary>
public class CodeIssue
{
    /// <summary>
    /// Unique identifier for the issue
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Repository ID where the issue was found
    /// </summary>
    public required Guid RepositoryId { get; set; }
    
    /// <summary>
    /// Scan ID that discovered this issue
    /// </summary>
    public required Guid ScanId { get; set; }
    
    /// <summary>
    /// File path where the issue was found
    /// </summary>
    public required string FilePath { get; set; }
    
    /// <summary>
    /// Line number where the issue starts
    /// </summary>
    public int LineNumber { get; set; }
    
    /// <summary>
    /// Column number where the issue starts
    /// </summary>
    public int? ColumnNumber { get; set; }
    
    /// <summary>
    /// Number of lines the issue spans
    /// </summary>
    public int? LineSpan { get; set; }
    
    /// <summary>
    /// Issue title or summary
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Detailed description of the issue
    /// </summary>
    public required string Description { get; set; }
    
    /// <summary>
    /// Severity level of the issue
    /// </summary>
    public IssueSeverity Severity { get; set; }
    
    /// <summary>
    /// Category of the issue
    /// </summary>
    public IssueCategory Category { get; set; }
    
    /// <summary>
    /// Confidence level of the issue detection
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Source code segment containing the issue
    /// </summary>
    public string? CodeSnippet { get; set; }
    
    /// <summary>
    /// Suggested fix for the issue
    /// </summary>
    public string? SuggestedFix { get; set; }
    
    /// <summary>
    /// Additional references or resources
    /// </summary>
    public string? References { get; set; }
    
    /// <summary>
    /// Unique hash of the issue (for deduplication)
    /// </summary>
    public string? Hash { get; set; }
    
    /// <summary>
    /// Detection method that found the issue
    /// </summary>
    public IssueDetectionMethod DetectionMethod { get; set; }
    
    /// <summary>
    /// When the issue was detected
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Severity levels for code issues
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Information or suggestion (not a problem)
    /// </summary>
    Info,
    
    /// <summary>
    /// Minor issues that should be addressed but aren't critical
    /// </summary>
    Low,
    
    /// <summary>
    /// Issues that should be fixed but don't pose immediate risks
    /// </summary>
    Medium,
    
    /// <summary>
    /// Serious issues that should be prioritized
    /// </summary>
    High,
    
    /// <summary>
    /// Critical issues that require immediate attention
    /// </summary>
    Critical
}

/// <summary>
/// Categories of code issues
/// </summary>
public enum IssueCategory
{
    /// <summary>
    /// Security vulnerabilities
    /// </summary>
    Security,
    
    /// <summary>
    /// Performance problems
    /// </summary>
    Performance,
    
    /// <summary>
    /// Code quality and maintainability
    /// </summary>
    Quality,
    
    /// <summary>
    /// Design and architecture issues
    /// </summary>
    Architecture,
    
    /// <summary>
    /// Dependency and package issues
    /// </summary>
    Dependency,
    
    /// <summary>
    /// Testing and coverage issues
    /// </summary>
    Testing,
    
    /// <summary>
    /// Documentation problems
    /// </summary>
    Documentation,
    
    /// <summary>
    /// Accessibility concerns
    /// </summary>
    Accessibility,
    
    /// <summary>
    /// Other issues that don't fit into the above categories
    /// </summary>
    Other
}

/// <summary>
/// Method used to detect an issue
/// </summary>
public enum IssueDetectionMethod
{
    /// <summary>
    /// Detected by AI-powered analysis
    /// </summary>
    AiAnalysis,
    
    /// <summary>
    /// Detected by IssueHunter
    /// </summary>
    IssueHunter,
    
    /// <summary>
    /// Detected by pattern matching
    /// </summary>
    PatternMatching,
    
    /// <summary>
    /// Detected by manual analysis
    /// </summary>
    Manual,
    
    /// <summary>
    /// Detected by static analysis tools
    /// </summary>
    Static
}
