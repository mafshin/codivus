using System.Collections.Generic;

namespace Codivus.Core.Models
{
    public class GraphScanningOptions
    {
        public const string SectionName = "GraphScanning";
        
        public bool Enabled { get; set; } = true;
        public ProcessingOptions Processing { get; set; } = new();
        public AnalysisOptions Analysis { get; set; } = new();
    }

    public class ProcessingOptions
    {
        public int MaxConcurrentFiles { get; set; } = 50;
        public int BatchSize { get; set; } = 1000;
        public int TimeoutMinutes { get; set; } = 30;
        public int RetryAttempts { get; set; } = 3;
        public int WorkerCount { get; set; } = 4;
    }

    public class AnalysisOptions
    {
        public bool IncludeTests { get; set; } = false;
        public int MaxFileSize { get; set; } = 1048576; // 1MB
        public List<string> SupportedExtensions { get; set; } = new() { ".cs", ".vb" };
        public List<string> ExcludedPatterns { get; set; } = new()
        {
            "**/bin/**",
            "**/obj/**",
            "**/.git/**",
            "**/packages/**",
            "**/*.Designer.cs",
            "**/*.g.cs",
            "**/*.g.i.cs"
        };
    }
}