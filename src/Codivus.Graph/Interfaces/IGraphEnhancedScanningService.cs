using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Core.Models;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    /// <summary>
    /// Interface for graph-enhanced code scanning with LLM integration
    /// </summary>
    public interface IGraphEnhancedScanningService
    {
        /// <summary>
        /// Performs enhanced scanning of a single file with graph context
        /// </summary>
        Task<GraphEnhancedAnalysis> ScanFileWithContextAsync(
            string repositoryId, 
            string filePath, 
            Models.GraphScanConfiguration? configuration = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs enhanced scanning of multiple files with shared context
        /// </summary>
        Task<IEnumerable<GraphEnhancedAnalysis>> ScanFilesWithContextAsync(
            string repositoryId, 
            IEnumerable<string> filePaths, 
            Models.GraphScanConfiguration? configuration = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs architectural analysis of a component or namespace
        /// </summary>
        Task<ArchitecturalAnalysis> AnalyzeArchitectureAsync(
            string repositoryId, 
            string componentPath, 
            ArchitecturalAnalysisOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes integration points and cross-component issues
        /// </summary>
        Task<IntegrationAnalysis> AnalyzeIntegrationAsync(
            string repositoryId, 
            IEnumerable<string> componentPaths,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs dependency analysis with graph context
        /// </summary>
        Task<DependencyAnalysis> AnalyzeDependenciesAsync(
            string repositoryId, 
            string componentPath,
            int maxDepth = 3,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets enhanced scanning statistics and metrics
        /// </summary>
        Task<EnhancedScanningMetrics> GetMetricsAsync(string repositoryId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Configuration options for architectural analysis
    /// </summary>
    public class ArchitecturalAnalysisOptions
    {
        public bool IncludePatternDetection { get; set; } = true;
        public bool AnalyzeCoupling { get; set; } = true;
        public bool CheckSOLIDPrinciples { get; set; } = true;
        public bool IdentifyCodeSmells { get; set; } = true;
        public int MaxDepth { get; set; } = 3;
        public List<string> FocusAreas { get; set; } = new();
    }
}