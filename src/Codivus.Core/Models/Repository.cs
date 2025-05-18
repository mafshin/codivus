namespace Codivus.Core.Models;

/// <summary>
/// Represents a code repository that can be analyzed
/// </summary>
public class Repository
{
    /// <summary>
    /// Unique identifier for the repository
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Name of the repository
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Optional description of the repository
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Type of repository (Local or GitHub)
    /// </summary>
    public RepositoryType Type { get; set; }
    
    /// <summary>
    /// Path or URL to the repository
    /// </summary>
    public required string Location { get; set; }
    
    /// <summary>
    /// GitHub specific owner (username or organization)
    /// </summary>
    public string? Owner { get; set; }
    
    /// <summary>
    /// Default branch name
    /// </summary>
    public string? DefaultBranch { get; set; }
    
    /// <summary>
    /// Date and time when the repository was added
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Date and time of the last scan
    /// </summary>
    public DateTime? LastScanAt { get; set; }
}

/// <summary>
/// Type of repository
/// </summary>
public enum RepositoryType
{
    /// <summary>
    /// Local file system repository
    /// </summary>
    Local,
    
    /// <summary>
    /// GitHub repository
    /// </summary>
    GitHub
}
