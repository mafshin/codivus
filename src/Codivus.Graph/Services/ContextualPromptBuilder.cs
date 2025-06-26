using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// Service for building context-aware prompts for LLM analysis
    /// </summary>
    public class ContextualPromptBuilder : IContextualPromptBuilder
    {
        private readonly ILogger<ContextualPromptBuilder> _logger;

        public ContextualPromptBuilder(ILogger<ContextualPromptBuilder> logger)
        {
            _logger = logger;
        }

        public async Task<string> BuildAnalysisPromptAsync(string code, GraphContext context, string analysisType, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Building analysis prompt for {AnalysisType}", analysisType);

            var prompt = new StringBuilder();

            // System prompt
            prompt.AppendLine("You are an expert code analyzer with deep understanding of software architecture and design patterns.");
            prompt.AppendLine($"Analyze the provided code for {analysisType} issues, considering the architectural context and relationships.");
            prompt.AppendLine();

            // Context information
            prompt.AppendLine("## Architectural Context");
            prompt.AppendLine($"**Repository:** {context.RepositoryId}");
            prompt.AppendLine($"**Focus File:** {context.FocusFilePath}");
            prompt.AppendLine($"**Analysis Scope:** {context.Nodes.Count} related components");
            prompt.AppendLine();

            // Add architectural summary
            if (context.Nodes.Any())
            {
                prompt.AppendLine("## Related Components");
                var componentsByType = context.Nodes.GroupBy(n => n.NodeType);
                foreach (var group in componentsByType)
                {
                    prompt.AppendLine($"**{group.Key}s:**");
                    foreach (var node in group.Take(10)) // Limit to avoid prompt overflow
                    {
                        prompt.AppendLine($"- {node.FullName}");
                    }
                    prompt.AppendLine();
                }
            }

            // Add key relationships
            if (context.Relationships.Any())
            {
                prompt.AppendLine("## Key Relationships");
                var criticalRels = context.Relationships
                    .Where(r => r.Type == RelationshipType.Inherits || r.Type == RelationshipType.Implements || r.Type == RelationshipType.Uses)
                    .Take(15);

                foreach (var rel in criticalRels)
                {
                    var sourceNode = context.Nodes.FirstOrDefault(n => n.Id == rel.SourceNodeId);
                    var targetNode = context.Nodes.FirstOrDefault(n => n.Id == rel.TargetNodeId);
                    if (sourceNode != null && targetNode != null)
                    {
                        prompt.AppendLine($"- {sourceNode.Name} {GetRelationshipDescription(rel.Type)} {targetNode.Name}");
                    }
                }
                prompt.AppendLine();
            }

            // Analysis-specific instructions
            prompt.AppendLine(GetAnalysisInstructions(analysisType));
            prompt.AppendLine();

            // Code to analyze
            prompt.AppendLine("## Code to Analyze");
            prompt.AppendLine("```csharp");
            prompt.AppendLine(code);
            prompt.AppendLine("```");
            prompt.AppendLine();

            // Output format
            prompt.AppendLine("## Required Output Format");
            prompt.AppendLine("Provide your analysis in the following JSON format:");
            prompt.AppendLine("```json");
            prompt.AppendLine("{");
            prompt.AppendLine("  \"issues\": [");
            prompt.AppendLine("    {");
            prompt.AppendLine("      \"type\": \"issue_type\",");
            prompt.AppendLine("      \"severity\": \"low|medium|high|critical\",");
            prompt.AppendLine("      \"message\": \"brief_description\",");
            prompt.AppendLine("      \"description\": \"detailed_explanation\",");
            prompt.AppendLine("      \"lineNumber\": 0,");
            prompt.AppendLine("      \"affectedComponents\": [\"component1\", \"component2\"],");
            prompt.AppendLine("      \"impact\": \"impact_description\",");
            prompt.AppendLine("      \"recommendations\": [\"recommendation1\", \"recommendation2\"],");
            prompt.AppendLine("      \"confidenceScore\": 0.85");
            prompt.AppendLine("    }");
            prompt.AppendLine("  ],");
            prompt.AppendLine("  \"insights\": [");
            prompt.AppendLine("    {");
            prompt.AppendLine("      \"type\": \"architectural|integration|performance\",");
            prompt.AppendLine("      \"title\": \"insight_title\",");
            prompt.AppendLine("      \"description\": \"insight_description\",");
            prompt.AppendLine("      \"involvedElements\": [\"element1\", \"element2\"],");
            prompt.AppendLine("      \"recommendation\": \"recommendation\",");
            prompt.AppendLine("      \"importanceScore\": 0.75");
            prompt.AppendLine("    }");
            prompt.AppendLine("  ]");
            prompt.AppendLine("}");
            prompt.AppendLine("```");

            return prompt.ToString();
        }

        public async Task<string> BuildArchitecturalPromptAsync(GraphContext context, string focus, CancellationToken cancellationToken = default)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("You are a software architect analyzing a codebase for architectural patterns and design quality.");
            prompt.AppendLine($"Focus your analysis on: {focus}");
            prompt.AppendLine();

            prompt.AppendLine("## Codebase Overview");
            prompt.AppendLine($"**Repository:** {context.RepositoryId}");
            prompt.AppendLine($"**Components Analyzed:** {context.Nodes.Count}");
            prompt.AppendLine($"**Relationships:** {context.Relationships.Count}");
            prompt.AppendLine();

            // Component structure
            prompt.AppendLine("## Component Structure");
            var typeNodes = context.Nodes.Where(n => n.NodeType == NodeType.Type).ToList();
            var namespaces = typeNodes.Select(n => ExtractNamespace(n.FullName)).Distinct().ToList();

            prompt.AppendLine("**Namespaces:**");
            foreach (var ns in namespaces)
            {
                var typesInNs = typeNodes.Where(t => t.FullName.StartsWith(ns)).ToList();
                prompt.AppendLine($"- {ns} ({typesInNs.Count} types)");
            }
            prompt.AppendLine();

            // Dependency analysis
            prompt.AppendLine("## Dependency Patterns");
            var dependencyTypes = context.Relationships.GroupBy(r => r.Type);
            foreach (var group in dependencyTypes)
            {
                prompt.AppendLine($"**{group.Key}:** {group.Count()} relationships");
            }
            prompt.AppendLine();

            prompt.AppendLine("## Analysis Request");
            prompt.AppendLine("Analyze this architectural structure and provide:");
            prompt.AppendLine("1. **Architectural Pattern Identification** - What patterns are being used?");
            prompt.AppendLine("2. **Design Quality Assessment** - How well does the design follow SOLID principles?");
            prompt.AppendLine("3. **Coupling Analysis** - Are there tight coupling issues?");
            prompt.AppendLine("4. **Cohesion Analysis** - Are components properly focused?");
            prompt.AppendLine("5. **Recommendations** - What improvements would you suggest?");

            return prompt.ToString();
        }

        public async Task<string> BuildDependencyPromptAsync(string code, IEnumerable<CodeElementInfo> dependencies, CancellationToken cancellationToken = default)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("You are analyzing code dependencies for potential issues and improvements.");
            prompt.AppendLine();

            prompt.AppendLine("## Dependencies Context");
            var deps = dependencies.ToList();
            prompt.AppendLine($"**Total Dependencies:** {deps.Count}");
            prompt.AppendLine();

            prompt.AppendLine("**Direct Dependencies:**");
            foreach (var dep in deps.Take(20)) // Limit to avoid overflow
            {
                prompt.AppendLine($"- {dep.FullName} ({dep.Type})");
                if (!string.IsNullOrEmpty(dep.FilePath))
                {
                    prompt.AppendLine($"  File: {dep.FilePath}");
                }
            }
            prompt.AppendLine();

            prompt.AppendLine("## Code Under Analysis");
            prompt.AppendLine("```csharp");
            prompt.AppendLine(code);
            prompt.AppendLine("```");
            prompt.AppendLine();

            prompt.AppendLine("## Analysis Focus");
            prompt.AppendLine("Analyze the dependencies and identify:");
            prompt.AppendLine("1. **Circular Dependencies** - Any circular reference patterns");
            prompt.AppendLine("2. **Excessive Coupling** - Dependencies that create tight coupling");
            prompt.AppendLine("3. **Missing Abstractions** - Direct dependencies that should use interfaces");
            prompt.AppendLine("4. **Violation of Principles** - DIP, ISP, or other SOLID principle violations");
            prompt.AppendLine("5. **Refactoring Opportunities** - Ways to improve the dependency structure");

            return prompt.ToString();
        }

        public async Task<string> BuildIntegrationPromptAsync(string code, GraphContext context, CancellationToken cancellationToken = default)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("You are an expert at detecting integration-level issues in software systems.");
            prompt.AppendLine("Focus on problems that emerge from component interactions and system-wide concerns.");
            prompt.AppendLine();

            prompt.AppendLine("## Integration Context");
            prompt.AppendLine($"**System:** {context.RepositoryId}");
            prompt.AppendLine($"**Focus Component:** {context.FocusFilePath}");
            prompt.AppendLine($"**Connected Components:** {context.Nodes.Count}");
            prompt.AppendLine();

            // Integration patterns
            prompt.AppendLine("## Integration Patterns Detected");
            // Find interfaces by checking TypeKind property
            var interfaceNodes = context.Nodes.Where(n => 
                n.NodeType == NodeType.Type && 
                n.TypeKind == TypeKind.Interface).ToList();
            var implementsRels = context.Relationships.Where(r => r.Type == RelationshipType.Implements).ToList();
            var usesRels = context.Relationships.Where(r => r.Type == RelationshipType.Uses).ToList();

            prompt.AppendLine($"**Interfaces:** {interfaceNodes.Count}");
            prompt.AppendLine($"**Implementations:** {implementsRels.Count}");
            prompt.AppendLine($"**Usage Dependencies:** {usesRels.Count}");
            prompt.AppendLine();

            // Component interactions
            if (context.Relationships.Any())
            {
                prompt.AppendLine("## Key Component Interactions");
                var crossFileRelationships = GetCrossFileRelationships(context);
                foreach (var rel in crossFileRelationships.Take(10))
                {
                    var sourceNode = context.Nodes.FirstOrDefault(n => n.Id == rel.SourceNodeId);
                    var targetNode = context.Nodes.FirstOrDefault(n => n.Id == rel.TargetNodeId);
                    if (sourceNode != null && targetNode != null)
                    {
                        var sourceFile = sourceNode.Properties.GetValueOrDefault("filePath", "unknown")?.ToString() ?? "unknown";
                        var targetFile = targetNode.Properties.GetValueOrDefault("filePath", "unknown")?.ToString() ?? "unknown";
                        prompt.AppendLine($"- {sourceNode.Name} ({sourceFile}) → {targetNode.Name} ({targetFile})");
                    }
                }
                prompt.AppendLine();
            }

            prompt.AppendLine("## Code to Analyze");
            prompt.AppendLine("```csharp");
            prompt.AppendLine(code);
            prompt.AppendLine("```");
            prompt.AppendLine();

            prompt.AppendLine("## Integration Analysis Focus");
            prompt.AppendLine("Examine this code for integration-level issues:");
            prompt.AppendLine();
            prompt.AppendLine("1. **Cross-Cutting Concerns**");
            prompt.AppendLine("   - Missing error handling across component boundaries");
            prompt.AppendLine("   - Inconsistent logging or monitoring");
            prompt.AppendLine("   - Transaction boundary issues");
            prompt.AppendLine();
            prompt.AppendLine("2. **Data Flow Issues**");
            prompt.AppendLine("   - Data consistency problems");
            prompt.AppendLine("   - Serialization/deserialization issues");
            prompt.AppendLine("   - State management problems");
            prompt.AppendLine();
            prompt.AppendLine("3. **Contract Violations**");
            prompt.AppendLine("   - Interface contract misuse");
            prompt.AppendLine("   - API usage patterns that could break");
            prompt.AppendLine("   - Async/await pattern issues");
            prompt.AppendLine();
            prompt.AppendLine("4. **System-wide Patterns**");
            prompt.AppendLine("   - Configuration management issues");
            prompt.AppendLine("   - Dependency injection problems");
            prompt.AppendLine("   - Resource management (disposal, connections)");
            prompt.AppendLine();
            prompt.AppendLine("5. **Performance & Scalability**");
            prompt.AppendLine("   - N+1 query patterns");
            prompt.AppendLine("   - Inefficient component communication");
            prompt.AppendLine("   - Resource contention issues");

            return prompt.ToString();
        }

        private string GetAnalysisInstructions(string analysisType)
        {
            return analysisType.ToLower() switch
            {
                "security" => GetSecurityAnalysisInstructions(),
                "performance" => GetPerformanceAnalysisInstructions(),
                "maintainability" => GetMaintainabilityAnalysisInstructions(),
                "architecture" => GetArchitectureAnalysisInstructions(),
                "integration" => GetIntegrationAnalysisInstructions(),
                _ => GetGeneralAnalysisInstructions()
            };
        }

        private string GetSecurityAnalysisInstructions()
        {
            return @"## Security Analysis Instructions
Focus on identifying security vulnerabilities considering the architectural context:
- **Input Validation**: Check for missing or inadequate input validation
- **Authentication/Authorization**: Verify proper access controls
- **Data Protection**: Look for sensitive data exposure
- **Injection Attacks**: Identify SQL injection, XSS, or other injection vulnerabilities
- **Cryptography**: Check for weak encryption or insecure key management
- **Error Handling**: Ensure errors don't leak sensitive information
- **Dependencies**: Consider security implications of used components";
        }

        private string GetPerformanceAnalysisInstructions()
        {
            return @"## Performance Analysis Instructions
Analyze performance issues considering component relationships:
- **Algorithmic Complexity**: Identify inefficient algorithms
- **Database Access**: Look for N+1 queries, missing indexes
- **Caching**: Check for missing or inappropriate caching
- **Resource Usage**: Identify memory leaks, excessive allocations
- **Concurrency**: Look for threading issues, deadlocks
- **I/O Operations**: Check for blocking operations, inefficient I/O
- **Component Interactions**: Analyze communication overhead between components";
        }

        private string GetMaintainabilityAnalysisInstructions()
        {
            return @"## Maintainability Analysis Instructions
Evaluate code maintainability in the context of the system architecture:
- **Code Complexity**: Identify overly complex methods or classes
- **Duplication**: Look for code duplication across components
- **Naming**: Check for unclear or inconsistent naming
- **Documentation**: Identify missing or outdated documentation
- **Testing**: Look for untestable code or missing test coverage
- **SOLID Principles**: Check for violations of SOLID principles
- **Design Patterns**: Evaluate appropriate use of design patterns";
        }

        private string GetArchitectureAnalysisInstructions()
        {
            return @"## Architecture Analysis Instructions
Analyze architectural quality and design decisions:
- **Separation of Concerns**: Check for proper separation
- **Coupling**: Identify tight coupling between components
- **Cohesion**: Verify components have single, well-defined purposes
- **Abstraction Levels**: Check for appropriate abstraction layers
- **Design Patterns**: Evaluate pattern usage and appropriateness
- **Scalability**: Consider how design affects system scalability
- **Flexibility**: Assess how easy it would be to modify or extend";
        }

        private string GetIntegrationAnalysisInstructions()
        {
            return @"## Integration Analysis Instructions
Focus on integration-level issues and component interactions:
- **Interface Contracts**: Verify proper interface usage
- **Error Propagation**: Check how errors are handled across components
- **Data Consistency**: Look for data consistency issues
- **Transaction Boundaries**: Verify proper transaction management
- **Async Patterns**: Check for proper async/await usage
- **Configuration**: Look for configuration-related issues
- **Resource Management**: Verify proper resource cleanup and disposal";
        }

        private string GetGeneralAnalysisInstructions()
        {
            return @"## General Analysis Instructions
Perform a comprehensive code analysis considering the architectural context:
- **Code Quality**: Check for code smells and anti-patterns
- **Best Practices**: Verify adherence to language and framework best practices
- **Error Handling**: Look for missing or inadequate error handling
- **Resource Management**: Check for proper resource usage and cleanup
- **Thread Safety**: Identify potential concurrency issues
- **API Design**: Evaluate public interface design
- **Documentation**: Check for missing or unclear documentation";
        }

        private string GetRelationshipDescription(RelationshipType type)
        {
            return type switch
            {
                RelationshipType.Uses => "uses",
                RelationshipType.Calls => "calls",
                RelationshipType.Inherits => "inherits from",
                RelationshipType.Implements => "implements",
                RelationshipType.Contains => "contains",
                RelationshipType.References => "references",
                _ => "relates to"
            };
        }

        private string ExtractNamespace(string fullName)
        {
            var lastDot = fullName.LastIndexOf('.');
            return lastDot > 0 ? fullName.Substring(0, lastDot) : "Global";
        }

        private IEnumerable<CodeRelationship> GetCrossFileRelationships(GraphContext context)
        {
            return context.Relationships.Where(rel =>
            {
                var sourceNode = context.Nodes.FirstOrDefault(n => n.Id == rel.SourceNodeId);
                var targetNode = context.Nodes.FirstOrDefault(n => n.Id == rel.TargetNodeId);
                
                if (sourceNode == null || targetNode == null) return false;
                
                var sourceFile = sourceNode.Properties.GetValueOrDefault("filePath", "");
                var targetFile = targetNode.Properties.GetValueOrDefault("filePath", "");
                
                return !string.IsNullOrEmpty(sourceFile.ToString()) && 
                       !string.IsNullOrEmpty(targetFile.ToString()) && 
                       !sourceFile.Equals(targetFile);
            });
        }
    }
}