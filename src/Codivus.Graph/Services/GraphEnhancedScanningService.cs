using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Codivus.Core.Models;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// Service for performing graph-enhanced code scanning with LLM integration
    /// </summary>
    public class GraphEnhancedScanningService : IGraphEnhancedScanningService
    {
        private readonly IGraphEmbeddingService _embeddingService;
        private readonly IContextualPromptBuilder _promptBuilder;
        private readonly IGraphQueryService _graphQueryService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GraphEnhancedScanningService> _logger;
        
        private const string LLM_ENDPOINT = "http://host.docker.internal:1234/api/v0/models";
        private const string MODEL_NAME = "qwen3-0.6b-mlx";

        public GraphEnhancedScanningService(
            IGraphEmbeddingService embeddingService,
            IContextualPromptBuilder promptBuilder,
            IGraphQueryService graphQueryService,
            HttpClient httpClient,
            ILogger<GraphEnhancedScanningService> logger)
        {
            _embeddingService = embeddingService;
            _promptBuilder = promptBuilder;
            _graphQueryService = graphQueryService;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<GraphEnhancedAnalysis> ScanFileWithContextAsync(
            string repositoryId, 
            string filePath, 
            Models.GraphScanConfiguration? configuration = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var analysisId = Guid.NewGuid().ToString();
            
            _logger.LogInformation("Starting enhanced scan for {FilePath} in repository {RepositoryId}", filePath, repositoryId);

            var analysis = new GraphEnhancedAnalysis
            {
                AnalysisId = analysisId,
                RepositoryId = repositoryId,
                FilePath = filePath,
                AnalyzedAt = startTime
            };

            try
            {
                // Extract graph context
                var contextStart = DateTime.UtcNow;
                var maxDepth = configuration?.MaxDepth ?? 2;
                analysis.Context = await _embeddingService.ExtractContextAsync(repositoryId, filePath, maxDepth, cancellationToken);
                var contextTime = DateTime.UtcNow - contextStart;

                // Get file content for analysis
                var fileContent = await GetFileContentAsync(repositoryId, filePath, cancellationToken);
                if (string.IsNullOrEmpty(fileContent))
                {
                    _logger.LogWarning("No content found for file {FilePath}", filePath);
                    return analysis;
                }

                // Generate architectural summary
                var embeddingStart = DateTime.UtcNow;
                analysis.Architecture = await _embeddingService.AnalyzeArchitectureAsync(analysis.Context, cancellationToken);
                var embeddingTime = DateTime.UtcNow - embeddingStart;

                // Perform different types of analysis
                var llmStart = DateTime.UtcNow;
                var analysisTypes = configuration?.AnalysisTypes ?? new[] { "general", "architecture", "integration" };
                
                var allIssues = new List<ContextualIssue>();
                var allInsights = new List<IntegrationInsight>();

                foreach (var analysisType in analysisTypes)
                {
                    var prompt = await _promptBuilder.BuildAnalysisPromptAsync(fileContent, analysis.Context, analysisType, cancellationToken);
                    var llmResult = await CallLLMAsync(prompt, cancellationToken);
                    
                    if (llmResult != null)
                    {
                        var parsedResult = ParseLLMResponse(llmResult, analysisType, filePath);
                        allIssues.AddRange(parsedResult.Issues);
                        allInsights.AddRange(parsedResult.Insights);
                    }
                }

                var llmTime = DateTime.UtcNow - llmStart;

                // Set results
                analysis.Issues = allIssues;
                analysis.Insights = allInsights;

                // Calculate metrics
                var totalTime = DateTime.UtcNow - startTime;
                analysis.Metrics = new AnalysisMetrics
                {
                    ContextExtractionTime = contextTime,
                    EmbeddingGenerationTime = embeddingTime,
                    LLMAnalysisTime = llmTime,
                    TotalAnalysisTime = totalTime,
                    NodesAnalyzed = analysis.Context.Nodes.Count,
                    RelationshipsAnalyzed = analysis.Context.Relationships.Count,
                    IssuesFound = allIssues.Count,
                    InsightsGenerated = allInsights.Count,
                    AdditionalMetrics = new Dictionary<string, object>
                    {
                        ["analysisTypes"] = analysisTypes,
                        ["maxDepth"] = maxDepth,
                        ["fileSize"] = fileContent.Length
                    }
                };

                _logger.LogInformation("Enhanced scan completed for {FilePath}. Found {IssueCount} issues and {InsightCount} insights in {Duration}ms",
                    filePath, allIssues.Count, allInsights.Count, totalTime.TotalMilliseconds);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during enhanced scan of {FilePath}", filePath);
                analysis.Issues.Add(new ContextualIssue
                {
                    IssueId = Guid.NewGuid().ToString(),
                    Type = "analysis_error",
                    Severity = "high",
                    Message = "Analysis failed due to internal error",
                    Description = ex.Message,
                    FilePath = filePath,
                    ConfidenceScore = 1.0
                });
                return analysis;
            }
        }

        public async Task<IEnumerable<GraphEnhancedAnalysis>> ScanFilesWithContextAsync(
            string repositoryId, 
            IEnumerable<string> filePaths, 
            Models.GraphScanConfiguration? configuration = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting batch enhanced scan for {FileCount} files in repository {RepositoryId}", 
                filePaths.Count(), repositoryId);

            var tasks = filePaths.Select(async filePath =>
            {
                try
                {
                    return await ScanFileWithContextAsync(repositoryId, filePath, configuration, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning file {FilePath}", filePath);
                    return new GraphEnhancedAnalysis
                    {
                        AnalysisId = Guid.NewGuid().ToString(),
                        RepositoryId = repositoryId,
                        FilePath = filePath,
                        AnalyzedAt = DateTime.UtcNow,
                        Issues = new List<ContextualIssue>
                        {
                            new ContextualIssue
                            {
                                IssueId = Guid.NewGuid().ToString(),
                                Type = "scan_error",
                                Severity = "high",
                                Message = "File scan failed",
                                Description = ex.Message,
                                FilePath = filePath,
                                ConfidenceScore = 1.0
                            }
                        }
                    };
                }
            });

            var results = await Task.WhenAll(tasks);
            
            _logger.LogInformation("Batch enhanced scan completed. Processed {ProcessedCount} files", results.Length);
            return results;
        }

        public async Task<ArchitecturalAnalysis> AnalyzeArchitectureAsync(
            string repositoryId, 
            string componentPath, 
            ArchitecturalAnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Starting architectural analysis for {ComponentPath}", componentPath);

            var analysis = new ArchitecturalAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                RepositoryId = repositoryId,
                ComponentPath = componentPath,
                AnalyzedAt = DateTime.UtcNow
            };

            try
            {
                // Extract context for architectural analysis
                var maxDepth = options?.MaxDepth ?? 3;
                var context = await _embeddingService.ExtractContextAsync(repositoryId, componentPath, maxDepth, cancellationToken);

                // Build architectural prompt
                var focus = string.Join(", ", options?.FocusAreas ?? new List<string> { "patterns", "coupling", "cohesion" });
                var prompt = await _promptBuilder.BuildArchitecturalPromptAsync(context, focus, cancellationToken);

                // Get LLM analysis
                var llmResult = await CallLLMAsync(prompt, cancellationToken);
                if (llmResult != null)
                {
                    // Parse architectural insights from LLM response
                    var architecturalInsights = ParseArchitecturalResponse(llmResult, context);
                    
                    analysis.DetectedPatterns = architecturalInsights.Patterns;
                    analysis.Issues = architecturalInsights.Issues;
                    analysis.PrincipleViolations = architecturalInsights.Violations;
                    analysis.Coupling = architecturalInsights.Coupling;
                    analysis.Cohesion = architecturalInsights.Cohesion;
                    analysis.Recommendations = architecturalInsights.Recommendations;
                    analysis.Metrics = architecturalInsights.Metrics;
                }

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during architectural analysis of {ComponentPath}", componentPath);
                analysis.Issues.Add(new ArchitecturalIssue
                {
                    IssueType = "analysis_error",
                    Severity = "high",
                    Title = "Architectural analysis failed",
                    Description = ex.Message,
                    ConfidenceScore = 1.0
                });
                return analysis;
            }
        }

        public async Task<IntegrationAnalysis> AnalyzeIntegrationAsync(
            string repositoryId, 
            IEnumerable<string> componentPaths,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Starting integration analysis for {ComponentCount} components", componentPaths.Count());

            var analysis = new IntegrationAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                RepositoryId = repositoryId,
                ComponentPaths = componentPaths.ToList(),
                AnalyzedAt = DateTime.UtcNow
            };

            try
            {
                // Build integrated context across all components
                var allContexts = new List<GraphContext>();
                
                foreach (var componentPath in componentPaths)
                {
                    var context = await _embeddingService.ExtractContextAsync(repositoryId, componentPath, 2, cancellationToken);
                    allContexts.Add(context);
                }

                // Merge contexts for integration analysis
                var mergedContext = MergeGraphContexts(allContexts, repositoryId);

                // Get content for integration analysis
                var integrationCode = await BuildIntegrationCodeSample(componentPaths, cancellationToken);
                var prompt = await _promptBuilder.BuildIntegrationPromptAsync(integrationCode, mergedContext, cancellationToken);

                // Get LLM analysis
                var llmResult = await CallLLMAsync(prompt, cancellationToken);
                if (llmResult != null)
                {
                    var integrationInsights = ParseIntegrationResponse(llmResult, mergedContext);
                    
                    analysis.Issues = integrationInsights.Issues;
                    analysis.CrossCuttingConcerns = integrationInsights.CrossCuttingConcerns;
                    analysis.Contracts = integrationInsights.Contracts;
                    analysis.DataFlowIssues = integrationInsights.DataFlowIssues;
                    analysis.CommunicationPatterns = integrationInsights.CommunicationPatterns;
                    analysis.Metrics = integrationInsights.Metrics;
                }

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during integration analysis");
                analysis.Issues.Add(new IntegrationIssue
                {
                    IssueType = "analysis_error",
                    Severity = "high",
                    Title = "Integration analysis failed",
                    Description = ex.Message,
                    ConfidenceScore = 1.0
                });
                return analysis;
            }
        }

        public async Task<DependencyAnalysis> AnalyzeDependenciesAsync(
            string repositoryId, 
            string componentPath,
            int maxDepth = 3,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Starting dependency analysis for {ComponentPath}", componentPath);

            var analysis = new DependencyAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                RepositoryId = repositoryId,
                ComponentPath = componentPath,
                AnalyzedAt = DateTime.UtcNow
            };

            try
            {
                // Extract context with focus on dependencies
                var context = await _embeddingService.ExtractContextAsync(repositoryId, componentPath, maxDepth, cancellationToken);
                
                // Find related elements for dependency analysis
                var primaryElementId = context.FocusElementId;
                var relatedElements = await _embeddingService.FindRelatedElementsAsync(repositoryId, primaryElementId, 20, cancellationToken);

                // Get file content and build dependency prompt
                var fileContent = await GetFileContentAsync(repositoryId, componentPath, cancellationToken);
                var prompt = await _promptBuilder.BuildDependencyPromptAsync(fileContent, relatedElements, cancellationToken);

                // Get LLM analysis
                var llmResult = await CallLLMAsync(prompt, cancellationToken);
                if (llmResult != null)
                {
                    var dependencyInsights = ParseDependencyResponse(llmResult, context);
                    
                    analysis.Dependencies = dependencyInsights.Dependencies;
                    analysis.CircularDependencies = dependencyInsights.CircularDependencies;
                    analysis.Violations = dependencyInsights.Violations;
                    analysis.Graph = dependencyInsights.Graph;
                    analysis.Metrics = dependencyInsights.Metrics;
                }

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during dependency analysis of {ComponentPath}", componentPath);
                analysis.Violations.Add(new DependencyViolation
                {
                    ViolationType = "analysis_error",
                    Description = $"Dependency analysis failed: {ex.Message}",
                    DependentComponent = componentPath,
                    Recommendation = "Review component structure and retry analysis"
                });
                return analysis;
            }
        }

        public async Task<EnhancedScanningMetrics> GetMetricsAsync(string repositoryId, CancellationToken cancellationToken = default)
        {
            return new EnhancedScanningMetrics
            {
                RepositoryId = repositoryId,
                GeneratedAt = DateTime.UtcNow,
                // These would typically be retrieved from a metrics store
                TotalFilesScanned = 0,
                GraphContextsGenerated = 0,
                LLMAnalysisRequests = 0,
                IssuesFound = 0,
                ArchitecturalInsights = 0
            };
        }

        private async Task<string?> CallLLMAsync(string prompt, CancellationToken cancellationToken)
        {
            try
            {
                var requestPayload = new
                {
                    model = MODEL_NAME,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 2000,
                    temperature = 0.1
                };

                var json = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Calling LLM with prompt length: {PromptLength}", prompt.Length);

                var response = await _httpClient.PostAsync($"{LLM_ENDPOINT}/chat/completions", content, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("message", out var message) && 
                            message.TryGetProperty("content", out var messageContent))
                        {
                            return messageContent.GetString();
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("LLM API returned {StatusCode}: {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling LLM API");
            }

            return null;
        }

        private async Task<string> GetFileContentAsync(string repositoryId, string filePath, CancellationToken cancellationToken)
        {
            // This would typically read from the repository storage
            // For now, return a placeholder indicating file content would be loaded
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    return await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read file content for {FilePath}", filePath);
            }

            return $"// File content for {filePath} would be loaded here";
        }

        private (List<ContextualIssue> Issues, List<IntegrationInsight> Insights) ParseLLMResponse(
            string llmResponse, string analysisType, string filePath)
        {
            var issues = new List<ContextualIssue>();
            var insights = new List<IntegrationInsight>();

            try
            {
                // Try to extract JSON from the response
                var jsonStart = llmResponse.IndexOf('{');
                var jsonEnd = llmResponse.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var parsed = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                    if (parsed.TryGetProperty("issues", out var issuesArray))
                    {
                        foreach (var issue in issuesArray.EnumerateArray())
                        {
                            issues.Add(new ContextualIssue
                            {
                                IssueId = Guid.NewGuid().ToString(),
                                Type = issue.GetProperty("type").GetString() ?? "unknown",
                                Severity = issue.GetProperty("severity").GetString() ?? "medium",
                                Message = issue.GetProperty("message").GetString() ?? "",
                                Description = issue.GetProperty("description").GetString() ?? "",
                                FilePath = filePath,
                                LineNumber = issue.TryGetProperty("lineNumber", out var line) ? line.GetInt32() : 0,
                                AffectedComponents = issue.TryGetProperty("affectedComponents", out var components) 
                                    ? components.EnumerateArray().Select(c => c.GetString() ?? "").ToList()
                                    : new List<string>(),
                                Impact = issue.TryGetProperty("impact", out var impact) ? impact.GetString() ?? "" : "",
                                Recommendations = issue.TryGetProperty("recommendations", out var recs)
                                    ? recs.EnumerateArray().Select(r => r.GetString() ?? "").ToList()
                                    : new List<string>(),
                                ConfidenceScore = issue.TryGetProperty("confidenceScore", out var conf) ? conf.GetDouble() : 0.5
                            });
                        }
                    }

                    if (parsed.TryGetProperty("insights", out var insightsArray))
                    {
                        foreach (var insight in insightsArray.EnumerateArray())
                        {
                            insights.Add(new IntegrationInsight
                            {
                                InsightId = Guid.NewGuid().ToString(),
                                Type = insight.GetProperty("type").GetString() ?? "general",
                                Title = insight.GetProperty("title").GetString() ?? "",
                                Description = insight.GetProperty("description").GetString() ?? "",
                                InvolvedElements = insight.TryGetProperty("involvedElements", out var elements)
                                    ? elements.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                                    : new List<string>(),
                                Recommendation = insight.TryGetProperty("recommendation", out var rec) ? rec.GetString() ?? "" : "",
                                ImportanceScore = insight.TryGetProperty("importanceScore", out var score) ? score.GetDouble() : 0.5
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse LLM response as JSON, creating fallback issue");
                issues.Add(new ContextualIssue
                {
                    IssueId = Guid.NewGuid().ToString(),
                    Type = "parse_error",
                    Severity = "low",
                    Message = "Could not parse LLM analysis response",
                    Description = $"LLM provided analysis but response format was unexpected: {ex.Message}",
                    FilePath = filePath,
                    ConfidenceScore = 0.1
                });
            }

            return (issues, insights);
        }

        // Placeholder parsing methods - these would contain more sophisticated parsing logic
        private (List<ArchitecturalPattern> Patterns, List<ArchitecturalIssue> Issues, 
                List<DesignPrincipleViolation> Violations, CouplingAnalysis Coupling, 
                CohesionAnalysis Cohesion, List<string> Recommendations, ArchitecturalMetrics Metrics) 
            ParseArchitecturalResponse(string llmResponse, GraphContext context)
        {
            // This would contain sophisticated parsing of architectural analysis
            return (new(), new(), new(), new(), new(), new(), new());
        }

        private (List<IntegrationIssue> Issues, List<CrossCuttingConcern> CrossCuttingConcerns,
                List<InterfaceContract> Contracts, List<DataFlowIssue> DataFlowIssues,
                List<CommunicationPattern> CommunicationPatterns, IntegrationMetrics Metrics)
            ParseIntegrationResponse(string llmResponse, GraphContext context)
        {
            // This would contain sophisticated parsing of integration analysis
            return (new(), new(), new(), new(), new(), new());
        }

        private (List<DependencyInfo> Dependencies, List<CircularDependency> CircularDependencies,
                List<DependencyViolation> Violations, Models.DependencyGraph Graph, DependencyMetrics Metrics)
            ParseDependencyResponse(string llmResponse, GraphContext context)
        {
            // This would contain sophisticated parsing of dependency analysis
            return (new(), new(), new(), new(), new());
        }

        private GraphContext MergeGraphContexts(List<GraphContext> contexts, string repositoryId)
        {
            var merged = new GraphContext
            {
                RepositoryId = repositoryId,
                ExtractedAt = DateTime.UtcNow,
                FocusFilePath = "multiple_components",
                MaxDepth = contexts.Max(c => c.MaxDepth)
            };

            var allNodes = new Dictionary<string, CodeNode>();
            var allRelationships = new Dictionary<string, CodeRelationship>();

            foreach (var context in contexts)
            {
                foreach (var node in context.Nodes)
                {
                    allNodes[node.Id] = node;
                }

                foreach (var rel in context.Relationships)
                {
                    allRelationships[rel.Id] = rel;
                }
            }

            merged.Nodes = allNodes.Values.ToList();
            merged.Relationships = allRelationships.Values.ToList();

            return merged;
        }

        private async Task<string> BuildIntegrationCodeSample(IEnumerable<string> componentPaths, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// Integration code sample from multiple components");
            
            foreach (var path in componentPaths.Take(3)) // Limit to avoid huge prompts
            {
                sb.AppendLine($"// Component: {path}");
                var content = await GetFileContentAsync("", path, cancellationToken);
                sb.AppendLine(content.Substring(0, Math.Min(1000, content.Length))); // Truncate for prompt size
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}