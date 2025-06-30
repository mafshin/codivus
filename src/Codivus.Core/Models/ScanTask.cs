using System;
using System.Collections.Generic;
using Codivus.Core.Interfaces;

namespace Codivus.Core.Models
{
    public enum QueueTaskStatus
    {
        Pending,
        Queued,
        InProgress,
        Completed,
        Failed,
        Cancelled,
        Paused
    }

    public enum TaskPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public enum ScanScope
    {
        File,
        Directory,
        Project,
        Solution,
        Repository
    }

    public class ScanTask : IQueueTask
    {
        public string TaskId { get; set; } = Guid.NewGuid().ToString();
        public string TaskType { get; set; } = string.Empty;
        public QueueTaskStatus Status { get; set; } = QueueTaskStatus.Pending;
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        public TimeSpan? EstimatedDuration { get; set; }
        public string? CreatedBy { get; set; }
        public string? AssignedTo { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class GraphScanTask : ScanTask, IGraphScanTask
    {
        public GraphScanTask()
        {
            TaskType = "GraphScan";
        }

        public string RepositoryId { get; set; } = string.Empty;
        public string ScanId { get; set; } = string.Empty;
        public ScanScope Scope { get; set; }
        public string TargetPath { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public List<string> FileIds { get; set; } = new();
        public GraphScanOptions Options { get; set; } = new();
        public GraphScanCheckpoint Checkpoint { get; set; } = new();
    }

    public class GraphScanOptions
    {
        public bool FullScan { get; set; } = false;
        public bool IncludeTests { get; set; } = false;
        public bool AnalyzeGeneratedCode { get; set; } = false;
        public long MaxFileSizeBytes { get; set; } = 1024 * 1024; // 1MB
        public bool BuildRelationships { get; set; } = true;
        public bool CalculateMetrics { get; set; } = true;
        public int BatchSize { get; set; } = 100;
        public bool ContinueOnError { get; set; } = true;
        public List<string> IncludePatterns { get; set; } = new();
        public List<string> ExcludePatterns { get; set; } = new();
        public List<string> SupportedExtensions { get; set; } = new() { ".cs", ".vb" };
    }

    public class GraphScanCheckpoint
    {
        public int ProcessedFiles { get; set; } = 0;
        public int TotalFiles { get; set; } = 0;
        public string? LastProcessedFile { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> State { get; set; } = new();
        public List<string> ProcessedFileIds { get; set; } = new();
        public List<string> FailedFileIds { get; set; } = new();
    }
}