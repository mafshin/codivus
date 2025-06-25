namespace Codivus.Graph.Models;

/// <summary>
/// Result of analyzing a code file using Roslyn
/// </summary>
public class CodeAnalysisResult
{
    /// <summary>
    /// Unique identifier for the analyzed file
    /// </summary>
    public string FileId { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the analyzed file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Project identifier this file belongs to
    /// </summary>
    public string? ProjectId { get; set; }
    
    /// <summary>
    /// Assembly identifier this file belongs to
    /// </summary>
    public string? AssemblyId { get; set; }
    
    /// <summary>
    /// Repository identifier this file belongs to
    /// </summary>
    public string RepositoryId { get; set; } = string.Empty;
    
    /// <summary>
    /// Code nodes (symbols) found in this file
    /// </summary>
    public List<CodeNode> Nodes { get; set; } = new();
    
    /// <summary>
    /// Relationships between symbols found in this file
    /// </summary>
    public List<CodeRelationship> Relationships { get; set; } = new();
    
    /// <summary>
    /// Analysis metrics for this file
    /// </summary>
    public FileAnalysisMetrics Metrics { get; set; } = new();
    
    /// <summary>
    /// Any errors that occurred during analysis
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Any warnings that occurred during analysis
    /// </summary>
    public List<string> Warnings { get; set; } = new();
    
    /// <summary>
    /// Time taken to analyze this file
    /// </summary>
    public TimeSpan AnalysisTime { get; set; }
    
    /// <summary>
    /// Timestamp when analysis was performed
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Metrics collected during file analysis
/// </summary>
public class FileAnalysisMetrics
{
    /// <summary>
    /// Total lines of code in the file
    /// </summary>
    public int LinesOfCode { get; set; }
    
    /// <summary>
    /// Number of namespaces found
    /// </summary>
    public int NamespaceCount { get; set; }
    
    /// <summary>
    /// Number of types (classes, interfaces, structs, etc.) found
    /// </summary>
    public int TypeCount { get; set; }
    
    /// <summary>
    /// Number of methods found
    /// </summary>
    public int MethodCount { get; set; }
    
    /// <summary>
    /// Number of properties found
    /// </summary>
    public int PropertyCount { get; set; }
    
    /// <summary>
    /// Number of fields found
    /// </summary>
    public int FieldCount { get; set; }
    
    /// <summary>
    /// Cyclomatic complexity of the file
    /// </summary>
    public int CyclomaticComplexity { get; set; }
    
    /// <summary>
    /// Number of dependencies (using statements)
    /// </summary>
    public int DependencyCount { get; set; }
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
}