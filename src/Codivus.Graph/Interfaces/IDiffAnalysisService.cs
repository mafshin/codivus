using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    /// <summary>
    /// Interface for analyzing code differences and planning incremental updates
    /// </summary>
    public interface IDiffAnalysisService
    {
        /// <summary>
        /// Analyzes differences between two versions of a file
        /// </summary>
        Task<FileDiffAnalysis> AnalyzeFileDiffAsync(string filePath, string oldContent, string newContent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes changes in a repository since last scan
        /// </summary>
        Task<RepositoryDiffAnalysis> AnalyzeRepositoryChangesAsync(string repositoryId, string repositoryPath, DateTime? lastScanTime = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Plans incremental graph updates based on diff analysis
        /// </summary>
        Task<IncrementalUpdatePlan> CreateUpdatePlanAsync(RepositoryDiffAnalysis diffAnalysis, CancellationToken cancellationToken = default);

        /// <summary>
        /// Estimates the impact of changes on the graph
        /// </summary>
        Task<ChangeImpactAnalysis> AnalyzeChangeImpactAsync(string repositoryId, IEnumerable<string> changedFiles, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets dependency graph for a file to understand change propagation
        /// </summary>
        Task<DependencyGraph> GetFileDependencyGraphAsync(string repositoryId, string filePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of file diff analysis
    /// </summary>
    public class FileDiffAnalysis
    {
        public string FilePath { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public string PreviousChecksum { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
        public ChangeType ChangeType { get; set; }
        public List<CodeElementChange> ElementChanges { get; set; } = new();
        public List<string> AddedSymbols { get; set; } = new();
        public List<string> RemovedSymbols { get; set; } = new();
        public List<string> ModifiedSymbols { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public int LinesModified { get; set; }
    }

    /// <summary>
    /// Result of repository-wide diff analysis
    /// </summary>
    public class RepositoryDiffAnalysis
    {
        public string RepositoryId { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
        public DateTime? LastScanTime { get; set; }
        public List<FileDiffAnalysis> FileChanges { get; set; } = new();
        public List<string> AddedFiles { get; set; } = new();
        public List<string> DeletedFiles { get; set; } = new();
        public List<string> RenamedFiles { get; set; } = new();
        public Dictionary<string, ProjectChange> ProjectChanges { get; set; } = new();
        public ChangeStatistics Statistics { get; set; } = new();
    }

    /// <summary>
    /// Plan for incremental graph updates
    /// </summary>
    public class IncrementalUpdatePlan
    {
        public string RepositoryId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<UpdateOperation> Operations { get; set; } = new();
        public List<string> FilesToReanalyze { get; set; } = new();
        public List<string> NodesToDelete { get; set; } = new();
        public List<string> EdgesToDelete { get; set; } = new();
        public List<string> CacheKeysToInvalidate { get; set; } = new();
        public UpdatePriority Priority { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Analysis of change impact on the graph
    /// </summary>
    public class ChangeImpactAnalysis
    {
        public string RepositoryId { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
        public List<string> DirectlyAffectedNodes { get; set; } = new();
        public List<string> IndirectlyAffectedNodes { get; set; } = new();
        public List<string> AffectedRelationships { get; set; } = new();
        public Dictionary<string, int> ImpactByNodeType { get; set; } = new();
        public ImpactSeverity Severity { get; set; }
        public List<string> RippleEffects { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    /// <summary>
    /// File dependency graph for understanding change propagation
    /// </summary>
    public class DependencyGraph
    {
        public string RootFile { get; set; } = string.Empty;
        public List<FileDependency> Dependencies { get; set; } = new();
        public List<FileDependency> Dependents { get; set; } = new();
        public int MaxDepth { get; set; }
        public Dictionary<string, int> DependencyLevels { get; set; } = new();
    }

    /// <summary>
    /// Individual code element change
    /// </summary>
    public class CodeElementChange
    {
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public CodeElementType ElementType { get; set; }
        public ChangeType ChangeType { get; set; }
        public string? OldSignature { get; set; }
        public string? NewSignature { get; set; }
        public int LineNumber { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// Project-level changes
    /// </summary>
    public class ProjectChange
    {
        public string ProjectPath { get; set; } = string.Empty;
        public ChangeType ChangeType { get; set; }
        public List<string> AddedReferences { get; set; } = new();
        public List<string> RemovedReferences { get; set; } = new();
        public List<string> ModifiedReferences { get; set; } = new();
        public Dictionary<string, string> PropertyChanges { get; set; } = new();
    }

    /// <summary>
    /// File dependency information
    /// </summary>
    public class FileDependency
    {
        public string FilePath { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DependencyType Type { get; set; }
        public int Distance { get; set; }
        public List<string> ThroughSymbols { get; set; } = new();
    }

    /// <summary>
    /// Statistics about repository changes
    /// </summary>
    public class ChangeStatistics
    {
        public int TotalFilesChanged { get; set; }
        public int TotalLinesAdded { get; set; }
        public int TotalLinesRemoved { get; set; }
        public int TotalLinesModified { get; set; }
        public Dictionary<string, int> ChangesByExtension { get; set; } = new();
        public Dictionary<string, int> ChangesByType { get; set; } = new();
        public int ProjectsAffected { get; set; }
        public int SymbolsAffected { get; set; }
    }

    /// <summary>
    /// Update operation for incremental changes
    /// </summary>
    public class UpdateOperation
    {
        public string OperationId { get; set; } = string.Empty;
        public UpdateOperationType Type { get; set; }
        public string TargetPath { get; set; } = string.Empty;
        public UpdatePriority Priority { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
    }

    /// <summary>
    /// Type of change detected
    /// </summary>
    public enum ChangeType
    {
        Added,
        Modified,
        Deleted,
        Renamed,
        Moved,
        Unchanged
    }

    /// <summary>
    /// Type of code element
    /// </summary>
    public enum CodeElementType
    {
        Namespace,
        Class,
        Interface,
        Struct,
        Enum,
        Method,
        Property,
        Field,
        Event,
        Delegate
    }

    /// <summary>
    /// Type of dependency
    /// </summary>
    public enum DependencyType
    {
        Reference,
        Inheritance,
        Implementation,
        Usage,
        Import
    }

    /// <summary>
    /// Impact severity levels
    /// </summary>
    public enum ImpactSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Update operation types
    /// </summary>
    public enum UpdateOperationType
    {
        AnalyzeFile,
        UpdateNodes,
        UpdateRelationships,
        DeleteNodes,
        DeleteRelationships,
        InvalidateCache,
        ReindexSymbols
    }

    /// <summary>
    /// Update priority levels
    /// </summary>
    public enum UpdatePriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}