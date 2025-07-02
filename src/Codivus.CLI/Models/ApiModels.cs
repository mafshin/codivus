namespace Codivus.CLI.Models;

// Repository models for API communication
public class RepositoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public int Type { get; set; } = 0; // RepositoryType enum: Local = 0, GitHub = 1
    public string? DefaultBranch { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? LastScannedAt { get; set; }
    
    // Helper property for string representation
    public string TypeName => Type == 0 ? "Local" : "GitHub";
}

public class CreateRepositoryRequest
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public int Type { get; set; } = 0; // RepositoryType enum: Local = 0, GitHub = 1
    public string? DefaultBranch { get; set; }
    public string? Url { get; set; }
}

public class RepositoryValidationRequest
{
    public string Location { get; set; } = "";
    public int Type { get; set; } = 0; // RepositoryType enum: Local = 0, GitHub = 1
}

public class RepositoryValidationResponse
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// Scan models for API communication
public class ScanConfigurationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid RepositoryId { get; set; }
    public string? Branch { get; set; }
    public List<string> IncludeExtensions { get; set; } = new();
    public List<string> ExcludeExtensions { get; set; } = new();
    public List<string> IncludeDirectories { get; set; } = new();
    public List<string> ExcludeDirectories { get; set; } = new();
    public long MaxFileSizeBytes { get; set; } = 1024 * 1024; // 1MB default
    public List<int> IncludeCategories { get; set; } = new();
    public int MinimumSeverity { get; set; } = 0; // Low = 0
    public bool UseAi { get; set; } = true;
    public int LlmProvider { get; set; } = 0; // Ollama = 0
    public string LlmModel { get; set; } = "codellama:7b-instruct";
    public bool UseIssueHunter { get; set; } = true;
    public bool SuggestFixes { get; set; } = true;
    public int MaxConcurrentTasks { get; set; } = 4;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StartScanRequest
{
    public Guid RepositoryId { get; set; }
    public ScanConfigurationDto Configuration { get; set; } = new();
}

public class ScanProgressDto
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid ConfigurationId { get; set; }
    public int Status { get; set; } = 0; // ScanStatus enum: 0=Pending, 1=Initializing, 2=InProgress, 3=Paused, 4=Canceled, 5=Completed, 6=Failed
    public int TotalFiles { get; set; }
    public int ScannedFiles { get; set; }
    public int IssuesFound { get; set; }
    public string? CurrentFile { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public double? EstimatedRemainingSeconds { get; set; }
    public Dictionary<string, int> IssuesByCategory { get; set; } = new();
    public Dictionary<string, int> IssuesBySeverity { get; set; } = new();
    
    // Helper property for string representation
    public string StatusName => Status switch
    {
        0 => "Pending",
        1 => "Initializing", 
        2 => "InProgress",
        3 => "Paused",
        4 => "Canceled",
        5 => "Completed",
        6 => "Failed",
        _ => "Unknown"
    };
}

public class CodeIssueDto
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid ScanId { get; set; }
    public string FilePath { get; set; } = "";
    public int LineNumber { get; set; }
    public int? ColumnNumber { get; set; }
    public int? LineSpan { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Severity { get; set; } = 0; // IssueSeverity enum: 0=Info, 1=Low, 2=Medium, 3=High, 4=Critical
    public int Category { get; set; } = 0; // IssueCategory enum: 0=Security, 1=Performance, 2=Quality, 3=Architecture, 4=Dependency, 5=Testing, 6=Documentation, 7=Accessibility, 8=Other
    public double Confidence { get; set; }
    public string? CodeSnippet { get; set; }
    public string? SuggestedFix { get; set; }
    public string? References { get; set; }
    public string? Hash { get; set; }
    public int DetectionMethod { get; set; } = 0; // IssueDetectionMethod enum: 0=AiAnalysis, 1=IssueHunter, 2=PatternMatching, 3=Manual, 4=Static
    public DateTime DetectedAt { get; set; }
    
    // Helper properties for string representation
    public string SeverityName => Severity switch
    {
        0 => "Info",
        1 => "Low",
        2 => "Medium",
        3 => "High",
        4 => "Critical",
        _ => "Unknown"
    };
    
    public string CategoryName => Category switch
    {
        0 => "Security",
        1 => "Performance",
        2 => "Quality",
        3 => "Architecture",
        4 => "Dependency",
        5 => "Testing",
        6 => "Documentation",
        7 => "Accessibility",
        8 => "Other",
        _ => "Unknown"
    };
    
    public string DetectionMethodName => DetectionMethod switch
    {
        0 => "AiAnalysis",
        1 => "IssueHunter",
        2 => "PatternMatching",
        3 => "Manual",
        4 => "Static",
        _ => "Unknown"
    };
}

// Graph models for API communication
public class GraphScanConfigurationDto
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string ScanMode { get; set; } = "Full";
    public int BatchSize { get; set; } = 100;
    public bool ProcessCodeElements { get; set; } = true;
    public bool ProcessRelationships { get; set; } = true;
    public bool ProcessMetrics { get; set; } = true;
    public List<string> IncludeFileExtensions { get; set; } = new();
    public List<string> ExcludeFileExtensions { get; set; } = new();
    public List<string> ExcludeDirectories { get; set; } = new();
    public long MaxFileSizeBytes { get; set; } = 1048576; // 1MB
    public int MaxConcurrentTasks { get; set; } = 4;
    public bool ContinueOnError { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class StartGraphScanRequest
{
    public Guid RepositoryId { get; set; }
    public GraphScanConfigurationDto Configuration { get; set; } = new();
}

public class GraphScanProgressDto
{
    public Guid ScanId { get; set; }
    public Guid RepositoryId { get; set; }
    public string Status { get; set; } = "";
    public string CurrentTask { get; set; } = "";
    public int TasksCompleted { get; set; }
    public int TasksTotal { get; set; }
    public int FilesProcessed { get; set; }
    public int FilesTotal { get; set; }
    public int NodesCreated { get; set; }
    public int RelationshipsCreated { get; set; }
    public Dictionary<string, int> NodesByType { get; set; } = new();
    public Dictionary<string, int> RelationshipsByType { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime? EstimatedCompletionAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class GraphMetricsDto
{
    public string RepositoryId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public long VertexCount { get; set; }
    public long EdgeCount { get; set; }
    public int TotalProjects { get; set; }
    public int TotalFiles { get; set; }
    public int TotalTypes { get; set; }
    public int TotalMethods { get; set; }
    public double AverageComplexity { get; set; }
    public double AverageCoupling { get; set; }
    public Dictionary<string, long> VertexCountByType { get; set; } = new();
    public Dictionary<string, long> EdgeCountByType { get; set; } = new();
    public long ProcessingTimeMs { get; set; }
    public long MemoryUsageBytes { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

public class GraphNodeDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string NodeType { get; set; } = "";
    public string RepositoryId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string FileId { get; set; } = "";
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }
    public string Checksum { get; set; } = "";
    public bool IsPublic { get; set; }
    public bool IsStatic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public int? CyclomaticComplexity { get; set; }
    public int? CognitiveComplexity { get; set; }
    public int? LinesOfCode { get; set; }
    public string? ReturnType { get; set; }
    public string? Signature { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GraphRelationshipDto
{
    public string Id { get; set; } = "";
    public string SourceNodeId { get; set; } = "";
    public string TargetNodeId { get; set; } = "";
    public string RelationshipType { get; set; } = "";
    public double Weight { get; set; } = 1.0;
    public Dictionary<string, object> Properties { get; set; } = new();
    public string Context { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

// API Response wrappers
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ApiListResponse<T> : ApiResponse<List<T>>
{
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
}

// Repository file structure
public class RepositoryFileDto
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Extension { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime LastModified { get; set; }
    public long? SizeInBytes { get; set; }
    public List<RepositoryFileDto>? Children { get; set; }
}

// Repository details with statistics
public class RepositoryDetailsDto
{
    public RepositoryDto Repository { get; set; } = new();
    public RepositorySummaryDto Summary { get; set; } = new();
    public DateTime? LastActivity { get; set; }
    public bool CanDelete { get; set; }
    public string? DeletionInfo { get; set; }
}

public class RepositorySummaryDto
{
    public bool HasActiveScans { get; set; }
    public int TotalScans { get; set; }
    public int TotalIssues { get; set; }
    public int TotalConfigurations { get; set; }
}