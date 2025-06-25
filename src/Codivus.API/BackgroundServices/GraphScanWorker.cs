using System;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Codivus.API.BackgroundServices
{
    public class GraphScanWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GraphScanWorker> _logger;
        private readonly int _workerCount;

        public GraphScanWorker(IServiceProvider serviceProvider, ILogger<GraphScanWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _workerCount = Environment.ProcessorCount;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Graph Scan Worker started with {WorkerCount} workers", _workerCount);

            var workers = new Task[_workerCount];
            
            for (int i = 0; i < _workerCount; i++)
            {
                var workerId = i;
                workers[i] = Task.Run(async () => await ProcessTasksAsync(workerId, stoppingToken), stoppingToken);
            }

            await Task.WhenAll(workers);
            
            _logger.LogInformation("Graph Scan Worker stopped");
        }

        private async Task ProcessTasksAsync(int workerId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker {WorkerId} started", workerId);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var taskQueue = scope.ServiceProvider.GetRequiredService<ITaskQueue<GraphScanTask>>();
                    var processor = scope.ServiceProvider.GetRequiredService<IGraphScanProcessor>();

                    // Get next task with timeout
                    var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                    GraphScanTask task = null;
                    try
                    {
                        task = await taskQueue.DequeueAsync(timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Timeout - continue to check for cancellation
                        continue;
                    }

                    if (task == null)
                        continue;

                    _logger.LogInformation("Worker {WorkerId} processing task {TaskId} for {TargetPath}",
                        workerId, task.TaskId, task.TargetPath);

                    try
                    {
                        // Process the task
                        await processor.ProcessTaskAsync(task, cancellationToken);
                        
                        // Mark as completed
                        await taskQueue.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Completed);
                        
                        _logger.LogInformation("Worker {WorkerId} completed task {TaskId}", workerId, task.TaskId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Worker {WorkerId} failed to process task {TaskId}", workerId, task.TaskId);
                        
                        // Update task status
                        await taskQueue.UpdateTaskStatusAsync(
                            task.TaskId, 
                            QueueTaskStatus.Failed, 
                            ex.Message);

                        // Requeue if retries available
                        if (task.RetryCount < task.MaxRetries)
                        {
                            task.RetryCount++;
                            await taskQueue.EnqueueAsync(task, cancellationToken);
                            _logger.LogInformation("Requeued task {TaskId} (retry {RetryCount}/{MaxRetries})",
                                task.TaskId, task.RetryCount, task.MaxRetries);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker {WorkerId} encountered an error", workerId);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            _logger.LogInformation("Worker {WorkerId} stopped", workerId);
        }
    }

    // Interface for the actual processing logic
    public interface IGraphScanProcessor
    {
        Task ProcessTaskAsync(GraphScanTask task, CancellationToken cancellationToken);
    }
}