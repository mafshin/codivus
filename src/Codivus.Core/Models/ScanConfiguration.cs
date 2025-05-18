namespace Codivus.Core.Models;

/// <summary>
/// Represents the configuration for a code scan
/// </summary>
public class ScanConfiguration
{
    /// <summary>
    /// Unique identifier for the scan configuration
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Name of the scan configuration
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Repository ID to scan
    /// </summary>
    public required Guid RepositoryId { get; set; }
    
    /// <summary>
    /// Branch to scan (null for default branch)
    /// </summary>
    public string? Branch { get; set; }
    
    /// <summary>
    /// File extensions to include in the scan
    /// </summary>
    public List<string> IncludeExtensions { get; set; } = new();
    
    /// <summary>
    /// File extensions to exclude from the scan
    /// </summary>
    public List<string> ExcludeExtensions { get; set; } = new();
    
    /// <summary>
    /// Directories to include in the scan
    /// </summary>
    public List<string> IncludeDirectories { get; set; } = new();
    
    /// <summary>
    /// Directories to exclude from the scan
    /// </summary>
    public List<string> ExcludeDirectories { get; set; } = new();
    
    /// <summary>
    /// Maximum file size to scan (in bytes)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 1024 * 1024; // 1MB default
    
    /// <summary>
    /// Issue categories to include in the scan
    /// </summary>
    public List<IssueCategory> IncludeCategories { get; set; } = new();
    
    /// <summary>
    /// Minimum severity level to report
    /// </summary>
    public IssueSeverity MinimumSeverity { get; set; } = IssueSeverity.Low;
    
    /// <summary>
    /// Whether to use AI-powered analysis
    /// </summary>
    public bool UseAi { get; set; } = true;
    
    /// <summary>
    /// LLM provider to use for AI analysis
    /// </summary>
    public LlmProviderType LlmProvider { get; set; } = LlmProviderType.Ollama;
    
    /// <summary>
    /// Model name to use for LLM provider
    /// </summary>
    public string LlmModel { get; set; } = "codellama:7b-instruct";
    
    /// <summary>
    /// Whether to use IssueHunter
    /// </summary>
    public bool UseIssueHunter { get; set; } = true;
    
    /// <summary>
    /// Whether to suggest fixes for issues
    /// </summary>
    public bool SuggestFixes { get; set; } = true;
    
    /// <summary>
    /// Maximum number of concurrent analysis tasks
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 4;
    
    /// <summary>
    /// Date and time when the configuration was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Date and time when the configuration was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Supported LLM provider types
/// </summary>
public enum LlmProviderType
{
    /// <summary>
    /// Ollama LLM provider
    /// </summary>
    Ollama,
    
    /// <summary>
    /// LMStudio LLM provider
    /// </summary>
    LmStudio
}
