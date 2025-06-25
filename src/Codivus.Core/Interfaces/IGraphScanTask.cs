using System.Collections.Generic;
using Codivus.Core.Models;

namespace Codivus.Core.Interfaces
{
    public interface IGraphScanTask : IQueueTask
    {
        string RepositoryId { get; set; }
        string ScanId { get; set; }
        ScanScope Scope { get; set; }
        string TargetPath { get; set; }
        string ProjectId { get; set; }
        List<string> FileIds { get; set; }
        GraphScanOptions Options { get; set; }
        GraphScanCheckpoint Checkpoint { get; set; }
    }

    public class GraphScanOptions
    {
        public bool FullScan { get; set; } = false;
        public bool IncludeTests { get; set; } = false;
        public bool AnalyzeGeneratedCode { get; set; } = false;
        public int MaxFileSizeBytes { get; set; } = 1048576;
        public List<string> IncludePatterns { get; set; } = new();
        public List<string> ExcludePatterns { get; set; } = new();
        public bool BuildRelationships { get; set; } = true;
        public bool CalculateMetrics { get; set; } = true;
        public int BatchSize { get; set; } = 100;
    }

    public class GraphScanCheckpoint
    {
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public string LastProcessedFile { get; set; }
        public Dictionary<string, object> State { get; set; } = new();
        public List<string> ProcessedFileIds { get; set; } = new();
        public List<string> FailedFileIds { get; set; } = new();
    }
}