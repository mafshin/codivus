using System;
using System.Collections.Generic;
using Codivus.Core.Models;

namespace Codivus.Graph.Models
{
    /// <summary>
    /// Architectural analysis result
    /// </summary>
    public class ArchitecturalAnalysis
    {
        public string AnalysisId { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;
        public string ComponentPath { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
        
        public List<ArchitecturalPattern> DetectedPatterns { get; set; } = new();
        public List<ArchitecturalIssue> Issues { get; set; } = new();
        public List<DesignPrincipleViolation> PrincipleViolations { get; set; } = new();
        public CouplingAnalysis Coupling { get; set; } = new();
        public CohesionAnalysis Cohesion { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        
        public ArchitecturalMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// Integration analysis result
    /// </summary>
    public class IntegrationAnalysis
    {
        public string AnalysisId { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;
        public List<string> ComponentPaths { get; set; } = new();
        public DateTime AnalyzedAt { get; set; }
        
        public List<IntegrationIssue> Issues { get; set; } = new();
        public List<CrossCuttingConcern> CrossCuttingConcerns { get; set; } = new();
        public List<InterfaceContract> Contracts { get; set; } = new();
        public List<DataFlowIssue> DataFlowIssues { get; set; } = new();
        public List<CommunicationPattern> CommunicationPatterns { get; set; } = new();
        
        public IntegrationMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// Dependency analysis result
    /// </summary>
    public class DependencyAnalysis
    {
        public string AnalysisId { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;
        public string ComponentPath { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
        
        public List<DependencyInfo> Dependencies { get; set; } = new();
        public List<CircularDependency> CircularDependencies { get; set; } = new();
        public List<DependencyViolation> Violations { get; set; } = new();
        public DependencyGraph Graph { get; set; } = new();
        
        public DependencyMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// Enhanced scanning metrics
    /// </summary>
    public class EnhancedScanningMetrics
    {
        public string RepositoryId { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        
        public int TotalFilesScanned { get; set; }
        public int GraphContextsGenerated { get; set; }
        public int LLMAnalysisRequests { get; set; }
        public int IssuesFound { get; set; }
        public int ArchitecturalInsights { get; set; }
        
        public TimeSpan AverageContextExtractionTime { get; set; }
        public TimeSpan AverageLLMResponseTime { get; set; }
        public TimeSpan TotalAnalysisTime { get; set; }
        
        public Dictionary<string, int> IssuesByType { get; set; } = new();
        public Dictionary<string, int> InsightsByType { get; set; } = new();
        public Dictionary<string, double> QualityScores { get; set; } = new();
    }

    /// <summary>
    /// Architectural pattern detection
    /// </summary>
    public class ArchitecturalPattern
    {
        public string PatternType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public List<string> EvidenceElements { get; set; } = new();
        public List<string> Benefits { get; set; } = new();
        public List<string> Drawbacks { get; set; } = new();
    }

    /// <summary>
    /// Architectural issue
    /// </summary>
    public class ArchitecturalIssue
    {
        public string IssueType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> AffectedComponents { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
        public double ConfidenceScore { get; set; }
    }

    /// <summary>
    /// Design principle violation
    /// </summary>
    public class DesignPrincipleViolation
    {
        public string Principle { get; set; } = string.Empty; // SRP, OCP, LSP, ISP, DIP
        public string ViolationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ViolatingElements { get; set; } = new();
        public string Recommendation { get; set; } = string.Empty;
        public double Severity { get; set; }
    }

    /// <summary>
    /// Coupling analysis
    /// </summary>
    public class CouplingAnalysis
    {
        public double OverallCouplingScore { get; set; }
        public List<HighCouplingArea> HighCouplingAreas { get; set; } = new();
        public List<CouplingReduction> ReductionOpportunities { get; set; } = new();
        public Dictionary<string, double> ComponentCouplingScores { get; set; } = new();
    }

    /// <summary>
    /// Cohesion analysis
    /// </summary>
    public class CohesionAnalysis
    {
        public double OverallCohesionScore { get; set; }
        public List<LowCohesionArea> LowCohesionAreas { get; set; } = new();
        public List<CohesionImprovement> ImprovementOpportunities { get; set; } = new();
        public Dictionary<string, double> ComponentCohesionScores { get; set; } = new();
    }

    /// <summary>
    /// High coupling area
    /// </summary>
    public class HighCouplingArea
    {
        public string ComponentName { get; set; } = string.Empty;
        public double CouplingScore { get; set; }
        public List<string> CoupledWith { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public List<string> ImprovementSuggestions { get; set; } = new();
    }

    /// <summary>
    /// Coupling reduction opportunity
    /// </summary>
    public class CouplingReduction
    {
        public string Strategy { get; set; } = string.Empty;
        public List<string> AffectedComponents { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public double EstimatedImpact { get; set; }
        public int ImplementationEffort { get; set; }
    }

    /// <summary>
    /// Low cohesion area
    /// </summary>
    public class LowCohesionArea
    {
        public string ComponentName { get; set; } = string.Empty;
        public double CohesionScore { get; set; }
        public List<string> UnrelatedResponsibilities { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public List<string> SplittingSuggestions { get; set; } = new();
    }

    /// <summary>
    /// Cohesion improvement opportunity
    /// </summary>
    public class CohesionImprovement
    {
        public string Strategy { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double EstimatedImpact { get; set; }
        public int ImplementationEffort { get; set; }
    }

    /// <summary>
    /// Integration issue
    /// </summary>
    public class IntegrationIssue
    {
        public string IssueType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> InvolvedComponents { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
    }

    /// <summary>
    /// Cross-cutting concern
    /// </summary>
    public class CrossCuttingConcern
    {
        public string ConcernType { get; set; } = string.Empty; // Logging, Security, Caching, etc.
        public string Description { get; set; } = string.Empty;
        public List<string> AffectedComponents { get; set; } = new();
        public bool IsProperlyImplemented { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Interface contract
    /// </summary>
    public class InterfaceContract
    {
        public string InterfaceName { get; set; } = string.Empty;
        public List<string> Implementers { get; set; } = new();
        public List<string> Consumers { get; set; } = new();
        public List<ContractViolation> Violations { get; set; } = new();
        public double ContractStability { get; set; }
    }

    /// <summary>
    /// Contract violation
    /// </summary>
    public class ContractViolation
    {
        public string ViolationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ViolatingComponent { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data flow issue
    /// </summary>
    public class DataFlowIssue
    {
        public string IssueType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> DataPath { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Communication pattern
    /// </summary>
    public class CommunicationPattern
    {
        public string PatternType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ParticipatingComponents { get; set; } = new();
        public bool IsEfficient { get; set; }
        public List<string> ImprovementSuggestions { get; set; } = new();
    }

    /// <summary>
    /// Circular dependency
    /// </summary>
    public class CircularDependency
    {
        public string CycleId { get; set; } = string.Empty;
        public List<string> ComponentsInCycle { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public List<string> BreakingStrategies { get; set; } = new();
    }

    /// <summary>
    /// Dependency violation
    /// </summary>
    public class DependencyViolation
    {
        public string ViolationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DependentComponent { get; set; } = string.Empty;
        public string DependedComponent { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dependency graph representation
    /// </summary>
    public class DependencyGraph
    {
        public List<DependencyNode> Nodes { get; set; } = new();
        public List<DependencyEdge> Edges { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Dependency graph node
    /// </summary>
    public class DependencyNode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// Dependency graph edge
    /// </summary>
    public class DependencyEdge
    {
        public string FromId { get; set; } = string.Empty;
        public string ToId { get; set; } = string.Empty;
        public string DependencyType { get; set; } = string.Empty;
        public double Weight { get; set; }
    }

    /// <summary>
    /// Architectural metrics
    /// </summary>
    public class ArchitecturalMetrics
    {
        public double OverallQualityScore { get; set; }
        public double MaintainabilityIndex { get; set; }
        public double TechnicalDebtRatio { get; set; }
        public int CyclomaticComplexity { get; set; }
        public Dictionary<string, double> QualityAttributes { get; set; } = new();
    }

    /// <summary>
    /// Integration metrics
    /// </summary>
    public class IntegrationMetrics
    {
        public double IntegrationComplexity { get; set; }
        public int NumberOfIntegrationPoints { get; set; }
        public double CrossCuttingConcernCoverage { get; set; }
        public Dictionary<string, int> CommunicationPatternUsage { get; set; } = new();
    }

    /// <summary>
    /// Dependency metrics
    /// </summary>
    public class DependencyMetrics
    {
        public int TotalDependencies { get; set; }
        public int CircularDependencies { get; set; }
        public double AverageDepth { get; set; }
        public double InstabilityIndex { get; set; }
        public double AbstractnessIndex { get; set; }
        public Dictionary<string, int> DependenciesByType { get; set; } = new();
    }
}