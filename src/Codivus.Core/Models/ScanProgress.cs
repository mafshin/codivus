namespace Codivus.Core.Models;

/// <summary>
/// Represents the progress and status of a scanning operation
/// </summary>
public class ScanProgress
{
    /// <summary>
    /// Unique identifier for the scan
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Repository ID being scanned
    /// </summary>
    public required Guid RepositoryId { get; set; }
    
    /// <summary>
    /// Configuration ID used for this scan
    /// </summary>
    public required Guid ConfigurationId { get; set; }
    
    /// <summary>
    /// Current status of the scan
    /// </summary>
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    
    /// <summary>
    /// Total number of files to scan
    /// </summary>
    public int TotalFiles { get; set; }
    
    /// <summary>
    /// Number of files that have been scanned
    /// </summary>
    public int ScannedFiles { get; set; }
    
    /// <summary>
    /// Number of issues found so far
    /// </summary>
    public int IssuesFound { get; set; }
    
    /// <summary>
    /// Current file being scanned
    /// </summary>
    public string? CurrentFile { get; set; }
    
    /// <summary>
    /// Time when the scan started
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// Time when the scan completed (or failed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Error message if the scan failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Estimated time remaining for the scan (in seconds)
    /// </summary>
    public double? EstimatedRemainingSeconds { get; set; }
    
    /// <summary>
    /// Issues by category counts
    /// </summary>
    public Dictionary<IssueCategory, int> IssuesByCategory { get; set; } = new();
    
    /// <summary>
    /// Issues by severity counts
    /// </summary>
    public Dictionary<IssueSeverity, int> IssuesBySeverity { get; set; } = new();
}

/// <summary>
/// Status of a scan
/// </summary>
public enum ScanStatus
{
    /// <summary>
    /// Scan is pending to start
    /// </summary>
    Pending,
    
    /// <summary>
    /// Scan is initializing
    /// </summary>
    Initializing,
    
    /// <summary>
    /// Scan is in progress
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Scan is paused
    /// </summary>
    Paused,
    
    /// <summary>
    /// Scan is canceled
    /// </summary>
    Canceled,
    
    /// <summary>
    /// Scan is completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Scan failed
    /// </summary>
    Failed
}
