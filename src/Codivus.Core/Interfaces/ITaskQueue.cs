using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Core.Models;

namespace Codivus.Core.Interfaces
{
    public interface ITaskQueue<T> where T : IQueueTask
    {
        Task<string> EnqueueAsync(T task, CancellationToken cancellationToken = default);
        Task<T> DequeueAsync(CancellationToken cancellationToken = default);
        Task<T> PeekAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateTaskStatusAsync(string taskId, QueueTaskStatus status, string? message = null, CancellationToken cancellationToken = default);
        Task<T> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetTasksAsync(QueueTaskStatus? status = null, int limit = 100, CancellationToken cancellationToken = default);
        Task<int> GetQueueLengthAsync(QueueTaskStatus? status = null, CancellationToken cancellationToken = default);
        Task<bool> RemoveTaskAsync(string taskId, CancellationToken cancellationToken = default);
        Task<bool> RequeueFailedTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
        Task ClearQueueAsync(CancellationToken cancellationToken = default);
    }

    public interface IQueueTask
    {
        string TaskId { get; set; }
        string TaskType { get; set; }
        QueueTaskStatus Status { get; set; }
        TaskPriority Priority { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime? StartedAt { get; set; }
        DateTime? CompletedAt { get; set; }
        int RetryCount { get; set; }
        int MaxRetries { get; set; }
        string? ErrorMessage { get; set; }
        Dictionary<string, object> Metadata { get; set; }
    }

    public interface IPersistentTaskQueue<T> : ITaskQueue<T> where T : IQueueTask
    {
        Task<bool> SaveCheckpointAsync(string taskId, object checkpointData, CancellationToken cancellationToken = default);
        Task<object> GetCheckpointAsync(string taskId, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetStaleTasksAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
        Task<bool> ArchiveCompletedTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
    }
}