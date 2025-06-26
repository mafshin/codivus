using System;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Codivus.Graph.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Codivus.API.BackgroundServices
{
    public class GraphMaintenanceWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GraphMaintenanceWorker> _logger;
        private readonly GraphMaintenanceOptions _options;

        public GraphMaintenanceWorker(
            IServiceProvider serviceProvider, 
            ILogger<GraphMaintenanceWorker> logger,
            IOptions<GraphMaintenanceOptions> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Graph Maintenance Worker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var taskQueue = scope.ServiceProvider.GetService<IPersistentTaskQueue<GraphScanTask>>();
                    var graphStorage = scope.ServiceProvider.GetService<Codivus.Graph.Interfaces.IGraphStorageService>();

                    if (taskQueue != null)
                    {
                        await PerformTaskMaintenanceAsync(taskQueue, stoppingToken);
                    }

                    if (graphStorage != null)
                    {
                        await PerformGraphMaintenanceAsync(graphStorage, stoppingToken);
                    }

                    // Wait for next maintenance cycle
                    await Task.Delay(_options.MaintenanceIntervalMinutes * 60 * 1000, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during graph maintenance");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("Graph Maintenance Worker stopped");
        }

        private async Task PerformTaskMaintenanceAsync(IPersistentTaskQueue<GraphScanTask> taskQueue, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Starting task maintenance");

                // Clean up stale tasks (tasks that have been running too long)
                var staleTimeout = TimeSpan.FromMinutes(_options.StaleTaskTimeoutMinutes);
                var staleTasks = await taskQueue.GetStaleTasksAsync(staleTimeout, cancellationToken);
                
                int staleCount = 0;
                foreach (var task in staleTasks)
                {
                    _logger.LogWarning("Found stale task {TaskId} running since {StartTime}", 
                        task.TaskId, task.StartedAt);
                    
                    await taskQueue.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Failed, 
                        "Task marked as stale due to timeout", cancellationToken);
                    staleCount++;
                }

                if (staleCount > 0)
                {
                    _logger.LogInformation("Marked {Count} stale tasks as failed", staleCount);
                }

                // Requeue failed tasks that are eligible for retry
                var retryAge = TimeSpan.FromMinutes(_options.RetryDelayMinutes);
                var requeuedAny = await taskQueue.RequeueFailedTasksAsync(retryAge, cancellationToken);
                
                if (requeuedAny)
                {
                    _logger.LogInformation("Requeued eligible failed tasks for retry");
                }

                // Archive old completed tasks
                if (_options.ArchiveCompletedTasksAfterDays > 0)
                {
                    var archiveAge = TimeSpan.FromDays(_options.ArchiveCompletedTasksAfterDays);
                    var archivedAny = await taskQueue.ArchiveCompletedTasksAsync(archiveAge, cancellationToken);
                    
                    if (archivedAny)
                    {
                        _logger.LogInformation("Archived old completed tasks");
                    }
                }

                _logger.LogDebug("Task maintenance completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during task maintenance");
            }
        }

        private async Task PerformGraphMaintenanceAsync(Codivus.Graph.Interfaces.IGraphStorageService graphStorage, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Starting graph maintenance");

                // Optimize graph indices
                if (_options.OptimizeIndices)
                {
                    await graphStorage.OptimizeIndicesAsync(cancellationToken);
                    _logger.LogDebug("Graph indices optimized");
                }

                // Clean up orphaned nodes
                if (_options.CleanupOrphanedNodes)
                {
                    var orphanedCount = await graphStorage.CleanupOrphanedNodesAsync(cancellationToken);
                    if (orphanedCount > 0)
                    {
                        _logger.LogInformation("Cleaned up {Count} orphaned graph nodes", orphanedCount);
                    }
                }

                // Defragment graph storage
                if (_options.DefragmentStorage)
                {
                    await graphStorage.DefragmentStorageAsync(cancellationToken);
                    _logger.LogDebug("Graph storage defragmented");
                }

                // Update graph statistics
                await graphStorage.UpdateStatisticsAsync(cancellationToken);

                _logger.LogDebug("Graph maintenance completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during graph maintenance");
            }
        }
    }

    public class GraphMaintenanceOptions
    {
        public const string SectionName = "GraphMaintenance";

        public int MaintenanceIntervalMinutes { get; set; } = 60;
        public int StaleTaskTimeoutMinutes { get; set; } = 120;
        public int RetryDelayMinutes { get; set; } = 30;
        public int ArchiveCompletedTasksAfterDays { get; set; } = 7;
        public bool OptimizeIndices { get; set; } = true;
        public bool CleanupOrphanedNodes { get; set; } = true;
        public bool DefragmentStorage { get; set; } = false;
    }

}