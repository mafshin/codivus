using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    public interface ICodeGraphBuilder
    {
        Task<GraphBuildResult> BuildGraphAsync(
            string repositoryId, 
            IEnumerable<CodeAnalysisResult> analysisResults,
            GraphBuildOptions options = null,
            CancellationToken cancellationToken = default);
        
        Task<GraphUpdateResult> UpdateGraphAsync(
            string repositoryId,
            IEnumerable<CodeAnalysisResult> analysisResults,
            GraphUpdateOptions options = null,
            CancellationToken cancellationToken = default);
        
        Task<bool> RemoveFromGraphAsync(
            string repositoryId,
            IEnumerable<string> fileIds,
            CancellationToken cancellationToken = default);
    }

    public class GraphBuildOptions
    {
        public bool ClearExisting { get; set; } = false;
        public int BatchSize { get; set; } = 1000;
        public bool BuildRelationships { get; set; } = true;
        public bool CalculateMetrics { get; set; } = true;
        public IProgress<GraphBuildProgress> Progress { get; set; }
    }

    public class GraphUpdateOptions : GraphBuildOptions
    {
        public bool IncrementalUpdate { get; set; } = true;
        public bool VerifyExisting { get; set; } = false;
    }

    public class GraphBuildResult
    {
        public string RepositoryId { get; set; }
        public int NodesCreated { get; set; }
        public int NodesUpdated { get; set; }
        public int NodesDeleted { get; set; }
        public int RelationshipsCreated { get; set; }
        public int RelationshipsDeleted { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public GraphMetrics Metrics { get; set; }
    }

    public class GraphUpdateResult : GraphBuildResult
    {
        public int FilesProcessed { get; set; }
        public int FilesSkipped { get; set; }
    }

    public class GraphBuildProgress
    {
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public string CurrentItem { get; set; }
        public string Phase { get; set; } // "Analysis", "Nodes", "Relationships", "Metrics"
        public double PercentComplete => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
    }

}