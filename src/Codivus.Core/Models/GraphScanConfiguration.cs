using System.Collections.Generic;

namespace Codivus.Core.Models
{
    public class GraphScanConfiguration
    {
        public string RepositoryId { get; set; }
        public bool Enabled { get; set; } = true;
        public ScanMode Mode { get; set; } = ScanMode.Incremental;
        public ProcessingConfiguration Processing { get; set; } = new();
        public AnalysisConfiguration Analysis { get; set; } = new();
        public RelationshipConfiguration Relationships { get; set; } = new();
        public MetricsConfiguration Metrics { get; set; } = new();
    }

    public enum ScanMode
    {
        Full,           // Complete rescan
        Incremental,    // Only changed files
        Differential    // Compare with baseline
    }

    public class ProcessingConfiguration
    {
        public int MaxConcurrentTasks { get; set; } = 4;
        public int BatchSize { get; set; } = 100;
        public int TimeoutMinutes { get; set; } = 30;
        public bool EnableCheckpoints { get; set; } = true;
        public int CheckpointIntervalMinutes { get; set; } = 5;
        public bool ContinueOnError { get; set; } = true;
        public int MaxErrorsBeforeStop { get; set; } = 100;
    }

    public class AnalysisConfiguration
    {
        public List<string> IncludedExtensions { get; set; } = new() { ".cs", ".vb" };
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
        public bool AnalyzeTests { get; set; } = false;
        public bool AnalyzeGeneratedCode { get; set; } = false;
        public int MaxFileSizeMB { get; set; } = 1;
        public int MaxMethodLength { get; set; } = 200;
        public int MaxClassLength { get; set; } = 1000;
        public int MaxCyclomaticComplexity { get; set; } = 10;
    }

    public class RelationshipConfiguration
    {
        public bool TrackCalls { get; set; } = true;
        public bool TrackInheritance { get; set; } = true;
        public bool TrackImplementations { get; set; } = true;
        public bool TrackUsages { get; set; } = true;
        public bool TrackReferences { get; set; } = true;
        public bool IncludeImplicitRelationships { get; set; } = false;
        public int MaxRelationshipDepth { get; set; } = 5;
    }

    public class MetricsConfiguration
    {
        public bool CalculateComplexity { get; set; } = true;
        public bool CalculateCoupling { get; set; } = true;
        public bool CalculateCohesion { get; set; } = true;
        public bool CalculateMaintainability { get; set; } = true;
        public bool TrackCodeChurn { get; set; } = false;
        public bool GenerateHeatmaps { get; set; } = false;
    }

    public class GraphScanProgress
    {
        public string ScanId { get; set; }
        public string RepositoryId { get; set; }
        public ScanStatus Status { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int FailedTasks { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int NodesCreated { get; set; }
        public int RelationshipsCreated { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public double EstimatedTimeRemainingMinutes { get; set; }
        public string CurrentTask { get; set; }
        public List<string> RecentErrors { get; set; } = new();
        public Dictionary<string, int> StatsByNodeType { get; set; } = new();
    }

}