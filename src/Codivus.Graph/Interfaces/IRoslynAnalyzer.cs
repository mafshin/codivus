using Microsoft.CodeAnalysis;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces;

/// <summary>
/// Interface for analyzing code using Roslyn and converting to graph representation
/// </summary>
public interface IRoslynAnalyzer
{
    /// <summary>
    /// Analyzes a single C# file and extracts symbols and relationships
    /// </summary>
    /// <param name="filePath">Path to the C# file to analyze</param>
    /// <param name="repositoryId">Repository identifier</param>
    /// <param name="projectPath">Optional project path for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis result containing nodes and relationships</returns>
    Task<CodeAnalysisResult> AnalyzeFileAsync(
        string filePath, 
        string repositoryId,
        string? projectPath = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyzes all C# files in a project
    /// </summary>
    /// <param name="projectPath">Path to the project file (.csproj)</param>
    /// <param name="repositoryId">Repository identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of analysis results for all files in the project</returns>
    Task<IEnumerable<CodeAnalysisResult>> AnalyzeProjectAsync(
        string projectPath,
        string repositoryId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyzes all projects in a solution
    /// </summary>
    /// <param name="solutionPath">Path to the solution file (.sln)</param>
    /// <param name="repositoryId">Repository identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of analysis results for all files in the solution</returns>
    Task<IEnumerable<CodeAnalysisResult>> AnalyzeSolutionAsync(
        string solutionPath,
        string repositoryId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extracts symbols from a syntax tree
    /// </summary>
    /// <param name="syntaxTree">The syntax tree to analyze</param>
    /// <param name="semanticModel">The semantic model for symbol resolution</param>
    /// <param name="fileId">File identifier</param>
    /// <param name="repositoryId">Repository identifier</param>
    /// <returns>Extracted code nodes</returns>
    IEnumerable<CodeNode> ExtractSymbols(
        SyntaxTree syntaxTree, 
        SemanticModel semanticModel, 
        string fileId,
        string repositoryId);
    
    /// <summary>
    /// Detects relationships between symbols
    /// </summary>
    /// <param name="syntaxTree">The syntax tree to analyze</param>
    /// <param name="semanticModel">The semantic model for symbol resolution</param>
    /// <param name="nodes">Previously extracted nodes</param>
    /// <returns>Detected relationships</returns>
    IEnumerable<CodeRelationship> DetectRelationships(
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        IEnumerable<CodeNode> nodes);
}