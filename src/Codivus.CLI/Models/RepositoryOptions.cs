namespace Codivus.CLI.Models;

public class RepositoryOptions
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Url { get; set; }
    public string? DefaultBranch { get; set; }
    public string Type { get; set; } = "Local";
    public string? RepositoryId { get; set; }
    public bool Detailed { get; set; }
    public bool Force { get; set; }
    public bool IncludeStructure { get; set; }
    public string? OutputFile { get; set; }
    public string OutputFormat { get; set; } = "console";
}