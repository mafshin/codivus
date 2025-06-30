using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codivus.API.Interfaces;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.API.Services
{
    public class GraphScanOrchestrator : IGraphScanOrchestrator
    {
        private readonly ITaskQueue<GraphScanTask> _taskQueue;
        private readonly IRepositoryService _repositoryService;
        private readonly ILogger<GraphScanOrchestrator> _logger;
        private readonly Dictionary<string, GraphScanProgress> _scanProgress = new();
        private readonly SemaphoreSlim _progressLock = new(1, 1);

        public GraphScanOrchestrator(
            ITaskQueue<GraphScanTask> taskQueue,
            IRepositoryService repositoryService,
            ILogger<GraphScanOrchestrator> logger)
        {
            _taskQueue = taskQueue;
            _repositoryService = repositoryService;
            _logger = logger;
        }

        public async Task<string> StartGraphScanAsync(
            string repositoryId, 
            GraphScanConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            var scanId = Guid.NewGuid().ToString();
            
            // Parse repositoryId to Guid for existing interface
            if (!Guid.TryParse(repositoryId, out var repoGuid))
            {
                throw new ArgumentException($"Invalid repository ID format: {repositoryId}");
            }
            
            var repository = await _repositoryService.GetRepositoryByIdAsync(repoGuid);
            
            if (repository == null)
            {
                throw new ArgumentException($"Repository {repositoryId} not found");
            }

            // Initialize scan progress
            var progress = new GraphScanProgress
            {
                ScanId = scanId,
                RepositoryId = repositoryId,
                Status = ScanStatus.Initializing
            };

            await _progressLock.WaitAsync(cancellationToken);
            try
            {
                _scanProgress[scanId] = progress;
            }
            finally
            {
                _progressLock.Release();
            }

            // Create scan tasks based on configuration
            var tasks = await CreateScanTasksAsync(repository, scanId, configuration, cancellationToken);
            
            progress.TotalTasks = tasks.Count();
            progress.Status = ScanStatus.Pending;

            // Enqueue all tasks
            foreach (var task in tasks)
            {
                await _taskQueue.EnqueueAsync(task, cancellationToken);
            }

            _logger.LogInformation("Started graph scan {ScanId} for repository {RepositoryId} with {TaskCount} tasks",
                scanId, repositoryId, tasks.Count);

            return scanId;
        }

        private List<RepositoryFile> FlattenRepositoryFiles(RepositoryFile root)
        {
            var files = new List<RepositoryFile>();
            
            if (!root.IsDirectory)
            {
                files.Add(root);
            }
            else if (root.Children != null)
            {
                foreach (var child in root.Children)
                {
                    files.AddRange(FlattenRepositoryFiles(child));
                }
            }
            
            return files;
        }

        public async Task<GraphScanProgress?> GetScanProgressAsync(string scanId)
        {
            await _progressLock.WaitAsync();
            try
            {
                if (_scanProgress.TryGetValue(scanId, out var progress))
                {
                    // Update progress from task queue
                    var tasks = await _taskQueue.GetTasksAsync();
                    var scanTasks = tasks.OfType<GraphScanTask>().Where(t => t.ScanId == scanId).ToList();

                    progress.CompletedTasks = scanTasks.Count(t => t.Status == QueueTaskStatus.Completed);
                    progress.FailedTasks = scanTasks.Count(t => t.Status == QueueTaskStatus.Failed);
                    
                    if (scanTasks.Any(t => t.Status == QueueTaskStatus.InProgress))
                    {
                        progress.Status = ScanStatus.InProgress;
                        progress.CurrentTask = scanTasks.FirstOrDefault(t => t.Status == QueueTaskStatus.InProgress)?.TargetPath;
                    }
                    else if (progress.CompletedTasks + progress.FailedTasks >= progress.TotalTasks)
                    {
                        progress.Status = progress.FailedTasks > 0 ? ScanStatus.Failed : ScanStatus.Completed;
                    }

                    // Calculate processed files
                    progress.ProcessedFiles = scanTasks
                        .Where(t => t.Status == QueueTaskStatus.Completed)
                        .Sum(t => t.Checkpoint?.ProcessedFiles ?? 0);

                    return progress;
                }

                return null;
            }
            finally
            {
                _progressLock.Release();
            }
        }

        public async Task<bool> PauseScanAsync(string scanId, CancellationToken cancellationToken = default)
        {
            var tasks = await _taskQueue.GetTasksAsync();
            var scanTasks = tasks.OfType<GraphScanTask>()
                .Where(t => t.ScanId == scanId && t.Status == QueueTaskStatus.Queued)
                .ToList();

            foreach (var task in scanTasks)
            {
                await _taskQueue.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Paused, cancellationToken: cancellationToken);
            }

            await UpdateScanStatusAsync(scanId, ScanStatus.Paused);
            
            _logger.LogInformation("Paused scan {ScanId}", scanId);
            return true;
        }

        public async Task<bool> ResumeScanAsync(string scanId, CancellationToken cancellationToken = default)
        {
            var tasks = await _taskQueue.GetTasksAsync();
            var scanTasks = tasks.OfType<GraphScanTask>()
                .Where(t => t.ScanId == scanId && t.Status == QueueTaskStatus.Paused)
                .ToList();

            foreach (var task in scanTasks)
            {
                await _taskQueue.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Queued, cancellationToken: cancellationToken);
            }

            await UpdateScanStatusAsync(scanId, ScanStatus.InProgress);
            
            _logger.LogInformation("Resumed scan {ScanId}", scanId);
            return true;
        }

        public async Task<bool> CancelScanAsync(string scanId, CancellationToken cancellationToken = default)
        {
            var tasks = await _taskQueue.GetTasksAsync();
            var scanTasks = tasks.OfType<GraphScanTask>()
                .Where(t => t.ScanId == scanId && 
                       (t.Status == QueueTaskStatus.Queued || t.Status == QueueTaskStatus.Paused))
                .ToList();

            foreach (var task in scanTasks)
            {
                await _taskQueue.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Cancelled, cancellationToken: cancellationToken);
            }

            await UpdateScanStatusAsync(scanId, ScanStatus.Canceled);
            
            _logger.LogInformation("Cancelled scan {ScanId}", scanId);
            return true;
        }

        private async Task<List<GraphScanTask>> CreateScanTasksAsync(
            Repository repository,
            string scanId,
            GraphScanConfiguration configuration,
            CancellationToken cancellationToken)
        {
            var tasks = new List<GraphScanTask>();

            switch (configuration.Mode)
            {
                case ScanMode.Full:
                    tasks.AddRange(await CreateFullScanTasksAsync(repository, scanId, configuration, cancellationToken));
                    break;
                    
                case ScanMode.Incremental:
                    tasks.AddRange(await CreateIncrementalScanTasksAsync(repository, scanId, configuration, cancellationToken));
                    break;
                    
                case ScanMode.Differential:
                    tasks.AddRange(await CreateDifferentialScanTasksAsync(repository, scanId, configuration, cancellationToken));
                    break;
            }

            return tasks;
        }

        private async Task<List<GraphScanTask>> CreateFullScanTasksAsync(
            Repository repository,
            string scanId,
            GraphScanConfiguration configuration,
            CancellationToken cancellationToken)
        {
            var tasks = new List<GraphScanTask>();
            
            // Placeholder: Use existing GetRepositoryStructureAsync and flatten it
            var rootStructure = await _repositoryService.GetRepositoryStructureAsync(repository.Id);
            var files = FlattenRepositoryFiles(rootStructure);
            
            // Filter files based on configuration
            var eligibleFiles = files.Where(f => IsFileEligible(f.Path, configuration.Analysis)).ToList();
            
            // Group files by project or directory for batching
            var fileGroups = eligibleFiles
                .GroupBy(f => Path.GetDirectoryName(f.Path))
                .Where(g => g.Count() > 0);

            foreach (var group in fileGroups)
            {
                var batch = new List<RepositoryFile>();
                
                foreach (var file in group)
                {
                    batch.Add(file);
                    
                    if (batch.Count >= configuration.Processing.BatchSize)
                    {
                        tasks.Add(CreateScanTask(repository, scanId, batch, configuration));
                        batch = new List<RepositoryFile>();
                    }
                }
                
                if (batch.Any())
                {
                    tasks.Add(CreateScanTask(repository, scanId, batch, configuration));
                }
            }

            return tasks;
        }

        private Task<List<GraphScanTask>> CreateIncrementalScanTasksAsync(
            Repository repository,
            string scanId,
            GraphScanConfiguration configuration,
            CancellationToken cancellationToken)
        {
            // TODO: Implement incremental scanning based on file changes
            return CreateFullScanTasksAsync(repository, scanId, configuration, cancellationToken);
        }

        private Task<List<GraphScanTask>> CreateDifferentialScanTasksAsync(
            Repository repository,
            string scanId,
            GraphScanConfiguration configuration,
            CancellationToken cancellationToken)
        {
            // TODO: Implement differential scanning
            return CreateFullScanTasksAsync(repository, scanId, configuration, cancellationToken);
        }

        private GraphScanTask CreateScanTask(
            Repository repository,
            string scanId,
            List<RepositoryFile> files,
            GraphScanConfiguration configuration)
        {
            return new GraphScanTask
            {
                RepositoryId = repository.Id.ToString(),
                ScanId = scanId,
                Scope = ScanScope.Repository,
                TargetPath = "",
                FileIds = files.Select(f => f.Id.ToString()).ToList(),
                Options = new GraphScanOptions
                {
                    FullScan = configuration.Mode == ScanMode.Full,
                    IncludeTests = configuration.Analysis.AnalyzeTests,
                    AnalyzeGeneratedCode = configuration.Analysis.AnalyzeGeneratedCode,
                    MaxFileSizeBytes = configuration.Analysis.MaxFileSizeMB * 1024 * 1024,
                    BuildRelationships = configuration.Relationships.TrackCalls,
                    CalculateMetrics = configuration.Metrics.CalculateComplexity,
                    BatchSize = configuration.Processing.BatchSize,
                    ContinueOnError = configuration.Processing.ContinueOnError,
                    SupportedExtensions = new List<string>(configuration.Analysis.IncludedExtensions),
                    ExcludePatterns = new List<string>(configuration.Analysis.ExcludedPatterns)
                },
                Priority = TaskPriority.Normal,
                MaxRetries = 3
            };
        }

        private bool IsFileEligible(string filePath, AnalysisConfiguration config)
        {
            var extension = Path.GetExtension(filePath);
            
            // Check if extension is included
            if (!config.IncludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return false;

            // Check exclusion patterns
            foreach (var pattern in config.ExcludedPatterns)
            {
                if (MatchesPattern(filePath, pattern))
                    return false;
            }

            return true;
        }

        private bool MatchesPattern(string path, string pattern)
        {
            // Simple pattern matching implementation
            // TODO: Implement proper glob pattern matching
            return path.Contains(pattern.Replace("**", "").Replace("*", ""));
        }

        private async Task UpdateScanStatusAsync(string scanId, ScanStatus status)
        {
            await _progressLock.WaitAsync();
            try
            {
                if (_scanProgress.TryGetValue(scanId, out var progress))
                {
                    progress.Status = status;
                }
            }
            finally
            {
                _progressLock.Release();
            }
        }
    }
}