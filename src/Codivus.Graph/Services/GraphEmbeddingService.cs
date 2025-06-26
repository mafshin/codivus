using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// Service for generating graph embeddings and extracting contextual subgraphs
    /// </summary>
    public class GraphEmbeddingService : IGraphEmbeddingService
    {
        private readonly IGraphQueryService _graphQueryService;
        private readonly IGraphStorageService _graphStorageService;
        private readonly ILogger<GraphEmbeddingService> _logger;

        public GraphEmbeddingService(
            IGraphQueryService graphQueryService,
            IGraphStorageService graphStorageService,
            ILogger<GraphEmbeddingService> logger)
        {
            _graphQueryService = graphQueryService;
            _graphStorageService = graphStorageService;
            _logger = logger;
        }

        public async Task<GraphContext> ExtractContextAsync(string repositoryId, string filePath, int maxDepth = 2, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogDebug("Extracting graph context for {FilePath} with max depth {MaxDepth}", filePath, maxDepth);

            var context = new GraphContext
            {
                RepositoryId = repositoryId,
                FocusFilePath = filePath,
                MaxDepth = maxDepth,
                ExtractedAt = startTime
            };

            try
            {
                // Find nodes in the target file by searching for file path in properties
                var fileNodes = await _graphQueryService.FindNodesByNameAsync(repositoryId, filePath, null, 1000, cancellationToken);
                var focusNodes = new List<CodeNode>();
                
                // Filter nodes that belong to this file
                foreach (var node in fileNodes)
                {
                    if (node.Properties.TryGetValue("filePath", out var nodeFilePath) && 
                        nodeFilePath?.ToString() == filePath)
                    {
                        focusNodes.Add(node);
                    }
                }

                if (!focusNodes.Any())
                {
                    _logger.LogWarning("No nodes found for file {FilePath} in repository {RepositoryId}", filePath, repositoryId);
                    return context;
                }

                // Set the primary focus element (typically the main class or first significant element)
                var primaryNode = focusNodes.FirstOrDefault(n => n.NodeType == NodeType.Type) ?? focusNodes.First();
                context.FocusElementId = primaryNode.Id;

                // Use the ExtractSubgraphAsync method to get the context
                var subgraphOptions = new SubgraphOptions
                {
                    MaxDepth = maxDepth,
                    MaxNodes = 1000,
                    IncludeMetrics = true
                };

                var subgraph = await _graphQueryService.ExtractSubgraphAsync(primaryNode.Id, subgraphOptions, cancellationToken);

                // Populate context from subgraph
                context.Nodes = subgraph.Nodes.ToList();
                context.Relationships = subgraph.Relationships.ToList();
                context.Statistics = GenerateStatistics(context.Nodes, context.Relationships, maxDepth);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogDebug("Context extraction completed in {Duration}ms. Found {NodeCount} nodes and {RelCount} relationships",
                    duration.TotalMilliseconds, context.Nodes.Count, context.Relationships.Count);

                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting graph context for {FilePath}", filePath);
                throw;
            }
        }

        public async Task<GraphEmbedding> GenerateEmbeddingsAsync(GraphContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Generating embeddings for context with {NodeCount} nodes", context.Nodes.Count);

            var embedding = new GraphEmbedding
            {
                ContextId = Guid.NewGuid().ToString(),
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                // Serialize graph structure
                embedding.SerializedGraph = await SerializeContextForLLMAsync(context, cancellationToken);

                // Generate architectural summary
                var architecture = await AnalyzeArchitectureAsync(context, cancellationToken);
                embedding.ArchitecturalSummary = JsonSerializer.Serialize(architecture, new JsonSerializerOptions { WriteIndented = true });

                // Extract dependencies
                embedding.Dependencies = ExtractDependencies(context);

                // Extract key concepts
                embedding.KeyConcepts = ExtractKeyConcepts(context);

                // Add metadata
                embedding.EmbeddingMetadata = new Dictionary<string, object>
                {
                    ["nodeCount"] = context.Nodes.Count,
                    ["relationshipCount"] = context.Relationships.Count,
                    ["focusFile"] = context.FocusFilePath,
                    ["maxDepth"] = context.MaxDepth,
                    ["extractedAt"] = context.ExtractedAt
                };

                _logger.LogDebug("Generated embeddings for context {ContextId}", embedding.ContextId);
                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embeddings for context");
                throw;
            }
        }

        public async Task<string> SerializeContextForLLMAsync(GraphContext context, CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Code Graph Context");
            sb.AppendLine($"Repository: {context.RepositoryId}");
            sb.AppendLine($"Focus File: {context.FocusFilePath}");
            sb.AppendLine($"Analysis Depth: {context.MaxDepth}");
            sb.AppendLine();

            // Group nodes by file for better organization
            var nodesByFile = context.Nodes.GroupBy(n => n.Properties.GetValueOrDefault("filePath", "unknown").ToString());

            foreach (var fileGroup in nodesByFile)
            {
                sb.AppendLine($"## File: {fileGroup.Key}");
                
                foreach (var node in fileGroup.OrderBy(n => n.NodeType))
                {
                    sb.AppendLine($"- **{node.NodeType}** `{node.Name}` ({node.FullName})");
                    // Add signature for methods
                    if (node.NodeType == NodeType.Method && !string.IsNullOrEmpty(node.Signature))
                    {
                        sb.AppendLine($"  > Signature: {node.Signature}");
                    }
                }
                sb.AppendLine();
            }

            // Add relationships
            if (context.Relationships.Any())
            {
                sb.AppendLine("## Relationships");
                var groupedRels = context.Relationships.GroupBy(r => r.Type);
                
                foreach (var relGroup in groupedRels)
                {
                    sb.AppendLine($"### {relGroup.Key}");
                    foreach (var rel in relGroup)
                    {
                        var sourceNode = context.Nodes.FirstOrDefault(n => n.Id == rel.SourceNodeId);
                        var targetNode = context.Nodes.FirstOrDefault(n => n.Id == rel.TargetNodeId);
                        
                        if (sourceNode != null && targetNode != null)
                        {
                            sb.AppendLine($"- `{sourceNode.Name}` → `{targetNode.Name}`");
                        }
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public async Task<IEnumerable<CodeElementInfo>> FindRelatedElementsAsync(string repositoryId, string elementId, int maxResults = 10, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Finding related elements for {ElementId}", elementId);

            var relatedElements = new List<CodeElementInfo>();

            try
            {
                // Use GetDependenciesAsync and GetDependentsAsync to find related elements
                var dependencies = await _graphQueryService.GetDependenciesAsync(elementId, 1, cancellationToken);
                var dependents = await _graphQueryService.GetDependentsAsync(elementId, 1, cancellationToken);

                var allRelatedNodes = dependencies.Concat(dependents).Distinct().Take(maxResults);

                foreach (var node in allRelatedNodes)
                {
                    var elementInfo = new CodeElementInfo
                    {
                        ElementId = node.Id,
                        Name = node.Name,
                        FullName = node.FullName,
                        Type = node.NodeType,
                        FilePath = node.Properties.GetValueOrDefault("filePath", "").ToString(),
                        Signature = node.Signature ?? node.Properties.GetValueOrDefault("signature", "").ToString(),
                        Documentation = string.Empty, // No documentation property available
                        RelevanceScore = CalculateRelevanceScore(node, dependencies, dependents)
                    };

                    relatedElements.Add(elementInfo);
                }

                return relatedElements.OrderByDescending(e => e.RelevanceScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding related elements for {ElementId}", elementId);
                return Enumerable.Empty<CodeElementInfo>();
            }
        }

        public async Task<ArchitecturalSummary> AnalyzeArchitectureAsync(GraphContext context, CancellationToken cancellationToken = default)
        {
            var summary = new ArchitecturalSummary();

            // Analyze architectural patterns
            var typeNodes = context.Nodes.Where(n => n.NodeType == NodeType.Type).ToList();
            var namespaces = typeNodes.Select(n => ExtractNamespace(n.FullName)).Distinct().ToList();

            summary.Components = typeNodes.Select(n => n.Name).ToList();
            summary.Layers = namespaces;

            // Identify key dependencies
            summary.KeyDependencies = ExtractDependencies(context);

            // Detect potential issues
            summary.PotentialIssues = DetectArchitecturalIssues(context);

            // Generate recommendations
            summary.Recommendations = GenerateRecommendations(context, summary.PotentialIssues);

            // Calculate metrics
            summary.Metrics = new Dictionary<string, object>
            {
                ["totalTypes"] = typeNodes.Count,
                ["totalNamespaces"] = namespaces.Count,
                ["averageDependenciesPerType"] = typeNodes.Count > 0 ? context.Relationships.Count / (double)typeNodes.Count : 0,
                ["complexityScore"] = CalculateComplexityScore(context)
            };

            // Determine architectural pattern
            summary.Pattern = DetectArchitecturalPattern(context, namespaces, summary.KeyDependencies);

            return summary;
        }

        private ContextStatistics GenerateStatistics(List<CodeNode> nodes, List<CodeRelationship> relationships, int maxDepth)
        {
            return new ContextStatistics
            {
                TotalNodes = nodes.Count,
                TotalRelationships = relationships.Count,
                NodesByType = nodes.GroupBy(n => n.NodeType).ToDictionary(g => g.Key, g => g.Count()),
                RelationshipsByType = relationships.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count()),
                MaxDepthReached = maxDepth,
                IncludedFiles = nodes.Select(n => n.Properties.GetValueOrDefault("filePath", "").ToString())
                    .Where(f => !string.IsNullOrEmpty(f))
                    .Distinct()
                    .ToList()
            };
        }

        private List<DependencyInfo> ExtractDependencies(GraphContext context)
        {
            return context.Relationships
                .Where(r => r.Type == RelationshipType.Uses || r.Type == RelationshipType.Calls || r.Type == RelationshipType.Inherits)
                .Select(r => new DependencyInfo
                {
                    FromElement = GetNodeName(context, r.SourceNodeId),
                    ToElement = GetNodeName(context, r.TargetNodeId),
                    DependencyType = r.Type,
                    Description = GetRelationshipDescription(r),
                    IsCritical = IsCriticalDependency(r, context)
                })
                .ToList();
        }

        private List<string> ExtractKeyConcepts(GraphContext context)
        {
            var concepts = new HashSet<string>();

            // Add type names
            foreach (var node in context.Nodes.Where(n => n.NodeType == NodeType.Type))
            {
                concepts.Add(node.Name);
                concepts.Add(ExtractNamespace(node.FullName));
            }

            // Add method names for important methods
            foreach (var node in context.Nodes.Where(n => n.NodeType == NodeType.Method))
            {
                if (IsImportantMethod(node, context))
                {
                    concepts.Add(node.Name);
                }
            }

            return concepts.Where(c => !string.IsNullOrEmpty(c)).ToList();
        }

        private double CalculateRelevanceScore(CodeNode node, IEnumerable<CodeNode> dependencies, IEnumerable<CodeNode> dependents)
        {
            var score = 0.0;

            // Base score by node type
            score += node.NodeType switch
            {
                NodeType.Type => 10.0,
                NodeType.Method => 5.0,
                NodeType.Property => 3.0,
                _ => 1.0
            };

            // Relationship count factor
            score += dependencies.Count() * 2.0;
            score += dependents.Count() * 1.5;

            return score;
        }

        private string ExtractNamespace(string fullName)
        {
            var lastDot = fullName.LastIndexOf('.');
            return lastDot > 0 ? fullName.Substring(0, lastDot) : "Global";
        }

        private List<string> DetectArchitecturalIssues(GraphContext context)
        {
            var issues = new List<string>();

            // Check for circular dependencies
            if (HasCircularDependencies(context))
            {
                issues.Add("Potential circular dependencies detected");
            }

            // Check for excessive coupling
            var avgDependencies = context.Relationships.Count / (double)Math.Max(1, context.Nodes.Count);
            if (avgDependencies > 10)
            {
                issues.Add("High coupling detected - consider reducing dependencies");
            }

            // Check for god classes
            var typeNodes = context.Nodes.Where(n => n.NodeType == NodeType.Type).ToList();
            foreach (var type in typeNodes)
            {
                var methodCount = context.Relationships.Count(r => r.SourceNodeId == type.Id && r.Type == RelationshipType.Contains);
                if (methodCount > 20)
                {
                    issues.Add($"Potential god class detected: {type.Name}");
                }
            }

            return issues;
        }

        private List<string> GenerateRecommendations(GraphContext context, List<string> issues)
        {
            var recommendations = new List<string>();

            if (issues.Any(i => i.Contains("circular")))
            {
                recommendations.Add("Consider using dependency injection to break circular dependencies");
            }

            if (issues.Any(i => i.Contains("coupling")))
            {
                recommendations.Add("Apply the Single Responsibility Principle to reduce coupling");
                recommendations.Add("Consider using interfaces to decouple implementations");
            }

            if (issues.Any(i => i.Contains("god class")))
            {
                recommendations.Add("Break large classes into smaller, focused components");
                recommendations.Add("Consider using composition over inheritance");
            }

            return recommendations;
        }

        private double CalculateComplexityScore(GraphContext context)
        {
            var nodeWeight = context.Nodes.Count * 1.0;
            var relationshipWeight = context.Relationships.Count * 2.0;
            var typeComplexity = context.Nodes.Count(n => n.NodeType == NodeType.Type) * 5.0;

            return (nodeWeight + relationshipWeight + typeComplexity) / 100.0;
        }

        private string DetectArchitecturalPattern(GraphContext context, List<string> namespaces, List<DependencyInfo> dependencies)
        {
            if (namespaces.Any(ns => ns.Contains("Controller")) && namespaces.Any(ns => ns.Contains("Service")))
            {
                return "MVC/Layered Architecture";
            }

            if (dependencies.Any(d => d.DependencyType == RelationshipType.Inherits))
            {
                return "Object-Oriented Inheritance";
            }

            if (context.Nodes.Any(n => n.Name.Contains("Factory") || n.Name.Contains("Builder")))
            {
                return "Creational Pattern";
            }

            return "Procedural/Simple";
        }

        private string GetNodeName(GraphContext context, string nodeId)
        {
            return context.Nodes.FirstOrDefault(n => n.Id == nodeId)?.Name ?? "Unknown";
        }

        private string GetRelationshipDescription(CodeRelationship relationship)
        {
            return relationship.Type switch
            {
                RelationshipType.Uses => "uses",
                RelationshipType.Calls => "calls",
                RelationshipType.Inherits => "inherits from",
                RelationshipType.Implements => "implements",
                RelationshipType.Contains => "contains",
                _ => "relates to"
            };
        }

        private bool IsCriticalDependency(CodeRelationship relationship, GraphContext context)
        {
            return relationship.Type == RelationshipType.Inherits || 
                   relationship.Type == RelationshipType.Implements ||
                   relationship.Properties.GetValueOrDefault("critical", false).ToString().ToLower() == "true";
        }

        private bool IsImportantMethod(CodeNode method, GraphContext context)
        {
            // Consider public methods and methods with many relationships as important
            var isPublic = method.Properties.GetValueOrDefault("accessibility", "").ToString() == "Public";
            var relationshipCount = context.Relationships.Count(r => r.SourceNodeId == method.Id || r.TargetNodeId == method.Id);
            
            return isPublic || relationshipCount > 3;
        }

        private bool HasCircularDependencies(GraphContext context)
        {
            // Simple cycle detection using DFS
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var node in context.Nodes)
            {
                if (!visited.Contains(node.Id))
                {
                    if (HasCycleDFS(node.Id, context, visited, recursionStack))
                        return true;
                }
            }

            return false;
        }

        private bool HasCycleDFS(string nodeId, GraphContext context, HashSet<string> visited, HashSet<string> recursionStack)
        {
            visited.Add(nodeId);
            recursionStack.Add(nodeId);

            var outgoingRels = context.Relationships.Where(r => r.SourceNodeId == nodeId);
            foreach (var rel in outgoingRels)
            {
                var targetId = rel.TargetNodeId;
                
                if (!visited.Contains(targetId))
                {
                    if (HasCycleDFS(targetId, context, visited, recursionStack))
                        return true;
                }
                else if (recursionStack.Contains(targetId))
                {
                    return true; // Cycle found
                }
            }

            recursionStack.Remove(nodeId);
            return false;
        }
    }
}