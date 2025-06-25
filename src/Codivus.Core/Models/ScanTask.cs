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
        public string TaskType { get; set; }
        public QueueTaskStatus Status { get; set; } = QueueTaskStatus.Pending;
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        public TimeSpan? EstimatedDuration { get; set; }
        public string CreatedBy { get; set; }
        public string AssignedTo { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class GraphScanTask : ScanTask, IGraphScanTask
    {
        public GraphScanTask()
        {
            TaskType = "GraphScan";
        }

        public string RepositoryId { get; set; }
        public string ScanId { get; set; }
        public ScanScope Scope { get; set; }
        public string TargetPath { get; set; }
        public string ProjectId { get; set; }
        public List<string> FileIds { get; set; } = new();
        public GraphScanOptions Options { get; set; } = new();
        public GraphScanCheckpoint Checkpoint { get; set; } = new();
    }
}