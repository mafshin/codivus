using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    /// <summary>
    /// Interface for generating graph embeddings and extracting contextual subgraphs
    /// </summary>
    public interface IGraphEmbeddingService
    {
        /// <summary>
        /// Extracts a contextual subgraph around a specific file or code element
        /// </summary>
        Task<GraphContext> ExtractContextAsync(string repositoryId, string filePath, int maxDepth = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates embeddings for a graph context suitable for LLM consumption
        /// </summary>
        Task<GraphEmbedding> GenerateEmbeddingsAsync(GraphContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts a graph context into a text representation for LLM prompts
        /// </summary>
        Task<string> SerializeContextForLLMAsync(GraphContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds related code elements based on dependencies and relationships
        /// </summary>
        Task<IEnumerable<CodeElementInfo>> FindRelatedElementsAsync(string repositoryId, string elementId, int maxResults = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a summary of architectural relationships in the context
        /// </summary>
        Task<ArchitecturalSummary> AnalyzeArchitectureAsync(GraphContext context, CancellationToken cancellationToken = default);
    }

}