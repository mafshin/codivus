using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// Service for tracking graph versions and managing version history
    /// </summary>
    public class VersionTrackingService : IVersionTrackingService
    {
        private readonly IGraphStorageService _graphStorage;
        private readonly ILogger<VersionTrackingService> _logger;
        private readonly ConcurrentDictionary<string, GraphVersion> _versionCache;
        private readonly ConcurrentDictionary<string, string> _currentVersions; // repositoryId -> versionId

        public VersionTrackingService(
            IGraphStorageService graphStorage,
            ILogger<VersionTrackingService> logger)
        {
            _graphStorage = graphStorage;
            _logger = logger;
            _versionCache = new ConcurrentDictionary<string, GraphVersion>();
            _currentVersions = new ConcurrentDictionary<string, string>();
        }

        public async Task<GraphVersion> CreateVersionAsync(string repositoryId, string description, VersionMetadata metadata, CancellationToken cancellationToken = default)
        {
            try
            {
                var versionId = GenerateVersionId();
                var now = DateTime.UtcNow;

                // Get current version to set as parent
                var currentVersion = await GetCurrentVersionAsync(repositoryId, cancellationToken);
                
                // Calculate graph statistics
                var stats = await CalculateGraphStatisticsAsync(repositoryId, cancellationToken);

                var version = new GraphVersion
                {
                    VersionId = versionId,
                    RepositoryId = repositoryId,
                    CreatedAt = now,
                    CreatedBy = Environment.UserName,
                    Description = description,
                    Metadata = metadata,
                    Stats = stats,
                    ParentVersionId = currentVersion?.VersionId,
                    IsCurrent = true
                };

                // Mark previous version as not current
                if (currentVersion != null)
                {
                    currentVersion.IsCurrent = false;
                    await StoreVersionAsync(currentVersion, cancellationToken);
                }

                // Store new version
                await StoreVersionAsync(version, cancellationToken);

                // Update current version tracking
                _currentVersions[repositoryId] = versionId;
                _versionCache[GetCacheKey(repositoryId, versionId)] = version;

                _logger.LogInformation("Created graph version {VersionId} for repository {RepositoryId} with {NodeCount} nodes, {EdgeCount} edges", 
                    versionId, repositoryId, stats.NodeCount, stats.EdgeCount);

                return version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating version for repository {RepositoryId}", repositoryId);
                throw;
            }
        }

        public async Task<GraphVersion?> GetCurrentVersionAsync(string repositoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check cache first
                if (_currentVersions.TryGetValue(repositoryId, out var versionId))
                {
                    return await GetVersionAsync(repositoryId, versionId, cancellationToken);
                }

                // Query from storage
                var versions = await ListVersionsAsync(repositoryId, 1, 0, cancellationToken);
                var currentVersion = versions.FirstOrDefault(v => v.IsCurrent);

                if (currentVersion != null)
                {
                    _currentVersions[repositoryId] = currentVersion.VersionId;
                }

                return currentVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current version for repository {RepositoryId}", repositoryId);
                return null;
            }
        }

        public async Task<GraphVersion?> GetVersionAsync(string repositoryId, string versionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var cacheKey = GetCacheKey(repositoryId, versionId);
                
                // Check cache
                if (_versionCache.TryGetValue(cacheKey, out var cachedVersion))
                {
                    return cachedVersion;
                }

                // Load from storage
                var version = await LoadVersionAsync(repositoryId, versionId, cancellationToken);
                
                if (version != null)
                {
                    _versionCache.TryAdd(cacheKey, version);
                }

                return version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting version {VersionId} for repository {RepositoryId}", versionId, repositoryId);
                return null;
            }
        }

        public async Task<IEnumerable<GraphVersion>> ListVersionsAsync(string repositoryId, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
        {
            try
            {
                // This would query the storage for all versions
                // For now, return versions from cache
                var versions = _versionCache.Values
                    .Where(v => v.RepositoryId == repositoryId)
                    .OrderByDescending(v => v.CreatedAt)
                    .Skip(offset)
                    .Take(limit)
                    .ToList();

                _logger.LogDebug("Listed {Count} versions for repository {RepositoryId}", versions.Count, repositoryId);
                return versions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing versions for repository {RepositoryId}", repositoryId);
                return Enumerable.Empty<GraphVersion>();
            }
        }

        public async Task<VersionDiff> CompareVersionsAsync(string repositoryId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromVersion = await GetVersionAsync(repositoryId, fromVersionId, cancellationToken);
                var toVersion = await GetVersionAsync(repositoryId, toVersionId, cancellationToken);

                if (fromVersion == null || toVersion == null)
                {
                    throw new InvalidOperationException("One or both versions not found");
                }

                var diff = new VersionDiff
                {
                    FromVersionId = fromVersionId,
                    ToVersionId = toVersionId,
                    ComparedAt = DateTime.UtcNow
                };

                // Compare nodes
                await CompareNodesAsync(repositoryId, fromVersion, toVersion, diff, cancellationToken);

                // Compare edges
                await CompareEdgesAsync(repositoryId, fromVersion, toVersion, diff, cancellationToken);

                // Compare files
                await CompareFilesAsync(fromVersion, toVersion, diff, cancellationToken);

                // Calculate statistics
                diff.Statistics = CalculateDiffStatistics(diff);

                _logger.LogDebug("Compared versions {FromVersion} to {ToVersion}: {NodeDiffs} node diffs, {EdgeDiffs} edge diffs", 
                    fromVersionId, toVersionId, diff.NodeDiffs.Count, diff.EdgeDiffs.Count);

                return diff;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing versions {FromVersion} to {ToVersion} for repository {RepositoryId}", 
                    fromVersionId, toVersionId, repositoryId);
                throw;
            }
        }

        public async Task<RollbackResult> RollbackToVersionAsync(string repositoryId, string versionId, CancellationToken cancellationToken = default)
        {
            var result = new RollbackResult();
            var startTime = DateTime.UtcNow;

            try
            {
                var targetVersion = await GetVersionAsync(repositoryId, versionId, cancellationToken);
                if (targetVersion == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Target version not found";
                    return result;
                }

                var currentVersion = await GetCurrentVersionAsync(repositoryId, cancellationToken);
                if (currentVersion == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Current version not found";
                    return result;
                }

                // Create a diff between current and target
                var diff = await CompareVersionsAsync(repositoryId, currentVersion.VersionId, targetVersion.VersionId, cancellationToken);

                // Apply rollback operations
                foreach (var nodeDiff in diff.NodeDiffs)
                {
                    await ApplyNodeRollbackAsync(repositoryId, nodeDiff, cancellationToken);
                    result.AffectedNodes.Add(nodeDiff.NodeId);
                }

                foreach (var edgeDiff in diff.EdgeDiffs)
                {
                    await ApplyEdgeRollbackAsync(repositoryId, edgeDiff, cancellationToken);
                    result.AffectedEdges.Add(edgeDiff.EdgeId);
                }

                // Create new version representing the rollback
                var rollbackMetadata = new VersionMetadata
                {
                    ScanType = "Rollback",
                    CustomMetadata = new Dictionary<string, object>
                    {
                        ["RolledBackFrom"] = currentVersion.VersionId,
                        ["RolledBackTo"] = targetVersion.VersionId
                    }
                };

                var newVersion = await CreateVersionAsync(repositoryId, 
                    $"Rollback to version {targetVersion.VersionId}", 
                    rollbackMetadata, 
                    cancellationToken);

                result.Success = true;
                result.NewVersionId = newVersion.VersionId;
                result.Duration = DateTime.UtcNow - startTime;

                _logger.LogInformation("Rolled back repository {RepositoryId} from version {FromVersion} to {ToVersion}, created new version {NewVersion}", 
                    repositoryId, currentVersion.VersionId, targetVersion.VersionId, newVersion.VersionId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rolling back repository {RepositoryId} to version {VersionId}", repositoryId, versionId);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.UtcNow - startTime;
                return result;
            }
        }

        public async Task TagVersionAsync(string repositoryId, string versionId, string tagName, Dictionary<string, object> tagMetadata, CancellationToken cancellationToken = default)
        {
            try
            {
                var version = await GetVersionAsync(repositoryId, versionId, cancellationToken);
                if (version == null)
                {
                    throw new InvalidOperationException("Version not found");
                }

                var tag = new VersionTag
                {
                    TagName = tagName,
                    TaggedAt = DateTime.UtcNow,
                    TaggedBy = Environment.UserName,
                    Metadata = tagMetadata
                };

                version.Tags.Add(tag);
                await StoreVersionAsync(version, cancellationToken);

                _logger.LogInformation("Tagged version {VersionId} with tag {TagName} for repository {RepositoryId}", 
                    versionId, tagName, repositoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tagging version {VersionId} for repository {RepositoryId}", versionId, repositoryId);
                throw;
            }
        }

        public async Task<PruneResult> PruneVersionsAsync(string repositoryId, VersionRetentionPolicy policy, CancellationToken cancellationToken = default)
        {
            var result = new PruneResult();
            var startTime = DateTime.UtcNow;

            try
            {
                var allVersions = (await ListVersionsAsync(repositoryId, int.MaxValue, 0, cancellationToken)).ToList();
                var versionsToKeep = new HashSet<string>();
                var versionsToPrune = new List<GraphVersion>();

                // Always keep current version
                var currentVersion = allVersions.FirstOrDefault(v => v.IsCurrent);
                if (currentVersion != null)
                {
                    versionsToKeep.Add(currentVersion.VersionId);
                }

                // Keep last N versions
                var recentVersions = allVersions.OrderByDescending(v => v.CreatedAt).Take(policy.KeepLastNVersions);
                foreach (var version in recentVersions)
                {
                    versionsToKeep.Add(version.VersionId);
                }

                // Keep versions newer than threshold
                if (policy.KeepVersionsNewerThan.HasValue)
                {
                    var threshold = DateTime.UtcNow - policy.KeepVersionsNewerThan.Value;
                    var newerVersions = allVersions.Where(v => v.CreatedAt > threshold);
                    foreach (var version in newerVersions)
                    {
                        versionsToKeep.Add(version.VersionId);
                    }
                }

                // Keep tagged versions
                if (policy.KeepTaggedVersions)
                {
                    var taggedVersions = allVersions.Where(v => v.Tags.Any());
                    foreach (var version in taggedVersions)
                    {
                        versionsToKeep.Add(version.VersionId);
                    }
                }

                // Keep versions with specific tags
                if (policy.KeepVersionsWithTags.Any())
                {
                    var specificTaggedVersions = allVersions.Where(v => 
                        v.Tags.Any(t => policy.KeepVersionsWithTags.Contains(t.TagName)));
                    foreach (var version in specificTaggedVersions)
                    {
                        versionsToKeep.Add(version.VersionId);
                    }
                }

                // Determine versions to prune
                foreach (var version in allVersions)
                {
                    if (!versionsToKeep.Contains(version.VersionId))
                    {
                        versionsToPrune.Add(version);
                    }
                }

                // Check size constraints
                if (policy.MaxTotalSizeBytes.HasValue)
                {
                    var totalSize = allVersions.Sum(v => v.Stats.GraphSizeBytes);
                    if (totalSize > policy.MaxTotalSizeBytes.Value)
                    {
                        // Sort versions to prune by age (oldest first)
                        var sortedVersions = allVersions
                            .Where(v => !versionsToKeep.Contains(v.VersionId))
                            .OrderBy(v => v.CreatedAt)
                            .ToList();

                        var currentSize = totalSize;
                        foreach (var version in sortedVersions)
                        {
                            if (currentSize <= policy.MaxTotalSizeBytes.Value)
                                break;

                            if (!versionsToPrune.Any(v => v.VersionId == version.VersionId))
                            {
                                versionsToPrune.Add(version);
                            }
                            currentSize -= version.Stats.GraphSizeBytes;
                        }
                    }
                }

                // Prune versions
                foreach (var version in versionsToPrune)
                {
                    await DeleteVersionAsync(repositoryId, version.VersionId, cancellationToken);
                    result.PrunedVersionIds.Add(version.VersionId);
                    result.SpaceReclaimed += version.Stats.GraphSizeBytes;
                    result.VersionsPruned++;
                }

                // Record retained versions
                result.RetainedVersionIds = versionsToKeep.ToList();
                result.Duration = DateTime.UtcNow - startTime;

                _logger.LogInformation("Pruned {PrunedCount} versions for repository {RepositoryId}, reclaimed {SpaceBytes} bytes", 
                    result.VersionsPruned, repositoryId, result.SpaceReclaimed);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pruning versions for repository {RepositoryId}", repositoryId);
                result.Duration = DateTime.UtcNow - startTime;
                return result;
            }
        }

        public async Task<VersionStatistics> GetStatisticsAsync(string repositoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                var versions = (await ListVersionsAsync(repositoryId, int.MaxValue, 0, cancellationToken)).ToList();
                
                var stats = new VersionStatistics
                {
                    TotalVersions = versions.Count,
                    TaggedVersions = versions.Count(v => v.Tags.Any()),
                    TotalStorageBytes = versions.Sum(v => v.Stats.GraphSizeBytes),
                    OldestVersion = versions.OrderBy(v => v.CreatedAt).FirstOrDefault()?.CreatedAt,
                    NewestVersion = versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault()?.CreatedAt,
                    AverageVersionSizeBytes = versions.Any() ? versions.Average(v => v.Stats.GraphSizeBytes) : 0
                };

                // Calculate versions by month
                var versionsByMonth = versions
                    .GroupBy(v => new { v.CreatedAt.Year, v.CreatedAt.Month })
                    .Select(g => new { Key = $"{g.Key.Year}-{g.Key.Month:D2}", Count = g.Count() });

                foreach (var month in versionsByMonth)
                {
                    stats.VersionsByMonth[month.Key] = month.Count;
                }

                // Calculate version frequency
                var versionsByDate = versions
                    .GroupBy(v => v.CreatedAt.Date)
                    .Select(g => new VersionFrequency
                    {
                        Date = g.Key,
                        VersionCount = g.Count(),
                        TotalChanges = g.Sum(v => v.Metadata.ChangedFiles.Count)
                    });

                stats.VersionFrequency = versionsByDate.ToList();

                _logger.LogDebug("Retrieved version statistics for repository {RepositoryId}: {TotalVersions} versions, {TotalSize} bytes", 
                    repositoryId, stats.TotalVersions, stats.TotalStorageBytes);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting version statistics for repository {RepositoryId}", repositoryId);
                return new VersionStatistics();
            }
        }

        private string GenerateVersionId()
        {
            return $"v_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}".Substring(0, 20);
        }

        private string GetCacheKey(string repositoryId, string versionId)
        {
            return $"{repositoryId}:{versionId}";
        }

        private async Task<GraphVersionStats> CalculateGraphStatisticsAsync(string repositoryId, CancellationToken cancellationToken)
        {
            // This would query the graph to get actual statistics
            // For now, return placeholder stats
            return new GraphVersionStats
            {
                NodeCount = 1000,
                EdgeCount = 5000,
                NodeCountByType = new Dictionary<string, long>
                {
                    ["Type"] = 100,
                    ["Method"] = 500,
                    ["Property"] = 200,
                    ["Field"] = 200
                },
                EdgeCountByType = new Dictionary<string, long>
                {
                    ["CONTAINS"] = 2000,
                    ["CALLS"] = 1500,
                    ["USES"] = 1000,
                    ["INHERITS"] = 500
                },
                GraphSizeBytes = 1024 * 1024 * 10, // 10MB placeholder
                FileCount = 50,
                ProjectCount = 5
            };
        }

        private async Task StoreVersionAsync(GraphVersion version, CancellationToken cancellationToken)
        {
            // This would persist the version to storage
            _versionCache[GetCacheKey(version.RepositoryId, version.VersionId)] = version;
        }

        private async Task<GraphVersion?> LoadVersionAsync(string repositoryId, string versionId, CancellationToken cancellationToken)
        {
            // This would load the version from storage
            // For now, check cache only
            var cacheKey = GetCacheKey(repositoryId, versionId);
            return _versionCache.TryGetValue(cacheKey, out var version) ? version : null;
        }

        private async Task DeleteVersionAsync(string repositoryId, string versionId, CancellationToken cancellationToken)
        {
            // This would delete the version from storage
            var cacheKey = GetCacheKey(repositoryId, versionId);
            _versionCache.TryRemove(cacheKey, out _);
        }

        private async Task CompareNodesAsync(string repositoryId, GraphVersion fromVersion, GraphVersion toVersion, VersionDiff diff, CancellationToken cancellationToken)
        {
            // This would query the graph to compare nodes between versions
            // For now, add placeholder diffs
            diff.NodeDiffs.Add(new NodeDiff
            {
                NodeId = "node1",
                NodeType = "Type",
                Operation = DiffOperation.Modified,
                PropertyDiffs = new Dictionary<string, PropertyDiff>
                {
                    ["name"] = new PropertyDiff
                    {
                        PropertyName = "name",
                        OldValue = "OldClass",
                        NewValue = "NewClass",
                        DiffType = PropertyDiffType.Modified
                    }
                }
            });
        }

        private async Task CompareEdgesAsync(string repositoryId, GraphVersion fromVersion, GraphVersion toVersion, VersionDiff diff, CancellationToken cancellationToken)
        {
            // This would query the graph to compare edges between versions
            // For now, add placeholder diffs
            diff.EdgeDiffs.Add(new EdgeDiff
            {
                EdgeId = "edge1",
                EdgeType = "CALLS",
                FromNodeId = "method1",
                ToNodeId = "method2",
                Operation = DiffOperation.Added
            });
        }

        private async Task CompareFilesAsync(GraphVersion fromVersion, GraphVersion toVersion, VersionDiff diff, CancellationToken cancellationToken)
        {
            // Compare changed files between versions
            var fromFiles = new HashSet<string>(fromVersion.Metadata.ChangedFiles);
            var toFiles = new HashSet<string>(toVersion.Metadata.ChangedFiles);

            // Files added in toVersion
            foreach (var file in toFiles.Except(fromFiles))
            {
                diff.FileDiffs.Add(new FileDiff
                {
                    FilePath = file,
                    Operation = DiffOperation.Added
                });
            }

            // Files removed in toVersion
            foreach (var file in fromFiles.Except(toFiles))
            {
                diff.FileDiffs.Add(new FileDiff
                {
                    FilePath = file,
                    Operation = DiffOperation.Deleted
                });
            }

            // Files present in both (potentially modified)
            foreach (var file in fromFiles.Intersect(toFiles))
            {
                diff.FileDiffs.Add(new FileDiff
                {
                    FilePath = file,
                    Operation = DiffOperation.Modified
                });
            }
        }

        private DiffStatistics CalculateDiffStatistics(VersionDiff diff)
        {
            var stats = new DiffStatistics
            {
                TotalNodeDiffs = diff.NodeDiffs.Count,
                TotalEdgeDiffs = diff.EdgeDiffs.Count,
                TotalFileDiffs = diff.FileDiffs.Count
            };

            // Count by node type
            foreach (var nodeDiff in diff.NodeDiffs)
            {
                stats.NodeDiffsByType[nodeDiff.NodeType] = stats.NodeDiffsByType.GetValueOrDefault(nodeDiff.NodeType, 0) + 1;
            }

            // Count by edge type
            foreach (var edgeDiff in diff.EdgeDiffs)
            {
                stats.EdgeDiffsByType[edgeDiff.EdgeType] = stats.EdgeDiffsByType.GetValueOrDefault(edgeDiff.EdgeType, 0) + 1;
            }

            // Count by operation
            foreach (DiffOperation op in Enum.GetValues<DiffOperation>())
            {
                stats.OperationCounts[op] = 
                    diff.NodeDiffs.Count(d => d.Operation == op) +
                    diff.EdgeDiffs.Count(d => d.Operation == op) +
                    diff.FileDiffs.Count(d => d.Operation == op);
            }

            return stats;
        }

        private async Task ApplyNodeRollbackAsync(string repositoryId, NodeDiff nodeDiff, CancellationToken cancellationToken)
        {
            // This would apply the node rollback operation to the graph
            switch (nodeDiff.Operation)
            {
                case DiffOperation.Added:
                    // Remove the node
                    break;
                case DiffOperation.Deleted:
                    // Re-add the node
                    break;
                case DiffOperation.Modified:
                    // Revert properties
                    break;
            }
        }

        private async Task ApplyEdgeRollbackAsync(string repositoryId, EdgeDiff edgeDiff, CancellationToken cancellationToken)
        {
            // This would apply the edge rollback operation to the graph
            switch (edgeDiff.Operation)
            {
                case DiffOperation.Added:
                    // Remove the edge
                    break;
                case DiffOperation.Deleted:
                    // Re-add the edge
                    break;
                case DiffOperation.Modified:
                    // Revert properties
                    break;
            }
        }
    }
}