using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    public interface IGraphQueryService
    {
        // Basic queries
        Task<IEnumerable<CodeNode>> FindNodesByNameAsync(
            string repositoryId,
            string namePattern,
            NodeType? nodeType = null,
            int limit = 100,
            CancellationToken cancellationToken = default);
        
        Task<IEnumerable<CodeNode>> GetDependenciesAsync(
            string nodeId,
            int maxDepth = 1,
            CancellationToken cancellationToken = default);
        
        Task<IEnumerable<CodeNode>> GetDependentsAsync(
            string nodeId,
            int maxDepth = 1,
            CancellationToken cancellationToken = default);
        
        // Call hierarchy
        Task<CallHierarchy> GetCallHierarchyAsync(
            string methodId,
            CallHierarchyDirection direction,
            int maxDepth = 3,
            CancellationToken cancellationToken = default);
        
        // Type hierarchy
        Task<TypeHierarchy> GetTypeHierarchyAsync(
            string typeId,
            bool includeInterfaces = true,
            CancellationToken cancellationToken = default);
        
        // Impact analysis
        Task<ImpactAnalysisResult> AnalyzeImpactAsync(
            string nodeId,
            ImpactAnalysisOptions options = null,
            CancellationToken cancellationToken = default);
        
        // Coupling analysis
        Task<CouplingAnalysisResult> AnalyzeCouplingAsync(
            string projectId,
            CancellationToken cancellationToken = default);
        
        // Custom queries
        Task<IEnumerable<Dictionary<string, object>>> ExecuteCustomQueryAsync(
            string cypherQuery,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default);
        
        // Subgraph extraction
        Task<Subgraph> ExtractSubgraphAsync(
            string nodeId,
            SubgraphOptions options = null,
            CancellationToken cancellationToken = default);
    }

    public enum CallHierarchyDirection
    {
        Callers,    // Who calls this method
        Callees,    // What this method calls
        Both
    }

    public class CallHierarchy
    {
        public CodeNode RootMethod { get; set; }
        public List<CallHierarchyNode> Nodes { get; set; } = new();
        public int TotalNodes { get; set; }
        public int MaxDepthReached { get; set; }
    }

    public class CallHierarchyNode
    {
        public CodeNode Method { get; set; }
        public string ParentId { get; set; }
        public int Depth { get; set; }
        public int CallCount { get; set; }
        public List<string> CallLocations { get; set; } = new();
    }

    public class TypeHierarchy
    {
        public CodeNode RootType { get; set; }
        public List<CodeNode> BaseTypes { get; set; } = new();
        public List<CodeNode> DerivedTypes { get; set; } = new();
        public List<CodeNode> ImplementedInterfaces { get; set; } = new();
        public List<CodeNode> ImplementingTypes { get; set; } = new();
    }

    public class ImpactAnalysisResult
    {
        public CodeNode SourceNode { get; set; }
        public List<ImpactedNode> DirectlyImpacted { get; set; } = new();
        public List<ImpactedNode> IndirectlyImpacted { get; set; } = new();
        public Dictionary<string, int> ImpactByNodeType { get; set; } = new();
        public int TotalImpactedNodes { get; set; }
        public double ImpactScore { get; set; }
    }

    public class ImpactedNode
    {
        public CodeNode Node { get; set; }
        public RelationshipType RelationshipType { get; set; }
        public int Distance { get; set; }
        public double ImpactWeight { get; set; }
        public List<string> ImpactPath { get; set; } = new();
    }

    public class ImpactAnalysisOptions
    {
        public int MaxDepth { get; set; } = 3;
        public bool IncludeTests { get; set; } = true;
        public bool IncludeIndirectDependencies { get; set; } = true;
        public HashSet<RelationshipType> ConsideredRelationships { get; set; }
    }

    public class CouplingAnalysisResult
    {
        public string ProjectId { get; set; }
        public Dictionary<string, CouplingMetrics> TypeCoupling { get; set; } = new();
        public List<CouplingHotspot> Hotspots { get; set; } = new();
        public double AverageCoupling { get; set; }
        public int HighlyCoupledTypes { get; set; }
    }

    public class CouplingMetrics
    {
        public string NodeId { get; set; }
        public string NodeName { get; set; }
        public int AfferentCoupling { get; set; } // Incoming dependencies
        public int EfferentCoupling { get; set; } // Outgoing dependencies
        public double InstabilityIndex { get; set; } // Efferent / (Afferent + Efferent)
        public List<string> CoupledNodeIds { get; set; } = new();
    }

    public class CouplingHotspot
    {
        public string NodeId { get; set; }
        public string NodeName { get; set; }
        public int TotalCoupling { get; set; }
        public string Recommendation { get; set; }
    }

    public class Subgraph
    {
        public List<CodeNode> Nodes { get; set; } = new();
        public List<CodeRelationship> Relationships { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class SubgraphOptions
    {
        public int MaxDepth { get; set; } = 2;
        public int MaxNodes { get; set; } = 100;
        public HashSet<NodeType> IncludedNodeTypes { get; set; }
        public HashSet<RelationshipType> IncludedRelationshipTypes { get; set; }
        public bool IncludeMetrics { get; set; } = false;
    }
}