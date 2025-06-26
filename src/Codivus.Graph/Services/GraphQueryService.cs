using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    public class GraphQueryService : IGraphQueryService
    {
        private readonly IGraphStorageService _graphStorageService;
        private readonly ILogger<GraphQueryService> _logger;

        public GraphQueryService(
            IGraphStorageService graphStorageService,
            ILogger<GraphQueryService> logger)
        {
            _graphStorageService = graphStorageService;
            _logger = logger;
        }

        public async Task<IEnumerable<CodeNode>> FindNodesByNameAsync(
            string repositoryId,
            string namePattern,
            NodeType? nodeType = null,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Finding nodes by name pattern {Pattern} in repository {RepositoryId}", namePattern, repositoryId);
            
            try
            {
                // For now, return a simple implementation - this would be enhanced with actual Gremlin queries
                var allNodes = nodeType.HasValue 
                    ? await _graphStorageService.GetNodesByTypeAsync(repositoryId, nodeType.Value, cancellationToken)
                    : new List<CodeNode>(); // Empty list for now - would implement repository-wide search

                var filteredNodes = allNodes.Where(n => 
                    (namePattern == "*" || n.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase)))
                    .Take(limit);

                return filteredNodes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding nodes by name pattern {Pattern}", namePattern);
                throw;
            }
        }

        public async Task<IEnumerable<CodeNode>> GetDependenciesAsync(
            string nodeId,
            int maxDepth = 1,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting dependencies for node {NodeId} with max depth {MaxDepth}", nodeId, maxDepth);
            
            try
            {
                var relationships = await _graphStorageService.GetRelationshipsAsync(nodeId, null, true, cancellationToken);
                var dependencyRelationships = relationships.Where(r => 
                    r.SourceNodeId == nodeId && 
                    (r.Type == RelationshipType.Uses || r.Type == RelationshipType.References || r.Type == RelationshipType.Dependency));

                var dependencies = new List<CodeNode>();
                foreach (var rel in dependencyRelationships)
                {
                    var node = await _graphStorageService.GetNodeAsync(rel.TargetNodeId, cancellationToken);
                    if (node != null)
                        dependencies.Add(node);
                }

                return dependencies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dependencies for node {NodeId}", nodeId);
                throw;
            }
        }

        public async Task<IEnumerable<CodeNode>> GetDependentsAsync(
            string nodeId,
            int maxDepth = 1,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting dependents for node {NodeId} with max depth {MaxDepth}", nodeId, maxDepth);
            
            try
            {
                var relationships = await _graphStorageService.GetRelationshipsAsync(nodeId, null, false, cancellationToken);
                var dependentRelationships = relationships.Where(r => 
                    r.TargetNodeId == nodeId && 
                    (r.Type == RelationshipType.Uses || r.Type == RelationshipType.References || r.Type == RelationshipType.Dependency));

                var dependents = new List<CodeNode>();
                foreach (var rel in dependentRelationships)
                {
                    var node = await _graphStorageService.GetNodeAsync(rel.SourceNodeId, cancellationToken);
                    if (node != null)
                        dependents.Add(node);
                }

                return dependents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dependents for node {NodeId}", nodeId);
                throw;
            }
        }

        public async Task<CallHierarchy> GetCallHierarchyAsync(
            string methodId,
            CallHierarchyDirection direction,
            int maxDepth = 3,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting call hierarchy for method {MethodId} in direction {Direction}", methodId, direction);
            
            try
            {
                var rootMethod = await _graphStorageService.GetNodeAsync(methodId, cancellationToken);
                if (rootMethod == null)
                    throw new ArgumentException($"Method {methodId} not found");

                var hierarchy = new CallHierarchy
                {
                    RootMethod = rootMethod,
                    Nodes = new List<CallHierarchyNode>()
                };

                // Simplified implementation - would be enhanced with recursive traversal
                var relationships = await _graphStorageService.GetRelationshipsAsync(methodId, RelationshipType.Calls, true, cancellationToken);
                var callRelationships = relationships.Where(r => r.Type == RelationshipType.Calls);

                foreach (var rel in callRelationships.Take(20)) // Limit for performance
                {
                    var targetNodeId = direction == CallHierarchyDirection.Callers ? rel.SourceNodeId : rel.TargetNodeId;
                    var targetNode = await _graphStorageService.GetNodeAsync(targetNodeId, cancellationToken);
                    
                    if (targetNode != null)
                    {
                        hierarchy.Nodes.Add(new CallHierarchyNode
                        {
                            Method = targetNode,
                            ParentId = methodId,
                            Depth = 1,
                            CallCount = 1
                        });
                    }
                }

                hierarchy.TotalNodes = hierarchy.Nodes.Count;
                hierarchy.MaxDepthReached = hierarchy.Nodes.Any() ? 1 : 0;

                return hierarchy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting call hierarchy for method {MethodId}", methodId);
                throw;
            }
        }

        public async Task<TypeHierarchy> GetTypeHierarchyAsync(
            string typeId,
            bool includeInterfaces = true,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting type hierarchy for type {TypeId}", typeId);
            
            try
            {
                var rootType = await _graphStorageService.GetNodeAsync(typeId, cancellationToken);
                if (rootType == null)
                    throw new ArgumentException($"Type {typeId} not found");

                var hierarchy = new TypeHierarchy
                {
                    RootType = rootType,
                    BaseTypes = new List<CodeNode>(),
                    DerivedTypes = new List<CodeNode>(),
                    ImplementedInterfaces = new List<CodeNode>(),
                    ImplementingTypes = new List<CodeNode>()
                };

                var relationships = await _graphStorageService.GetRelationshipsAsync(typeId, null, true, cancellationToken);

                // Base types (inheritance)
                var inheritanceRels = relationships.Where(r => r.SourceNodeId == typeId && r.Type == RelationshipType.Inherits);
                foreach (var rel in inheritanceRels)
                {
                    var baseType = await _graphStorageService.GetNodeAsync(rel.TargetNodeId, cancellationToken);
                    if (baseType != null)
                        hierarchy.BaseTypes.Add(baseType);
                }

                // Derived types
                var derivedRels = relationships.Where(r => r.TargetNodeId == typeId && r.Type == RelationshipType.Inherits);
                foreach (var rel in derivedRels)
                {
                    var derivedType = await _graphStorageService.GetNodeAsync(rel.SourceNodeId, cancellationToken);
                    if (derivedType != null)
                        hierarchy.DerivedTypes.Add(derivedType);
                }

                if (includeInterfaces)
                {
                    // Implemented interfaces
                    var implementsRels = relationships.Where(r => r.SourceNodeId == typeId && r.Type == RelationshipType.Implements);
                    foreach (var rel in implementsRels)
                    {
                        var interfaceType = await _graphStorageService.GetNodeAsync(rel.TargetNodeId, cancellationToken);
                        if (interfaceType != null)
                            hierarchy.ImplementedInterfaces.Add(interfaceType);
                    }

                    // Implementing types (if this is an interface)
                    var implementingRels = relationships.Where(r => r.TargetNodeId == typeId && r.Type == RelationshipType.Implements);
                    foreach (var rel in implementingRels)
                    {
                        var implementingType = await _graphStorageService.GetNodeAsync(rel.SourceNodeId, cancellationToken);
                        if (implementingType != null)
                            hierarchy.ImplementingTypes.Add(implementingType);
                    }
                }

                return hierarchy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting type hierarchy for type {TypeId}", typeId);
                throw;
            }
        }

        public async Task<ImpactAnalysisResult> AnalyzeImpactAsync(
            string nodeId,
            ImpactAnalysisOptions options = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Analyzing impact for node {NodeId}", nodeId);
            
            try
            {
                options ??= new ImpactAnalysisOptions();
                
                var sourceNode = await _graphStorageService.GetNodeAsync(nodeId, cancellationToken);
                if (sourceNode == null)
                    throw new ArgumentException($"Node {nodeId} not found");

                var result = new ImpactAnalysisResult
                {
                    SourceNode = sourceNode,
                    DirectlyImpacted = new List<ImpactedNode>(),
                    IndirectlyImpacted = new List<ImpactedNode>()
                };

                // Simplified impact analysis
                var dependents = await GetDependentsAsync(nodeId, options.MaxDepth, cancellationToken);
                foreach (var dependent in dependents)
                {
                    result.DirectlyImpacted.Add(new ImpactedNode
                    {
                        Node = dependent,
                        RelationshipType = RelationshipType.Uses,
                        Distance = 1,
                        ImpactWeight = 1.0
                    });
                }

                result.TotalImpactedNodes = result.DirectlyImpacted.Count + result.IndirectlyImpacted.Count;
                result.ImpactScore = result.TotalImpactedNodes * 0.5; // Simplified scoring

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing impact for node {NodeId}", nodeId);
                throw;
            }
        }

        public async Task<CouplingAnalysisResult> AnalyzeCouplingAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Analyzing coupling for project {ProjectId}", projectId);
            
            try
            {
                var result = new CouplingAnalysisResult
                {
                    ProjectId = projectId,
                    TypeCoupling = new Dictionary<string, CouplingMetrics>(),
                    Hotspots = new List<CouplingHotspot>()
                };

                // Simplified coupling analysis
                var typeNodes = await _graphStorageService.GetNodesByTypeAsync(projectId, NodeType.Type, cancellationToken);

                foreach (var typeNode in typeNodes)
                {
                    var dependencies = await GetDependenciesAsync(typeNode.Id, 1, cancellationToken);
                    var dependents = await GetDependentsAsync(typeNode.Id, 1, cancellationToken);

                    var afferent = dependents.Count();
                    var efferent = dependencies.Count();
                    var instability = efferent > 0 ? (double)efferent / (afferent + efferent) : 0;

                    result.TypeCoupling[typeNode.Id] = new CouplingMetrics
                    {
                        NodeId = typeNode.Id,
                        NodeName = typeNode.Name,
                        AfferentCoupling = afferent,
                        EfferentCoupling = efferent,
                        InstabilityIndex = instability
                    };
                }

                // Identify hotspots
                var highlyCoupled = result.TypeCoupling.Values
                    .Where(c => c.AfferentCoupling + c.EfferentCoupling > 10)
                    .OrderByDescending(c => c.AfferentCoupling + c.EfferentCoupling);

                foreach (var coupling in highlyCoupled.Take(5))
                {
                    result.Hotspots.Add(new CouplingHotspot
                    {
                        NodeId = coupling.NodeId,
                        NodeName = coupling.NodeName,
                        TotalCoupling = coupling.AfferentCoupling + coupling.EfferentCoupling,
                        Recommendation = "Consider refactoring to reduce coupling"
                    });
                }

                result.AverageCoupling = result.TypeCoupling.Values.Any() 
                    ? result.TypeCoupling.Values.Average(c => c.AfferentCoupling + c.EfferentCoupling) 
                    : 0;
                result.HighlyCoupledTypes = result.Hotspots.Count;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing coupling for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<IEnumerable<Dictionary<string, object>>> ExecuteCustomQueryAsync(
            string gremlinQuery,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Executing custom Gremlin query");
            
            try
            {
                // For now, return empty results - this would be implemented with actual Gremlin execution
                _logger.LogWarning("Custom Gremlin query execution not yet implemented");
                return new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing custom query");
                throw;
            }
        }

        public async Task<Subgraph> ExtractSubgraphAsync(
            string nodeId,
            SubgraphOptions options = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Extracting subgraph for node {NodeId}", nodeId);
            
            try
            {
                options ??= new SubgraphOptions();
                
                var subgraph = new Subgraph
                {
                    Nodes = new List<CodeNode>(),
                    Relationships = new List<CodeRelationship>()
                };

                // Start with the root node
                var rootNode = await _graphStorageService.GetNodeAsync(nodeId, cancellationToken);
                if (rootNode != null)
                {
                    subgraph.Nodes.Add(rootNode);

                    // Get connected nodes within the specified depth
                    var visited = new HashSet<string> { nodeId };
                    await ExpandSubgraphAsync(subgraph, nodeId, 0, options.MaxDepth, options, visited, cancellationToken);

                    // Limit nodes if necessary
                    if (subgraph.Nodes.Count > options.MaxNodes)
                    {
                        subgraph.Nodes = subgraph.Nodes.Take(options.MaxNodes).ToList();
                    }
                }

                return subgraph;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting subgraph for node {NodeId}", nodeId);
                throw;
            }
        }

        private async Task ExpandSubgraphAsync(
            Subgraph subgraph,
            string nodeId,
            int currentDepth,
            int maxDepth,
            SubgraphOptions options,
            HashSet<string> visited,
            CancellationToken cancellationToken)
        {
            if (currentDepth >= maxDepth || subgraph.Nodes.Count >= options.MaxNodes)
                return;

            var relationships = await _graphStorageService.GetRelationshipsAsync(nodeId, null, true, cancellationToken);
            
            // Filter relationships based on options
            var filteredRelationships = relationships.Where(r =>
                options.IncludedRelationshipTypes == null || 
                options.IncludedRelationshipTypes.Contains(r.Type));

            foreach (var relationship in filteredRelationships)
            {
                var connectedNodeId = relationship.SourceNodeId == nodeId ? relationship.TargetNodeId : relationship.SourceNodeId;
                
                if (!visited.Contains(connectedNodeId))
                {
                    var connectedNode = await _graphStorageService.GetNodeAsync(connectedNodeId, cancellationToken);
                    if (connectedNode != null && 
                        (options.IncludedNodeTypes == null || options.IncludedNodeTypes.Contains(connectedNode.NodeType)))
                    {
                        subgraph.Nodes.Add(connectedNode);
                        subgraph.Relationships.Add(relationship);
                        visited.Add(connectedNodeId);

                        // Recursively expand
                        await ExpandSubgraphAsync(subgraph, connectedNodeId, currentDepth + 1, maxDepth, options, visited, cancellationToken);
                    }
                }
                else if (visited.Contains(relationship.SourceNodeId) && visited.Contains(relationship.TargetNodeId))
                {
                    // Add relationship between already visited nodes
                    if (!subgraph.Relationships.Any(r => r.Id == relationship.Id))
                    {
                        subgraph.Relationships.Add(relationship);
                    }
                }
            }
        }
    }
}