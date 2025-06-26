using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    /// <summary>
    /// Interface for building context-aware prompts for LLM analysis
    /// </summary>
    public interface IContextualPromptBuilder
    {
        /// <summary>
        /// Builds a context-aware analysis prompt for a specific analysis type
        /// </summary>
        Task<string> BuildAnalysisPromptAsync(
            string code, 
            GraphContext context, 
            string analysisType, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds a prompt for architectural analysis
        /// </summary>
        Task<string> BuildArchitecturalPromptAsync(
            GraphContext context, 
            string focus, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds a prompt for dependency analysis
        /// </summary>
        Task<string> BuildDependencyPromptAsync(
            string code, 
            IEnumerable<CodeElementInfo> dependencies, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds a prompt for integration analysis
        /// </summary>
        Task<string> BuildIntegrationPromptAsync(
            string code, 
            GraphContext context, 
            CancellationToken cancellationToken = default);
    }
}