using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// Service for analyzing code differences and planning incremental updates
    /// </summary>
    public class DiffAnalysisService : IDiffAnalysisService
    {
        private readonly ILogger<DiffAnalysisService> _logger;
        private readonly IGraphQueryService _graphQueryService;

        public DiffAnalysisService(
            ILogger<DiffAnalysisService> logger,
            IGraphQueryService graphQueryService)
        {
            _logger = logger;
            _graphQueryService = graphQueryService;
        }

        public async Task<FileDiffAnalysis> AnalyzeFileDiffAsync(string filePath, string oldContent, string newContent, CancellationToken cancellationToken = default)
        {
            var analysis = new FileDiffAnalysis
            {
                FilePath = filePath,
                AnalyzedAt = DateTime.UtcNow,
                Checksum = ComputeChecksum(newContent),
                PreviousChecksum = ComputeChecksum(oldContent)
            };

            try
            {
                // Basic line-level analysis
                var oldLines = oldContent.Split('\n');
                var newLines = newContent.Split('\n');
                
                AnalyzeLineChanges(oldLines, newLines, analysis);

                // If it's a C# file, do deeper analysis
                if (Path.GetExtension(filePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    await AnalyzeCSharpChangesAsync(oldContent, newContent, analysis, cancellationToken);
                }

                // Determine overall change type
                analysis.ChangeType = DetermineChangeType(analysis);

                _logger.LogDebug("Analyzed diff for {FilePath}: {ChangeType}, {ElementChanges} element changes", 
                    filePath, analysis.ChangeType, analysis.ElementChanges.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing diff for file {FilePath}", filePath);
                analysis.ChangeType = ChangeType.Modified; // Fallback
            }

            return analysis;
        }

        public async Task<RepositoryDiffAnalysis> AnalyzeRepositoryChangesAsync(string repositoryId, string repositoryPath, DateTime? lastScanTime = null, CancellationToken cancellationToken = default)
        {
            var analysis = new RepositoryDiffAnalysis
            {
                RepositoryId = repositoryId,
                AnalyzedAt = DateTime.UtcNow,
                LastScanTime = lastScanTime
            };

            try
            {
                // Get all relevant files
                var supportedExtensions = new[] { ".cs", ".vb", ".fs", ".csproj", ".vbproj", ".fsproj", ".sln" };
                var allFiles = Directory.GetFiles(repositoryPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .Where(f => !ShouldExcludeFile(f))
                    .ToList();

                // Analyze each file if modified since last scan
                foreach (var filePath in allFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileInfo = new FileInfo(filePath);
                    if (lastScanTime.HasValue && fileInfo.LastWriteTime <= lastScanTime.Value)
                    {
                        continue; // File hasn't changed
                    }

                    var relativePath = Path.GetRelativePath(repositoryPath, filePath);
                    
                    // For new analysis, we can only detect that the file has changed
                    // We would need previous content from cache/git to do proper diff
                    var fileDiff = new FileDiffAnalysis
                    {
                        FilePath = relativePath,
                        AnalyzedAt = DateTime.UtcNow,
                        ChangeType = ChangeType.Modified,
                        Checksum = await ComputeFileChecksumAsync(filePath)
                    };

                    analysis.FileChanges.Add(fileDiff);
                }

                // Analyze project files for dependency changes
                var projectFiles = allFiles.Where(f => Path.GetExtension(f).EndsWith("proj", StringComparison.OrdinalIgnoreCase));
                foreach (var projectFile in projectFiles)
                {
                    await AnalyzeProjectChangesAsync(projectFile, repositoryPath, analysis, cancellationToken);
                }

                // Compile statistics
                CompileChangeStatistics(analysis);

                _logger.LogInformation("Repository diff analysis for {RepositoryId}: {FileCount} files changed, {ProjectCount} projects affected", 
                    repositoryId, analysis.FileChanges.Count, analysis.ProjectChanges.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing repository changes for {RepositoryId}", repositoryId);
            }

            return analysis;
        }

        public async Task<IncrementalUpdatePlan> CreateUpdatePlanAsync(RepositoryDiffAnalysis diffAnalysis, CancellationToken cancellationToken = default)
        {
            var plan = new IncrementalUpdatePlan
            {
                RepositoryId = diffAnalysis.RepositoryId,
                CreatedAt = DateTime.UtcNow,
                Priority = DetermineUpdatePriority(diffAnalysis)
            };

            try
            {
                // Plan operations for each changed file
                foreach (var fileChange in diffAnalysis.FileChanges)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var operations = await CreateFileUpdateOperationsAsync(fileChange, cancellationToken);
                    plan.Operations.AddRange(operations);

                    // Add file to reanalysis list
                    plan.FilesToReanalyze.Add(fileChange.FilePath);

                    // Plan cache invalidation
                    var cacheKeys = GenerateCacheKeys(diffAnalysis.RepositoryId, fileChange.FilePath);
                    plan.CacheKeysToInvalidate.AddRange(cacheKeys);
                }

                // Plan project-level updates
                foreach (var projectChange in diffAnalysis.ProjectChanges.Values)
                {
                    var projectOps = await CreateProjectUpdateOperationsAsync(projectChange, cancellationToken);
                    plan.Operations.AddRange(projectOps);
                }

                // Estimate duration based on operations
                plan.EstimatedDuration = EstimateUpdateDuration(plan);

                // Order operations by dependencies and priority
                plan.Operations = OrderOperationsByDependencies(plan.Operations);

                _logger.LogDebug("Created update plan for {RepositoryId}: {OperationCount} operations, estimated duration {Duration}", 
                    diffAnalysis.RepositoryId, plan.Operations.Count, plan.EstimatedDuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating update plan for repository {RepositoryId}", diffAnalysis.RepositoryId);
            }

            return plan;
        }

        public async Task<ChangeImpactAnalysis> AnalyzeChangeImpactAsync(string repositoryId, IEnumerable<string> changedFiles, CancellationToken cancellationToken = default)
        {
            var analysis = new ChangeImpactAnalysis
            {
                RepositoryId = repositoryId,
                AnalyzedAt = DateTime.UtcNow
            };

            try
            {
                foreach (var filePath in changedFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Get nodes directly affected by this file
                    var directNodes = await GetNodesForFileAsync(repositoryId, filePath, cancellationToken);
                    analysis.DirectlyAffectedNodes.AddRange(directNodes);

                    // Get indirectly affected nodes through relationships
                    var indirectNodes = await GetIndirectlyAffectedNodesAsync(repositoryId, directNodes, cancellationToken);
                    analysis.IndirectlyAffectedNodes.AddRange(indirectNodes);
                }

                // Remove duplicates
                analysis.DirectlyAffectedNodes = analysis.DirectlyAffectedNodes.Distinct().ToList();
                analysis.IndirectlyAffectedNodes = analysis.IndirectlyAffectedNodes.Distinct().ToList();

                // Analyze relationships
                analysis.AffectedRelationships = await GetAffectedRelationshipsAsync(
                    repositoryId, analysis.DirectlyAffectedNodes, cancellationToken);

                // Calculate impact metrics
                analysis.ImpactByNodeType = await CalculateImpactByNodeTypeAsync(
                    repositoryId, analysis.DirectlyAffectedNodes, cancellationToken);

                // Determine severity
                analysis.Severity = DetermineImpactSeverity(analysis);

                // Find ripple effects
                analysis.RippleEffects = await FindRippleEffectsAsync(
                    repositoryId, analysis.DirectlyAffectedNodes, cancellationToken);

                _logger.LogDebug("Change impact analysis for {RepositoryId}: {DirectCount} direct, {IndirectCount} indirect nodes affected, severity {Severity}", 
                    repositoryId, analysis.DirectlyAffectedNodes.Count, analysis.IndirectlyAffectedNodes.Count, analysis.Severity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing change impact for repository {RepositoryId}", repositoryId);
            }

            return analysis;
        }

        public async Task<Interfaces.DependencyGraph> GetFileDependencyGraphAsync(string repositoryId, string filePath, CancellationToken cancellationToken = default)
        {
            var graph = new Interfaces.DependencyGraph
            {
                RootFile = filePath
            };

            try
            {
                // Get dependencies (files this file depends on)
                graph.Dependencies = await GetFileDependenciesAsync(repositoryId, filePath, cancellationToken);

                // Get dependents (files that depend on this file)
                graph.Dependents = await GetFileDependentsAsync(repositoryId, filePath, cancellationToken);

                // Calculate dependency levels
                graph.DependencyLevels = CalculateDependencyLevels(graph);
                graph.MaxDepth = graph.DependencyLevels.Values.DefaultIfEmpty(0).Max();

                _logger.LogDebug("Built dependency graph for {FilePath}: {DependencyCount} dependencies, {DependentCount} dependents, max depth {MaxDepth}", 
                    filePath, graph.Dependencies.Count, graph.Dependents.Count, graph.MaxDepth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building dependency graph for file {FilePath} in repository {RepositoryId}", filePath, repositoryId);
            }

            return graph;
        }

        private void AnalyzeLineChanges(string[] oldLines, string[] newLines, FileDiffAnalysis analysis)
        {
            // Simple line-by-line comparison (could be enhanced with better diff algorithm)
            var maxLines = Math.Max(oldLines.Length, newLines.Length);
            
            for (int i = 0; i < maxLines; i++)
            {
                var oldLine = i < oldLines.Length ? oldLines[i] : null;
                var newLine = i < newLines.Length ? newLines[i] : null;

                if (oldLine == null && newLine != null)
                {
                    analysis.LinesAdded++;
                }
                else if (oldLine != null && newLine == null)
                {
                    analysis.LinesRemoved++;
                }
                else if (oldLine != null && newLine != null && !oldLine.Equals(newLine))
                {
                    analysis.LinesModified++;
                }
            }
        }

        private async Task AnalyzeCSharpChangesAsync(string oldContent, string newContent, FileDiffAnalysis analysis, CancellationToken cancellationToken)
        {
            try
            {
                var oldTree = CSharpSyntaxTree.ParseText(oldContent);
                var newTree = CSharpSyntaxTree.ParseText(newContent);

                var oldRoot = await oldTree.GetRootAsync(cancellationToken);
                var newRoot = await newTree.GetRootAsync(cancellationToken);

                // Extract symbols from both versions
                var oldSymbols = ExtractSymbols(oldRoot);
                var newSymbols = ExtractSymbols(newRoot);

                // Compare symbols
                CompareSymbols(oldSymbols, newSymbols, analysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse C# content for detailed analysis");
            }
        }

        private Dictionary<string, (CodeElementType Type, string Signature, int Line)> ExtractSymbols(SyntaxNode root)
        {
            var symbols = new Dictionary<string, (CodeElementType Type, string Signature, int Line)>();

            // Extract classes, interfaces, etc.
            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var name = typeDecl.Identifier.ValueText;
                var type = typeDecl switch
                {
                    ClassDeclarationSyntax => CodeElementType.Class,
                    InterfaceDeclarationSyntax => CodeElementType.Interface,
                    StructDeclarationSyntax => CodeElementType.Struct,
                    _ => CodeElementType.Class
                };
                var line = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                symbols[name] = (type, typeDecl.ToString().Split('\n')[0].Trim(), line);
            }

            // Extract methods
            foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var name = methodDecl.Identifier.ValueText;
                var signature = $"{methodDecl.ReturnType} {name}({string.Join(", ", methodDecl.ParameterList.Parameters)})";
                var line = methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                symbols[$"method:{name}"] = (CodeElementType.Method, signature, line);
            }

            // Extract properties
            foreach (var propDecl in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                var name = propDecl.Identifier.ValueText;
                var signature = $"{propDecl.Type} {name}";
                var line = propDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                symbols[$"property:{name}"] = (CodeElementType.Property, signature, line);
            }

            return symbols;
        }

        private void CompareSymbols(
            Dictionary<string, (CodeElementType Type, string Signature, int Line)> oldSymbols,
            Dictionary<string, (CodeElementType Type, string Signature, int Line)> newSymbols,
            FileDiffAnalysis analysis)
        {
            // Find added symbols
            foreach (var kvp in newSymbols)
            {
                if (!oldSymbols.ContainsKey(kvp.Key))
                {
                    analysis.AddedSymbols.Add(kvp.Key);
                    analysis.ElementChanges.Add(new CodeElementChange
                    {
                        ElementId = kvp.Key,
                        ElementName = kvp.Key,
                        ElementType = kvp.Value.Type,
                        ChangeType = ChangeType.Added,
                        NewSignature = kvp.Value.Signature,
                        LineNumber = kvp.Value.Line
                    });
                }
            }

            // Find removed symbols
            foreach (var kvp in oldSymbols)
            {
                if (!newSymbols.ContainsKey(kvp.Key))
                {
                    analysis.RemovedSymbols.Add(kvp.Key);
                    analysis.ElementChanges.Add(new CodeElementChange
                    {
                        ElementId = kvp.Key,
                        ElementName = kvp.Key,
                        ElementType = kvp.Value.Type,
                        ChangeType = ChangeType.Deleted,
                        OldSignature = kvp.Value.Signature,
                        LineNumber = kvp.Value.Line
                    });
                }
            }

            // Find modified symbols
            foreach (var kvp in newSymbols)
            {
                if (oldSymbols.TryGetValue(kvp.Key, out var oldSymbol))
                {
                    if (oldSymbol.Signature != kvp.Value.Signature)
                    {
                        analysis.ModifiedSymbols.Add(kvp.Key);
                        analysis.ElementChanges.Add(new CodeElementChange
                        {
                            ElementId = kvp.Key,
                            ElementName = kvp.Key,
                            ElementType = kvp.Value.Type,
                            ChangeType = ChangeType.Modified,
                            OldSignature = oldSymbol.Signature,
                            NewSignature = kvp.Value.Signature,
                            LineNumber = kvp.Value.Line
                        });
                    }
                }
            }
        }

        private ChangeType DetermineChangeType(FileDiffAnalysis analysis)
        {
            if (analysis.AddedSymbols.Any() && !analysis.RemovedSymbols.Any() && !analysis.ModifiedSymbols.Any())
                return ChangeType.Added;
            
            if (analysis.RemovedSymbols.Any() && !analysis.AddedSymbols.Any() && !analysis.ModifiedSymbols.Any())
                return ChangeType.Deleted;
            
            if (analysis.AddedSymbols.Any() || analysis.RemovedSymbols.Any() || analysis.ModifiedSymbols.Any())
                return ChangeType.Modified;
            
            return analysis.LinesAdded > 0 || analysis.LinesRemoved > 0 || analysis.LinesModified > 0 
                ? ChangeType.Modified 
                : ChangeType.Unchanged;
        }

        private async Task AnalyzeProjectChangesAsync(string projectFile, string repositoryPath, RepositoryDiffAnalysis analysis, CancellationToken cancellationToken)
        {
            var relativePath = Path.GetRelativePath(repositoryPath, projectFile);
            
            var projectChange = new ProjectChange
            {
                ProjectPath = relativePath,
                ChangeType = ChangeType.Modified // Would need previous version to determine exact change
            };

            analysis.ProjectChanges[relativePath] = projectChange;
        }

        private void CompileChangeStatistics(RepositoryDiffAnalysis analysis)
        {
            analysis.Statistics.TotalFilesChanged = analysis.FileChanges.Count;
            analysis.Statistics.TotalLinesAdded = analysis.FileChanges.Sum(f => f.LinesAdded);
            analysis.Statistics.TotalLinesRemoved = analysis.FileChanges.Sum(f => f.LinesRemoved);
            analysis.Statistics.TotalLinesModified = analysis.FileChanges.Sum(f => f.LinesModified);
            analysis.Statistics.ProjectsAffected = analysis.ProjectChanges.Count;
            analysis.Statistics.SymbolsAffected = analysis.FileChanges.Sum(f => f.AddedSymbols.Count + f.RemovedSymbols.Count + f.ModifiedSymbols.Count);

            // Group by extension
            foreach (var fileChange in analysis.FileChanges)
            {
                var ext = Path.GetExtension(fileChange.FilePath);
                analysis.Statistics.ChangesByExtension[ext] = analysis.Statistics.ChangesByExtension.GetValueOrDefault(ext, 0) + 1;
            }

            // Group by change type
            foreach (var fileChange in analysis.FileChanges)
            {
                var changeType = fileChange.ChangeType.ToString();
                analysis.Statistics.ChangesByType[changeType] = analysis.Statistics.ChangesByType.GetValueOrDefault(changeType, 0) + 1;
            }
        }

        private async Task<List<UpdateOperation>> CreateFileUpdateOperationsAsync(FileDiffAnalysis fileChange, CancellationToken cancellationToken)
        {
            var operations = new List<UpdateOperation>();

            // Always reanalyze the file
            operations.Add(new UpdateOperation
            {
                OperationId = Guid.NewGuid().ToString(),
                Type = UpdateOperationType.AnalyzeFile,
                TargetPath = fileChange.FilePath,
                Priority = UpdatePriority.Normal
            });

            // Update nodes if elements changed
            if (fileChange.ElementChanges.Any())
            {
                operations.Add(new UpdateOperation
                {
                    OperationId = Guid.NewGuid().ToString(),
                    Type = UpdateOperationType.UpdateNodes,
                    TargetPath = fileChange.FilePath,
                    Priority = UpdatePriority.High
                });
            }

            return operations;
        }

        private async Task<List<UpdateOperation>> CreateProjectUpdateOperationsAsync(ProjectChange projectChange, CancellationToken cancellationToken)
        {
            var operations = new List<UpdateOperation>();

            if (projectChange.AddedReferences.Any() || projectChange.RemovedReferences.Any())
            {
                operations.Add(new UpdateOperation
                {
                    OperationId = Guid.NewGuid().ToString(),
                    Type = UpdateOperationType.UpdateRelationships,
                    TargetPath = projectChange.ProjectPath,
                    Priority = UpdatePriority.High
                });
            }

            return operations;
        }

        private UpdatePriority DetermineUpdatePriority(RepositoryDiffAnalysis diffAnalysis)
        {
            var totalChanges = diffAnalysis.Statistics.TotalFilesChanged;
            var symbolsAffected = diffAnalysis.Statistics.SymbolsAffected;

            if (totalChanges > 100 || symbolsAffected > 500)
                return UpdatePriority.Critical;
            if (totalChanges > 50 || symbolsAffected > 200)
                return UpdatePriority.High;
            if (totalChanges > 10 || symbolsAffected > 50)
                return UpdatePriority.Normal;

            return UpdatePriority.Low;
        }

        private TimeSpan EstimateUpdateDuration(IncrementalUpdatePlan plan)
        {
            // Simple estimation based on operation count
            var baseTime = TimeSpan.FromSeconds(1); // Base per operation
            var totalTime = TimeSpan.FromTicks(plan.Operations.Count * baseTime.Ticks);
            
            // Add extra time for high-priority operations
            var highPriorityOps = plan.Operations.Count(o => o.Priority == UpdatePriority.High || o.Priority == UpdatePriority.Critical);
            totalTime = totalTime.Add(TimeSpan.FromTicks(highPriorityOps * baseTime.Ticks));

            return totalTime;
        }

        private List<UpdateOperation> OrderOperationsByDependencies(List<UpdateOperation> operations)
        {
            // Simple ordering: analysis first, then updates, then cleanup
            return operations
                .OrderBy(o => o.Type)
                .ThenByDescending(o => o.Priority)
                .ToList();
        }

        private List<string> GenerateCacheKeys(string repositoryId, string filePath)
        {
            return new List<string>
            {
                $"symbols:{repositoryId}:{filePath}",
                $"compilation:{repositoryId}:{Path.GetDirectoryName(filePath)}",
                $"query:{repositoryId}:*{Path.GetFileNameWithoutExtension(filePath)}*"
            };
        }

        private async Task<List<string>> GetNodesForFileAsync(string repositoryId, string filePath, CancellationToken cancellationToken)
        {
            // This would query the graph to get all nodes for a specific file
            // For now, return empty list as placeholder
            return new List<string>();
        }

        private async Task<List<string>> GetIndirectlyAffectedNodesAsync(string repositoryId, List<string> directNodes, CancellationToken cancellationToken)
        {
            // This would query the graph to find nodes connected to the direct nodes
            // For now, return empty list as placeholder
            return new List<string>();
        }

        private async Task<List<string>> GetAffectedRelationshipsAsync(string repositoryId, List<string> affectedNodes, CancellationToken cancellationToken)
        {
            // This would query the graph to find relationships involving the affected nodes
            return new List<string>();
        }

        private async Task<Dictionary<string, int>> CalculateImpactByNodeTypeAsync(string repositoryId, List<string> affectedNodes, CancellationToken cancellationToken)
        {
            // This would analyze the types of affected nodes
            return new Dictionary<string, int>();
        }

        private ImpactSeverity DetermineImpactSeverity(ChangeImpactAnalysis analysis)
        {
            var totalAffected = analysis.DirectlyAffectedNodes.Count + analysis.IndirectlyAffectedNodes.Count;
            
            if (totalAffected > 1000) return ImpactSeverity.Critical;
            if (totalAffected > 500) return ImpactSeverity.High;
            if (totalAffected > 100) return ImpactSeverity.Medium;
            return ImpactSeverity.Low;
        }

        private async Task<List<string>> FindRippleEffectsAsync(string repositoryId, List<string> directNodes, CancellationToken cancellationToken)
        {
            // This would find cascading effects of the changes
            return new List<string>();
        }

        private async Task<List<FileDependency>> GetFileDependenciesAsync(string repositoryId, string filePath, CancellationToken cancellationToken)
        {
            // This would query the graph for file dependencies
            return new List<FileDependency>();
        }

        private async Task<List<FileDependency>> GetFileDependentsAsync(string repositoryId, string filePath, CancellationToken cancellationToken)
        {
            // This would query the graph for files that depend on this file
            return new List<FileDependency>();
        }

        private Dictionary<string, int> CalculateDependencyLevels(Interfaces.DependencyGraph graph)
        {
            var levels = new Dictionary<string, int>();
            
            // Simple BFS to calculate dependency levels
            levels[graph.RootFile] = 0;
            
            var queue = new Queue<(string file, int level)>();
            queue.Enqueue((graph.RootFile, 0));
            
            while (queue.Count > 0)
            {
                var (currentFile, currentLevel) = queue.Dequeue();
                
                foreach (var dependency in graph.Dependencies.Where(d => d.Distance == currentLevel + 1))
                {
                    if (!levels.ContainsKey(dependency.FilePath))
                    {
                        levels[dependency.FilePath] = currentLevel + 1;
                        queue.Enqueue((dependency.FilePath, currentLevel + 1));
                    }
                }
            }
            
            return levels;
        }

        private bool ShouldExcludeFile(string filePath)
        {
            var excludedPatterns = new[] { "bin", "obj", ".git", ".vs", "node_modules", "packages" };
            return excludedPatterns.Any(pattern => filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private string ComputeChecksum(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private async Task<string> ComputeFileChecksumAsync(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }
    }
}