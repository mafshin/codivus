using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.API.Services
{
    public class TaskQueueService<T> : ITaskQueue<T> where T : class, IQueueTask
    {
        private readonly ConcurrentDictionary<string, T> _tasks = new();
        private readonly ConcurrentQueue<string> _pendingQueue = new();
        private readonly ConcurrentDictionary<QueueTaskStatus, ConcurrentBag<string>> _tasksByStatus = new();
        private readonly SemaphoreSlim _queueSemaphore = new(0);
        private readonly object _dequeueLock = new();
        private readonly ILogger<TaskQueueService<T>> _logger;
        
        protected ILogger<TaskQueueService<T>> Logger => _logger;

        public TaskQueueService(ILogger<TaskQueueService<T>> logger)
        {
            _logger = logger;
            
            // Initialize status bags
            foreach (QueueTaskStatus status in Enum.GetValues(typeof(QueueTaskStatus)))
            {
                _tasksByStatus[status] = new ConcurrentBag<string>();
            }
        }

        public Task<string> EnqueueAsync(T task, CancellationToken cancellationToken = default)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            task.TaskId = task.TaskId ?? Guid.NewGuid().ToString();
            task.CreatedAt = DateTime.UtcNow;
            task.Status = QueueTaskStatus.Queued;

            if (!_tasks.TryAdd(task.TaskId, task))
            {
                throw new InvalidOperationException($"Task with ID {task.TaskId} already exists");
            }

            _tasksByStatus[QueueTaskStatus.Queued].Add(task.TaskId);
            _pendingQueue.Enqueue(task.TaskId);
            _queueSemaphore.Release();

            _logger.LogInformation("Enqueued task {TaskId} of type {TaskType} with priority {Priority}", 
                task.TaskId, task.TaskType, task.Priority);

            return Task.FromResult(task.TaskId);
        }

        public async Task<T> DequeueAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _queueSemaphore.WaitAsync(cancellationToken);

                // Use lock to prevent race conditions between multiple workers
                lock (_dequeueLock)
                {
                    var taskId = GetNextTaskId();
                    if (taskId == null)
                        continue;

                    if (_tasks.TryGetValue(taskId, out var task) && task.Status == QueueTaskStatus.Queued)
                    {
                        // Update status immediately within the lock to prevent other workers from taking it
                        task.Status = QueueTaskStatus.InProgress;
                        task.StartedAt = DateTime.UtcNow;
                        
                        // Update status tracking
                        RemoveFromStatusBag(QueueTaskStatus.Queued, taskId);
                        _tasksByStatus[QueueTaskStatus.InProgress].Add(taskId);
                        
                        _logger.LogInformation("Dequeued task {TaskId} of type {TaskType}", task.TaskId, task.TaskType);
                        return task;
                    }
                }
            }

            return default;
        }

        public Task<T> PeekAsync(CancellationToken cancellationToken = default)
        {
            var taskId = GetNextTaskId(peek: true);
            if (taskId != null && _tasks.TryGetValue(taskId, out var task))
            {
                return Task.FromResult(task);
            }

            return Task.FromResult(default(T));
        }

        public Task<bool> UpdateTaskStatusAsync(string taskId, QueueTaskStatus status, string? message = null, CancellationToken cancellationToken = default)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                _logger.LogWarning("Task {TaskId} not found", taskId);
                return Task.FromResult(false);
            }

            var oldStatus = task.Status;
            task.Status = status;

            if (!string.IsNullOrEmpty(message))
            {
                task.ErrorMessage = message;
            }

            if (status == QueueTaskStatus.Completed || status == QueueTaskStatus.Failed || status == QueueTaskStatus.Cancelled)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            // Update status tracking
            RemoveFromStatusBag(oldStatus, taskId);
            _tasksByStatus[status].Add(taskId);

            _logger.LogInformation("Updated task {TaskId} status from {OldStatus} to {NewStatus}", 
                taskId, oldStatus, status);

            return Task.FromResult(true);
        }

        public Task<T> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                return Task.FromResult(task);
            }

            return Task.FromResult(default(T));
        }

        public Task<IEnumerable<T>> GetTasksAsync(QueueTaskStatus? status = null, int limit = 100, CancellationToken cancellationToken = default)
        {
            IEnumerable<T> tasks;

            if (status.HasValue)
            {
                var taskIds = _tasksByStatus[status.Value].ToList();
                tasks = taskIds
                    .Select(id => _tasks.TryGetValue(id, out var task) ? task : default)
                    .Where(t => t != null)
                    .Take(limit);
            }
            else
            {
                tasks = _tasks.Values.Take(limit);
            }

            return Task.FromResult(tasks);
        }

        public Task<int> GetQueueLengthAsync(QueueTaskStatus? status = null, CancellationToken cancellationToken = default)
        {
            if (status.HasValue)
            {
                return Task.FromResult(_tasksByStatus[status.Value].Count);
            }

            return Task.FromResult(_tasks.Count);
        }

        public Task<bool> RemoveTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            if (_tasks.TryRemove(taskId, out var task))
            {
                RemoveFromStatusBag(task.Status, taskId);
                _logger.LogInformation("Removed task {TaskId}", taskId);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public async Task<bool> RequeueFailedTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            var cutoffTime = DateTime.UtcNow - olderThan;
            var failedTaskIds = _tasksByStatus[QueueTaskStatus.Failed].ToList();
            var requeuedCount = 0;

            foreach (var taskId in failedTaskIds)
            {
                if (_tasks.TryGetValue(taskId, out var task) && 
                    task.CompletedAt.HasValue && 
                    task.CompletedAt.Value < cutoffTime &&
                    task.RetryCount < task.MaxRetries)
                {
                    task.RetryCount++;
                    task.Status = QueueTaskStatus.Queued;
                    task.CompletedAt = null;
                    task.ErrorMessage = null;

                    RemoveFromStatusBag(QueueTaskStatus.Failed, taskId);
                    _tasksByStatus[QueueTaskStatus.Queued].Add(taskId);
                    _pendingQueue.Enqueue(taskId);
                    _queueSemaphore.Release();

                    requeuedCount++;
                    _logger.LogInformation("Requeued failed task {TaskId} (retry {RetryCount}/{MaxRetries})", 
                        taskId, task.RetryCount, task.MaxRetries);
                }
            }

            _logger.LogInformation("Requeued {Count} failed tasks", requeuedCount);
            return requeuedCount > 0;
        }

        public Task ClearQueueAsync(CancellationToken cancellationToken = default)
        {
            _tasks.Clear();
            
            while (_pendingQueue.TryDequeue(out _)) { }
            
            foreach (var bag in _tasksByStatus.Values)
            {
                while (bag.TryTake(out _)) { }
            }

            _logger.LogInformation("Cleared task queue");
            return Task.CompletedTask;
        }

        protected Task<IEnumerable<T>> GetAllTasksAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<T>>(_tasks.Values);
        }

        private string GetNextTaskId(bool peek = false)
        {
            // Priority-based dequeue
            var priorityOrder = new[] { TaskPriority.Critical, TaskPriority.High, TaskPriority.Normal, TaskPriority.Low };
            
            foreach (var priority in priorityOrder)
            {
                var queuedTaskIds = _tasksByStatus[QueueTaskStatus.Queued].ToList();
                var taskId = queuedTaskIds
                    .Where(id => _tasks.TryGetValue(id, out var t) && t.Priority == priority && t.Status == QueueTaskStatus.Queued)
                    .FirstOrDefault();

                if (taskId != null)
                {
                    return taskId;
                }
            }

            // Fallback to FIFO
            if (_pendingQueue.TryPeek(out var nextId))
            {
                // Double check the task is still queued
                if (_tasks.TryGetValue(nextId, out var task) && task.Status == QueueTaskStatus.Queued)
                {
                    if (!peek)
                    {
                        _pendingQueue.TryDequeue(out _); // Actually remove it
                    }
                    return nextId;
                }
                else
                {
                    // Remove stale reference and try again
                    _pendingQueue.TryDequeue(out _);
                    return GetNextTaskId(peek);
                }
            }

            return null;
        }

        private void RemoveFromStatusBag(QueueTaskStatus status, string taskId)
        {
            var bag = _tasksByStatus[status];
            var items = bag.ToList();
            while (bag.TryTake(out _)) { }
            
            foreach (var item in items.Where(id => id != taskId))
            {
                bag.Add(item);
            }
        }
    }

    // Persistent task queue implementation
    public class PersistentTaskQueueService<T> : TaskQueueService<T>, IPersistentTaskQueue<T> where T : class, IQueueTask
    {
        private readonly IDataStore _dataStore;
        private readonly string _queueName;

        public PersistentTaskQueueService(IDataStore dataStore, ILogger<PersistentTaskQueueService<T>> logger, string queueName) 
            : base(logger)
        {
            _dataStore = dataStore;
            _queueName = queueName;
        }

        public async Task<bool> SaveCheckpointAsync(string taskId, object checkpointData, CancellationToken cancellationToken = default)
        {
            var key = $"{_queueName}:checkpoint:{taskId}";
            return await _dataStore.SaveAsync(key, checkpointData, cancellationToken);
        }

        public async Task<TCheckpoint?> GetCheckpointAsync<TCheckpoint>(string taskId, CancellationToken cancellationToken = default)
        {
            var key = $"{_queueName}:checkpoint:{taskId}";
            return await _dataStore.GetAsync<TCheckpoint>(key, cancellationToken);
        }

        public async Task<object?> GetCheckpointAsync(string taskId, CancellationToken cancellationToken = default)
        {
            return await GetCheckpointAsync<object>(taskId, cancellationToken);
        }

        public async Task<bool> ClearCheckpointAsync(string taskId, CancellationToken cancellationToken = default)
        {
            var key = $"{_queueName}:checkpoint:{taskId}";
            return await _dataStore.DeleteAsync(key, cancellationToken);
        }

        public async Task<IEnumerable<T>> GetStaleTasksAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var cutoffTime = DateTime.UtcNow - timeout;
            var tasks = await GetTasksAsync(QueueTaskStatus.InProgress, cancellationToken: cancellationToken);
            
            return tasks.Where(t => t.StartedAt.HasValue && t.StartedAt.Value < cutoffTime);
        }

        public async Task<bool> ArchiveCompletedTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            var cutoffTime = DateTime.UtcNow - olderThan;
            var completedStatuses = new[] { QueueTaskStatus.Completed, QueueTaskStatus.Failed, QueueTaskStatus.Cancelled };
            var archivedCount = 0;

            foreach (var status in completedStatuses)
            {
                var tasks = await GetTasksAsync(status, cancellationToken: cancellationToken);
                
                foreach (var task in tasks.Where(t => t.CompletedAt.HasValue && t.CompletedAt.Value < cutoffTime))
                {
                    var archiveKey = $"{_queueName}:archive:{task.TaskId}";
                    if (await _dataStore.SaveAsync(archiveKey, task, cancellationToken))
                    {
                        await RemoveTaskAsync(task.TaskId, cancellationToken);
                        archivedCount++;
                    }
                }
            }

            return archivedCount > 0;
        }
    }
}