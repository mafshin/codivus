namespace Codivus.Core.Models;

/// <summary>
/// Represents a file in a repository structure
/// </summary>
public class RepositoryFile
{
    /// <summary>
    /// Unique identifier for the file
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Repository ID this file belongs to
    /// </summary>
    public required Guid RepositoryId { get; set; }
    
    /// <summary>
    /// Parent directory ID (null for root-level files)
    /// </summary>
    public Guid? ParentDirectoryId { get; set; }
    
    /// <summary>
    /// Name of the file
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Full path to the file
    /// </summary>
    public required string Path { get; set; }
    
    /// <summary>
    /// File extension
    /// </summary>
    public string? Extension { get; set; }
    
    /// <summary>
    /// Size of the file in bytes
    /// </summary>
    public long SizeInBytes { get; set; }
    
    /// <summary>
    /// Last modification date
    /// </summary>
    public DateTime LastModified { get; set; }
    
    /// <summary>
    /// Whether this is a directory
    /// </summary>
    public bool IsDirectory { get; set; }
    
    /// <summary>
    /// Number of issues found in this file
    /// </summary>
    public int IssueCount { get; set; }
    
    /// <summary>
    /// Child files (if this is a directory)
    /// </summary>
    public List<RepositoryFile>? Children { get; set; }
}
