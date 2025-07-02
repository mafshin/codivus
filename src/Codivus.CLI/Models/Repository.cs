namespace Codivus.CLI.Models;

public class Repository
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Type { get; set; } = "";
    public string? DefaultBranch { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? LastScannedAt { get; set; }
}

public class RepositoryFile
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Extension { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime LastModified { get; set; }
    public long? SizeInBytes { get; set; }
    public List<RepositoryFile>? Children { get; set; }
}

public class CodeNode
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

public class CodeRelationship
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