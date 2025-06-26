using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    /// <summary>
    /// Interface for tracking graph versions and managing version history
    /// </summary>
    public interface IVersionTrackingService
    {
        /// <summary>
        /// Creates a new version snapshot of the graph
        /// </summary>
        Task<GraphVersion> CreateVersionAsync(string repositoryId, string description, VersionMetadata metadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current version of the graph
        /// </summary>
        Task<GraphVersion?> GetCurrentVersionAsync(string repositoryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific version by ID
        /// </summary>
        Task<GraphVersion?> GetVersionAsync(string repositoryId, string versionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all versions for a repository
        /// </summary>
        Task<IEnumerable<GraphVersion>> ListVersionsAsync(string repositoryId, int limit = 100, int offset = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Compares two versions and returns the differences
        /// </summary>
        Task<VersionDiff> CompareVersionsAsync(string repositoryId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the graph to a specific version
        /// </summary>
        Task<RollbackResult> RollbackToVersionAsync(string repositoryId, string versionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tags a version with additional metadata
        /// </summary>
        Task TagVersionAsync(string repositoryId, string versionId, string tagName, Dictionary<string, object> tagMetadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Prunes old versions based on retention policy
        /// </summary>
        Task<PruneResult> PruneVersionsAsync(string repositoryId, VersionRetentionPolicy policy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets version history statistics
        /// </summary>
        Task<VersionStatistics> GetStatisticsAsync(string repositoryId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a version of the graph
    /// </summary>
    public class GraphVersion
    {
        public string VersionId { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public VersionMetadata Metadata { get; set; } = new();
        public GraphVersionStats Stats { get; set; } = new();
        public List<VersionTag> Tags { get; set; } = new();
        public string? ParentVersionId { get; set; }
        public bool IsCurrent { get; set; }
    }

    /// <summary>
    /// Metadata associated with a graph version
    /// </summary>
    public class VersionMetadata
    {
        public string CommitHash { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public List<string> ChangedFiles { get; set; } = new();
        public Dictionary<string, int> ChangesSummary { get; set; } = new();
        public string ScanType { get; set; } = string.Empty; // Full, Incremental
        public TimeSpan ScanDuration { get; set; }
        public Dictionary<string, object> CustomMetadata { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a graph version
    /// </summary>
    public class GraphVersionStats
    {
        public long NodeCount { get; set; }
        public long EdgeCount { get; set; }
        public Dictionary<string, long> NodeCountByType { get; set; } = new();
        public Dictionary<string, long> EdgeCountByType { get; set; } = new();
        public long GraphSizeBytes { get; set; }
        public int FileCount { get; set; }
        public int ProjectCount { get; set; }
    }

    /// <summary>
    /// Tag associated with a version
    /// </summary>
    public class VersionTag
    {
        public string TagName { get; set; } = string.Empty;
        public DateTime TaggedAt { get; set; }
        public string TaggedBy { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Differences between two graph versions
    /// </summary>
    public class VersionDiff
    {
        public string FromVersionId { get; set; } = string.Empty;
        public string ToVersionId { get; set; } = string.Empty;
        public DateTime ComparedAt { get; set; }
        public List<NodeDiff> NodeDiffs { get; set; } = new();
        public List<EdgeDiff> EdgeDiffs { get; set; } = new();
        public List<FileDiff> FileDiffs { get; set; } = new();
        public DiffStatistics Statistics { get; set; } = new();
    }

    /// <summary>
    /// Node-level differences
    /// </summary>
    public class NodeDiff
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public DiffOperation Operation { get; set; }
        public Dictionary<string, PropertyDiff> PropertyDiffs { get; set; } = new();
    }

    /// <summary>
    /// Edge-level differences
    /// </summary>
    public class EdgeDiff
    {
        public string EdgeId { get; set; } = string.Empty;
        public string EdgeType { get; set; } = string.Empty;
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public DiffOperation Operation { get; set; }
        public Dictionary<string, PropertyDiff> PropertyDiffs { get; set; } = new();
    }

    /// <summary>
    /// File-level differences
    /// </summary>
    public class FileDiff
    {
        public string FilePath { get; set; } = string.Empty;
        public DiffOperation Operation { get; set; }
        public string? OldChecksum { get; set; }
        public string? NewChecksum { get; set; }
        public int NodesAffected { get; set; }
        public int EdgesAffected { get; set; }
    }

    /// <summary>
    /// Property-level differences
    /// </summary>
    public class PropertyDiff
    {
        public string PropertyName { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public PropertyDiffType DiffType { get; set; }
    }

    /// <summary>
    /// Statistics about version differences
    /// </summary>
    public class DiffStatistics
    {
        public int TotalNodeDiffs { get; set; }
        public int TotalEdgeDiffs { get; set; }
        public int TotalFileDiffs { get; set; }
        public Dictionary<string, int> NodeDiffsByType { get; set; } = new();
        public Dictionary<string, int> EdgeDiffsByType { get; set; } = new();
        public Dictionary<DiffOperation, int> OperationCounts { get; set; } = new();
    }

    /// <summary>
    /// Result of a rollback operation
    /// </summary>
    public class RollbackResult
    {
        public bool Success { get; set; }
        public string? NewVersionId { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> AffectedNodes { get; set; } = new();
        public List<string> AffectedEdges { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Result of pruning old versions
    /// </summary>
    public class PruneResult
    {
        public int VersionsPruned { get; set; }
        public long SpaceReclaimed { get; set; }
        public List<string> PrunedVersionIds { get; set; } = new();
        public List<string> RetainedVersionIds { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Version retention policy
    /// </summary>
    public class VersionRetentionPolicy
    {
        public int KeepLastNVersions { get; set; } = 10;
        public TimeSpan? KeepVersionsNewerThan { get; set; }
        public bool KeepTaggedVersions { get; set; } = true;
        public List<string> KeepVersionsWithTags { get; set; } = new();
        public long? MaxTotalSizeBytes { get; set; }
    }

    /// <summary>
    /// Version history statistics
    /// </summary>
    public class VersionStatistics
    {
        public int TotalVersions { get; set; }
        public int TaggedVersions { get; set; }
        public long TotalStorageBytes { get; set; }
        public DateTime? OldestVersion { get; set; }
        public DateTime? NewestVersion { get; set; }
        public double AverageVersionSizeBytes { get; set; }
        public Dictionary<string, int> VersionsByMonth { get; set; } = new();
        public List<VersionFrequency> VersionFrequency { get; set; } = new();
    }

    /// <summary>
    /// Version creation frequency
    /// </summary>
    public class VersionFrequency
    {
        public DateTime Date { get; set; }
        public int VersionCount { get; set; }
        public long TotalChanges { get; set; }
    }

    /// <summary>
    /// Type of diff operation
    /// </summary>
    public enum DiffOperation
    {
        Added,
        Modified,
        Deleted
    }

    /// <summary>
    /// Type of property difference
    /// </summary>
    public enum PropertyDiffType
    {
        Added,
        Modified,
        Deleted,
        TypeChanged
    }
}