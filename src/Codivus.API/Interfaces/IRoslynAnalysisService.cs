using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.API.Interfaces
{
    /// <summary>
    /// Interface for Roslyn-based code analysis service
    /// </summary>
    public interface IRoslynAnalysisService
    {
        /// <summary>
        /// Analyzes a single file and returns the analysis result
        /// </summary>
        Task<CodeAnalysisResult> AnalyzeFileAsync(
            string filePath, 
            string repositoryId,
            string? projectPath = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes all files in a project
        /// </summary>
        Task<IEnumerable<CodeAnalysisResult>> AnalyzeProjectAsync(
            string projectPath,
            string repositoryId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes all projects in a solution
        /// </summary>
        Task<IEnumerable<CodeAnalysisResult>> AnalyzeSolutionAsync(
            string solutionPath,
            string repositoryId,
            CancellationToken cancellationToken = default);
    }
}