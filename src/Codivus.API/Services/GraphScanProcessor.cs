using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codivus.API.BackgroundServices;
using Codivus.API.Interfaces;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Codivus.Graph.Interfaces;
using Microsoft.Extensions.Logging;

namespace Codivus.API.Services
{
    /// <summary>
    /// Processes graph scan tasks by analyzing code files and storing results in the graph database
    /// </summary>
    public class GraphScanProcessor : IGraphScanProcessor
    {
        private readonly IRoslynAnalysisService _roslynAnalysisService;
        private readonly IGraphStorageService _graphStorage;
        private readonly IRepositoryService _repositoryService;
        private readonly ILogger<GraphScanProcessor> _logger;

        public GraphScanProcessor(
            IRoslynAnalysisService roslynAnalysisService,
            IGraphStorageService graphStorage,
            IRepositoryService repositoryService,
            ILogger<GraphScanProcessor> logger)
        {
            _roslynAnalysisService = roslynAnalysisService;
            _graphStorage = graphStorage;
            _repositoryService = repositoryService;
            _logger = logger;
        }

        public async Task ProcessTaskAsync(GraphScanTask task, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing graph scan task {TaskId} for {TargetPath}", task.TaskId, task.TargetPath);

            try
            {
                // Initialize checkpoint if not exists
                task.Checkpoint ??= new GraphScanCheckpoint();

                // Update task status to in progress
                task.Status = QueueTaskStatus.InProgress;
                task.StartedAt = DateTime.UtcNow;

                // Initialize graph if needed
                await _graphStorage.InitializeAsync();

                // Process files in the task
                await ProcessFilesAsync(task, cancellationToken);

                // Mark task as completed
                task.Status = QueueTaskStatus.Completed;
                task.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Completed graph scan task {TaskId}, processed {ProcessedFiles} files",
                    task.TaskId, task.Checkpoint.ProcessedFiles);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Graph scan task {TaskId} was cancelled", task.TaskId);
                task.Status = QueueTaskStatus.Cancelled;
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process graph scan task {TaskId}", task.TaskId);
                task.Status = QueueTaskStatus.Failed;
                task.ErrorMessage = ex.Message;
                throw;
            }
        }

        private async Task ProcessFilesAsync(GraphScanTask task, CancellationToken cancellationToken)
        {
            // Parse repository ID
            if (!Guid.TryParse(task.RepositoryId, out var repositoryGuid))
            {
                throw new ArgumentException($"Invalid repository ID format: {task.RepositoryId}");
            }

            // Get repository info
            var repository = await _repositoryService.GetRepositoryByIdAsync(repositoryGuid);
            if (repository == null)
            {
                throw new ArgumentException($"Repository {task.RepositoryId} not found");
            }

            // Get files to process
            var filesToProcess = await GetFilesToProcessAsync(task, repository, cancellationToken);

            task.Checkpoint.TotalFiles = filesToProcess.Count;
            var processedFiles = 0;

            // Process files in batches
            var batchSize = task.Options.BatchSize;
            for (int i = 0; i < filesToProcess.Count; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = filesToProcess.Skip(i).Take(batchSize).ToList();
                
                // Use transaction for batch processing
                using var transaction = await _graphStorage.BeginTransactionAsync();
                
                try
                {
                    foreach (var file in batch)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Skip if already processed (for resumable scans)
                        if (task.Checkpoint.ProcessedFileIds.Contains(file.Id.ToString()))
                        {
                            processedFiles++;
                            continue;
                        }

                        await ProcessSingleFileAsync(file, task, repository, cancellationToken);
                        
                        // Update checkpoint
                        task.Checkpoint.ProcessedFileIds.Add(file.Id.ToString());
                        task.Checkpoint.ProcessedFiles = ++processedFiles;
                        task.Checkpoint.LastProcessedFile = file.Path;

                        _logger.LogDebug("Processed file {FilePath} ({ProcessedFiles}/{TotalFiles})",
                            file.Path, processedFiles, task.Checkpoint.TotalFiles);
                    }

                    await transaction.CommitAsync();
                    
                    _logger.LogInformation("Committed batch of {BatchSize} files for task {TaskId}",
                        batch.Count, task.TaskId);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Failed to process batch for task {TaskId}, rolling back", task.TaskId);
                    
                    // Mark failed files
                    foreach (var file in batch)
                    {
                        if (!task.Checkpoint.ProcessedFileIds.Contains(file.Id.ToString()))
                        {
                            task.Checkpoint.FailedFileIds.Add(file.Id.ToString());
                        }
                    }
                    
                    throw;
                }
            }
        }

        private async Task ProcessSingleFileAsync(
            RepositoryFile file, 
            GraphScanTask task, 
            Repository repository,
            CancellationToken cancellationToken)
        {
            try
            {
                var filePath = file.Path;
                
                // Check file size limits
                if (new FileInfo(filePath).Length > task.Options.MaxFileSizeBytes)
                {
                    _logger.LogWarning("Skipping file {FilePath}: exceeds max size limit", filePath);
                    return;
                }

                // Analyze the file using Roslyn
                var analysisResult = await _roslynAnalysisService.AnalyzeFileAsync(
                    filePath, 
                    task.RepositoryId, 
                    task.ProjectId,
                    cancellationToken);

                if (analysisResult.Errors.Any())
                {
                    _logger.LogWarning("Analysis errors for {FilePath}: {Errors}", 
                        filePath, string.Join("; ", analysisResult.Errors));
                }

                // Store nodes in graph database
                if (analysisResult.Nodes.Any())
                {
                    var createdNodes = await _graphStorage.CreateNodesAsync(analysisResult.Nodes);
                    _logger.LogDebug("Created {NodeCount} nodes for {FilePath}", 
                        createdNodes.Count(), filePath);
                }

                // Store relationships if enabled
                if (task.Options.BuildRelationships && analysisResult.Relationships.Any())
                {
                    foreach (var relationship in analysisResult.Relationships)
                    {
                        await _graphStorage.CreateRelationshipAsync(relationship);
                    }
                    
                    _logger.LogDebug("Created {RelationshipCount} relationships for {FilePath}", 
                        analysisResult.Relationships.Count, filePath);
                }

                // Update task statistics
                task.Checkpoint.State["nodesCreated"] = 
                    (task.Checkpoint.State.ContainsKey("nodesCreated") ? 
                        (int)task.Checkpoint.State["nodesCreated"] : 0) + analysisResult.Nodes.Count;
                        
                task.Checkpoint.State["relationshipsCreated"] = 
                    (task.Checkpoint.State.ContainsKey("relationshipsCreated") ? 
                        (int)task.Checkpoint.State["relationshipsCreated"] : 0) + analysisResult.Relationships.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process file {FilePath}", file.Path);
                task.Checkpoint.FailedFileIds.Add(file.Id.ToString());
                
                // Don't fail the entire task for single file errors
                if (task.Options.ContinueOnError)
                {
                    return;
                }
                
                throw;
            }
        }

        private async Task<List<RepositoryFile>> GetFilesToProcessAsync(
            GraphScanTask task, 
            Repository repository,
            CancellationToken cancellationToken)
        {
            var files = new List<RepositoryFile>();

            _logger.LogInformation("Getting files to process for task {TaskId} with scope {Scope} and target path {TargetPath}", 
                task.TaskId, task.Scope, task.TargetPath);

            switch (task.Scope)
            {
                case ScanScope.File:
                    // Single file specified in TargetPath
                    var singleFile = await FindFileByPathAsync(repository, task.TargetPath);
                    if (singleFile != null)
                        files.Add(singleFile);
                    break;

                case ScanScope.Directory:
                    // All eligible files in the directory
                    var directoryFiles = await GetFilesInDirectoryAsync(repository, task.TargetPath);
                    files.AddRange(directoryFiles.Where(f => IsFileEligible(f, task.Options)));
                    break;

                case ScanScope.Project:
                    // All files in the project
                    var projectFiles = await GetFilesInProjectAsync(repository, task.ProjectId);
                    files.AddRange(projectFiles.Where(f => IsFileEligible(f, task.Options)));
                    break;

                case ScanScope.Repository:
                    // All eligible files in the repository
                    var allFiles = await GetAllRepositoryFilesAsync(repository);
                    var eligibleFiles = allFiles.Where(f => IsFileEligible(f, task.Options)).ToList();
                    _logger.LogInformation("Found {EligibleFiles} eligible files out of {TotalFiles} total files", 
                        eligibleFiles.Count, allFiles.Count);
                    files.AddRange(eligibleFiles);
                    break;
            }

            // Note: Skipping FileIds filtering since RepositoryFile IDs are regenerated each scan
            // The eligibility filtering above (extensions, patterns, etc.) provides sufficient filtering
            if (task.FileIds.Any())
            {
                _logger.LogInformation("Skipping FileIds filtering - using eligibility filters instead. Task had {FileIdCount} file IDs", task.FileIds.Count);
            }

            _logger.LogInformation("Returning {FileCount} files to process for task {TaskId}", files.Count, task.TaskId);
            return files;
        }

        private async Task<RepositoryFile?> FindFileByPathAsync(Repository repository, string path)
        {
            // This is a simplified implementation - in practice you'd query your file storage
            var structure = await _repositoryService.GetRepositoryStructureAsync(repository.Id);
            return FindFileInStructure(structure, path);
        }

        private RepositoryFile? FindFileInStructure(RepositoryFile root, string path)
        {
            if (root.Path == path)
                return root;

            if (root.Children != null)
            {
                foreach (var child in root.Children)
                {
                    var found = FindFileInStructure(child, path);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        private async Task<List<RepositoryFile>> GetFilesInDirectoryAsync(Repository repository, string directoryPath)
        {
            var structure = await _repositoryService.GetRepositoryStructureAsync(repository.Id);
            var directory = FindFileInStructure(structure, directoryPath);
            
            if (directory?.Children == null)
                return new List<RepositoryFile>();

            return FlattenFiles(directory).Where(f => !f.IsDirectory).ToList();
        }

        private async Task<List<RepositoryFile>> GetFilesInProjectAsync(Repository repository, string? projectId)
        {
            // For now, treat project as directory scan
            // In a more sophisticated implementation, you'd parse project files
            var structure = await _repositoryService.GetRepositoryStructureAsync(repository.Id);
            return FlattenFiles(structure).Where(f => !f.IsDirectory).ToList();
        }

        private async Task<List<RepositoryFile>> GetAllRepositoryFilesAsync(Repository repository)
        {
            var structure = await _repositoryService.GetRepositoryStructureAsync(repository.Id);
            var allFiles = FlattenFiles(structure).Where(f => !f.IsDirectory).ToList();
            _logger.LogInformation("Found {TotalFiles} files in repository structure", allFiles.Count);
            return allFiles;
        }

        private List<RepositoryFile> FlattenFiles(RepositoryFile? root)
        {
            var files = new List<RepositoryFile>();
            
            if (root == null)
                return files;
                
            if (!root.IsDirectory)
            {
                files.Add(root);
            }
            else if (root.Children != null)
            {
                foreach (var child in root.Children)
                {
                    files.AddRange(FlattenFiles(child));
                }
            }
            
            return files;
        }

        private static bool IsFileEligible(RepositoryFile file, GraphScanOptions options)
        {
            var extension = Path.GetExtension(file.Path);
            
            // Check for supported file extensions
            if (!options.SupportedExtensions.Any(ext => extension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Check include patterns
            if (options.IncludePatterns.Any() && 
                !options.IncludePatterns.Any(pattern => MatchesPattern(file.Path, pattern)))
                return false;

            // Check exclude patterns
            if (options.ExcludePatterns.Any(pattern => MatchesPattern(file.Path, pattern)))
                return false;

            // Check test files
            if (!options.IncludeTests && IsTestFile(file.Path))
                return false;

            // Check generated code
            if (!options.AnalyzeGeneratedCode && IsGeneratedFile(file.Path))
                return false;

            return true;
        }

        private static bool MatchesPattern(string path, string pattern)
        {
            // Simple pattern matching - in production, use a proper glob library
            return path.Contains(pattern.Replace("*", ""), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTestFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Contains("Tests", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("test", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGeneratedFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName.Contains(".g.cs") ||
                   fileName.Contains(".designer.cs") ||
                   fileName.Contains(".generated.cs") ||
                   path.Contains("obj/", StringComparison.OrdinalIgnoreCase);
        }
    }
}