using System;
using System.Collections.Generic;

namespace Codivus.Graph.Models
{
    /// <summary>
    /// Represents a contextual subgraph extracted around a code element
    /// </summary>
    public class GraphContext
    {
        public string RepositoryId { get; set; } = string.Empty;
        public string FocusElementId { get; set; } = string.Empty;
        public string FocusFilePath { get; set; } = string.Empty;
        public int MaxDepth { get; set; }
        public DateTime ExtractedAt { get; set; }
        
        public List<CodeNode> Nodes { get; set; } = new();
        public List<CodeRelationship> Relationships { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        public ContextStatistics Statistics { get; set; } = new();
    }

    /// <summary>
    /// Statistics about the extracted graph context
    /// </summary>
    public class ContextStatistics
    {
        public int TotalNodes { get; set; }
        public int TotalRelationships { get; set; }
        public Dictionary<NodeType, int> NodesByType { get; set; } = new();
        public Dictionary<RelationshipType, int> RelationshipsByType { get; set; } = new();
        public int MaxDepthReached { get; set; }
        public List<string> IncludedFiles { get; set; } = new();
    }

    /// <summary>
    /// Graph embeddings for LLM consumption
    /// </summary>
    public class GraphEmbedding
    {
        public string ContextId { get; set; } = string.Empty;
        public string SerializedGraph { get; set; } = string.Empty;
        public string ArchitecturalSummary { get; set; } = string.Empty;
        public List<DependencyInfo> Dependencies { get; set; } = new();
        public List<string> KeyConcepts { get; set; } = new();
        public Dictionary<string, object> EmbeddingMetadata { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// Information about a code element in context
    /// </summary>
    public class CodeElementInfo
    {
        public string ElementId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public NodeType Type { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string Signature { get; set; } = string.Empty;
        public string Documentation { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        public double RelevanceScore { get; set; }
    }

    /// <summary>
    /// Dependency information for context
    /// </summary>
    public class DependencyInfo
    {
        public string FromElement { get; set; } = string.Empty;
        public string ToElement { get; set; } = string.Empty;
        public RelationshipType DependencyType { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsCritical { get; set; }
        public string Impact { get; set; } = string.Empty;
    }

    /// <summary>
    /// Architectural summary of the graph context
    /// </summary>
    public class ArchitecturalSummary
    {
        public string Pattern { get; set; } = string.Empty;
        public List<string> Components { get; set; } = new();
        public List<string> Layers { get; set; } = new();
        public List<DependencyInfo> KeyDependencies { get; set; } = new();
        public List<string> PotentialIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    /// <summary>
    /// Enhanced code analysis result with graph context
    /// </summary>
    public class GraphEnhancedAnalysis
    {
        public string AnalysisId { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
        
        public GraphContext Context { get; set; } = new();
        public List<ContextualIssue> Issues { get; set; } = new();
        public ArchitecturalSummary Architecture { get; set; } = new();
        public List<IntegrationInsight> Insights { get; set; } = new();
        
        public AnalysisMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// A code issue detected with graph context
    /// </summary>
    public class ContextualIssue
    {
        public string IssueId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string CodeSnippet { get; set; } = string.Empty;
        
        public List<string> RelatedElements { get; set; } = new();
        public List<string> AffectedComponents { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
        
        public double ConfidenceScore { get; set; }
        public Dictionary<string, object> ContextualEvidence { get; set; } = new();
    }

    /// <summary>
    /// Architectural or integration insight from graph analysis
    /// </summary>
    public class IntegrationInsight
    {
        public string InsightId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        public List<string> InvolvedElements { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        
        public double ImportanceScore { get; set; }
        public Dictionary<string, object> Evidence { get; set; } = new();
    }

    /// <summary>
    /// Metrics about the analysis process
    /// </summary>
    public class AnalysisMetrics
    {
        public TimeSpan ContextExtractionTime { get; set; }
        public TimeSpan EmbeddingGenerationTime { get; set; }
        public TimeSpan LLMAnalysisTime { get; set; }
        public TimeSpan TotalAnalysisTime { get; set; }
        
        public int NodesAnalyzed { get; set; }
        public int RelationshipsAnalyzed { get; set; }
        public int IssuesFound { get; set; }
        public int InsightsGenerated { get; set; }
        
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    /// <summary>
    /// Configuration for enhanced scanning
    /// </summary>
    public class GraphScanConfiguration
    {
        public int MaxDepth { get; set; } = 2;
        public string[] AnalysisTypes { get; set; } = { "general", "architecture", "integration" };
        public bool IncludeArchitecturalAnalysis { get; set; } = true;
        public bool IncludeIntegrationAnalysis { get; set; } = true;
        public bool IncludeDependencyAnalysis { get; set; } = true;
        public int MaxNodes { get; set; } = 1000;
        public int MaxRelationships { get; set; } = 2000;
        public Dictionary<string, object> CustomOptions { get; set; } = new();
    }
}