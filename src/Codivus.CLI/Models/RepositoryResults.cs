namespace Codivus.CLI.Models;

public class RepositoryResult
{
    public Repository? Repository { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class RepositoryListResult
{
    public List<RepositoryDetail> Repositories { get; set; } = new();
    public int TotalCount { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class RepositoryDetail
{
    public Repository Repository { get; set; } = new()
    {
        Name = "",
        Location = ""
    };
    public int ScanCount { get; set; }
    public int IssueCount { get; set; }
    public bool HasActiveScans { get; set; }
}

public class RepositoryValidationResult
{
    public string Path { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class RepositoryInfoResult
{
    public Repository? Repository { get; set; }
    public int ScanCount { get; set; }
    public int IssueCount { get; set; }
    public bool HasActiveScans { get; set; }
    public RepositoryFile? Structure { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}