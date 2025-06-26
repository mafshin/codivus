using Microsoft.AspNetCore.Mvc;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using Codivus.Core.Models;
using Codivus.API.Interfaces;
using Codivus.API.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Codivus.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GraphController : ControllerBase
    {
        private readonly IGraphQueryService _graphQueryService;
        private readonly IGraphStorageService _graphStorageService;
        private readonly IGraphScanOrchestrator _graphScanOrchestrator;
        private readonly ILogger<GraphController> _logger;

        public GraphController(
            IGraphQueryService graphQueryService,
            IGraphStorageService graphStorageService,
            IGraphScanOrchestrator graphScanOrchestrator,
            ILogger<GraphController> logger)
        {
            _graphQueryService = graphQueryService;
            _graphStorageService = graphStorageService;
            _graphScanOrchestrator = graphScanOrchestrator;
            _logger = logger;
        }

        // Phase 5: Graph Scanning Endpoints

        [HttpPost("scan/{repositoryId}")]
        public async Task<IActionResult> StartGraphScan(string repositoryId, [FromBody] GraphScanRequestDto request)
        {
            try
            {
                var configuration = new Core.Models.GraphScanConfiguration
                {
                    RepositoryId = repositoryId,
                    Mode = request.Mode,
                    Processing = request.Processing ?? new ProcessingConfiguration(),
                    Analysis = request.Analysis ?? new AnalysisConfiguration(),
                    Relationships = request.Relationships ?? new RelationshipConfiguration(),
                    Metrics = request.Metrics ?? new MetricsConfiguration()
                };

                var scanId = await _graphScanOrchestrator.StartGraphScanAsync(repositoryId, configuration);
                
                return Ok(new { scanId, message = "Graph scan started successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting graph scan for repository {RepositoryId}", repositoryId);
                return StatusCode(500, new { error = "Failed to start graph scan", message = ex.Message });
            }
        }

        [HttpGet("scan/{scanId}/status")]
        public async Task<IActionResult> GetGraphScanStatus(string scanId)
        {
            try
            {
                var progress = await _graphScanOrchestrator.GetScanProgressAsync(scanId);
                if (progress == null)
                    return NotFound(new { error = "Scan not found" });

                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scan status for {ScanId}", scanId);
                return StatusCode(500, new { error = "Failed to retrieve scan status", message = ex.Message });
            }
        }

        [HttpPost("scan/{scanId}/pause")]
        public async Task<IActionResult> PauseGraphScan(string scanId)
        {
            try
            {
                var success = await _graphScanOrchestrator.PauseScanAsync(scanId);
                if (!success)
                    return NotFound(new { error = "Scan not found or cannot be paused" });

                return Ok(new { message = "Scan paused successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing scan {ScanId}", scanId);
                return StatusCode(500, new { error = "Failed to pause scan", message = ex.Message });
            }
        }

        [HttpPost("scan/{scanId}/resume")]
        public async Task<IActionResult> ResumeGraphScan(string scanId)
        {
            try
            {
                var success = await _graphScanOrchestrator.ResumeScanAsync(scanId);
                if (!success)
                    return NotFound(new { error = "Scan not found or cannot be resumed" });

                return Ok(new { message = "Scan resumed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming scan {ScanId}", scanId);
                return StatusCode(500, new { error = "Failed to resume scan", message = ex.Message });
            }
        }

        [HttpPost("scan/{scanId}/cancel")]
        public async Task<IActionResult> CancelGraphScan(string scanId)
        {
            try
            {
                var success = await _graphScanOrchestrator.CancelScanAsync(scanId);
                if (!success)
                    return NotFound(new { error = "Scan not found or cannot be cancelled" });

                return Ok(new { message = "Scan cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling scan {ScanId}", scanId);
                return StatusCode(500, new { error = "Failed to cancel scan", message = ex.Message });
            }
        }


        [HttpGet("nodes")]
        public async Task<IActionResult> GetNodes(
            [FromQuery] string repositoryId,
            [FromQuery] string? nodeType = null,
            [FromQuery] string namePattern = "*",
            [FromQuery] int limit = 100)
        {
            try
            {
                NodeType? nodeTypeEnum = null;
                if (!string.IsNullOrEmpty(nodeType) && Enum.TryParse<NodeType>(nodeType, true, out var parsedNodeType))
                {
                    nodeTypeEnum = parsedNodeType;
                }

                var nodes = await _graphQueryService.FindNodesByNameAsync(repositoryId, namePattern, nodeTypeEnum, limit);
                return Ok(nodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving nodes");
                return StatusCode(500, new { error = "Failed to retrieve nodes", message = ex.Message });
            }
        }

        [HttpGet("nodes/{nodeId}")]
        public async Task<IActionResult> GetNode(string nodeId)
        {
            try
            {
                var node = await _graphStorageService.GetNodeAsync(nodeId);
                if (node == null)
                    return NotFound(new { error = "Node not found" });

                return Ok(node);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving node {NodeId}", nodeId);
                return StatusCode(500, new { error = "Failed to retrieve node", message = ex.Message });
            }
        }

        [HttpGet("nodes/{nodeId}/dependencies")]
        public async Task<IActionResult> GetNodeDependencies(string nodeId, [FromQuery] int maxDepth = 2)
        {
            try
            {
                var dependencies = await _graphQueryService.GetDependenciesAsync(nodeId, maxDepth);
                return Ok(dependencies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dependencies for node {NodeId}", nodeId);
                return StatusCode(500, new { error = "Failed to retrieve dependencies", message = ex.Message });
            }
        }

        [HttpGet("nodes/{nodeId}/dependents")]
        public async Task<IActionResult> GetNodeDependents(string nodeId, [FromQuery] int maxDepth = 2)
        {
            try
            {
                var dependents = await _graphQueryService.GetDependentsAsync(nodeId, maxDepth);
                return Ok(dependents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dependents for node {NodeId}", nodeId);
                return StatusCode(500, new { error = "Failed to retrieve dependents", message = ex.Message });
            }
        }

        [HttpGet("nodes/{nodeId}/call-hierarchy")]
        public async Task<IActionResult> GetCallHierarchy(string nodeId, [FromQuery] string direction = "both", [FromQuery] int maxDepth = 3)
        {
            try
            {
                CallHierarchyDirection hierarchyDirection = direction.ToLower() switch
                {
                    "callers" => CallHierarchyDirection.Callers,
                    "callees" => CallHierarchyDirection.Callees,
                    "both" => CallHierarchyDirection.Both,
                    _ => CallHierarchyDirection.Both
                };

                var hierarchy = await _graphQueryService.GetCallHierarchyAsync(nodeId, hierarchyDirection, maxDepth);
                return Ok(hierarchy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving call hierarchy for node {NodeId}", nodeId);
                return StatusCode(500, new { error = "Failed to retrieve call hierarchy", message = ex.Message });
            }
        }

        [HttpGet("nodes/{nodeId}/type-hierarchy")]
        public async Task<IActionResult> GetTypeHierarchy(string nodeId, [FromQuery] bool includeInterfaces = true)
        {
            try
            {
                var hierarchy = await _graphQueryService.GetTypeHierarchyAsync(nodeId, includeInterfaces);
                return Ok(hierarchy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving type hierarchy for node {NodeId}", nodeId);
                return StatusCode(500, new { error = "Failed to retrieve type hierarchy", message = ex.Message });
            }
        }

        [HttpPost("nodes/{nodeId}/impact-analysis")]
        public async Task<IActionResult> AnalyzeImpact(string nodeId, [FromBody] ImpactAnalysisRequestDto? request)
        {
            try
            {
                var options = new ImpactAnalysisOptions
                {
                    MaxDepth = request?.MaxDepth ?? 3,
                    IncludeTests = request?.IncludeTests ?? true,
                    IncludeIndirectDependencies = request?.IncludeIndirectDependencies ?? true
                };

                var result = await _graphQueryService.AnalyzeImpactAsync(nodeId, options);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing impact for node {NodeId}", nodeId);
                return StatusCode(500, new { error = "Failed to analyze impact", message = ex.Message });
            }
        }

        [HttpGet("coupling-analysis/{projectId}")]
        public async Task<IActionResult> AnalyzeCoupling(string projectId)
        {
            try
            {
                var result = await _graphQueryService.AnalyzeCouplingAsync(projectId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing coupling for project {ProjectId}", projectId);
                return StatusCode(500, new { error = "Failed to analyze coupling", message = ex.Message });
            }
        }

        [HttpPost("nodes/{nodeId}/subgraph")]
        public async Task<IActionResult> GetSubgraph(string nodeId, [FromBody] SubgraphRequestDto? request)
        {
            try
            {
                var options = new SubgraphOptions
                {
                    MaxDepth = request?.MaxDepth ?? 2,
                    MaxNodes = request?.MaxNodes ?? 100,
                    IncludeMetrics = request?.IncludeMetrics ?? false
                };

                if (request?.IncludeNodeTypes?.Any() == true)
                {
                    options.IncludedNodeTypes = new HashSet<NodeType>(
                        request.IncludeNodeTypes.Where(nt => Enum.TryParse<NodeType>(nt, true, out _))
                                                .Select(nt => Enum.Parse<NodeType>(nt, true)));
                }

                if (request?.IncludeRelationshipTypes?.Any() == true)
                {
                    options.IncludedRelationshipTypes = new HashSet<RelationshipType>(
                        request.IncludeRelationshipTypes.Where(rt => Enum.TryParse<RelationshipType>(rt, true, out _))
                                                        .Select(rt => Enum.Parse<RelationshipType>(rt, true)));
                }

                var subgraph = await _graphQueryService.ExtractSubgraphAsync(nodeId, options);
                return Ok(subgraph);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subgraph for node {NodeId}", nodeId);
                return StatusCode(500, new { error = "Failed to retrieve subgraph", message = ex.Message });
            }
        }

        [HttpGet("metrics/{repositoryId}")]
        public async Task<IActionResult> GetGraphMetrics(string repositoryId)
        {
            try
            {
                var metrics = await _graphStorageService.GetMetricsAsync(repositoryId);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metrics for repository {RepositoryId}", repositoryId);
                return StatusCode(500, new { error = "Failed to retrieve metrics", message = ex.Message });
            }
        }

        [HttpPost("query")]
        public async Task<IActionResult> ExecuteCustomQuery([FromBody] GraphQueryRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Query))
                    return BadRequest(new { error = "Query is required" });

                var results = await _graphQueryService.ExecuteCustomQueryAsync(
                    request.Query,
                    request.Parameters ?? new Dictionary<string, object>());

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing custom query");
                return StatusCode(500, new { error = "Failed to execute query", message = ex.Message });
            }
        }

        [HttpGet("visualization/{repositoryId}")]
        public async Task<IActionResult> GetVisualizationData(
            string repositoryId,
            [FromQuery] string visualizationType = "hierarchy",
            [FromQuery] string? rootNodeId = null)
        {
            try
            {
                object visualizationData = visualizationType.ToLower() switch
                {
                    "hierarchy" => await GetHierarchyVisualization(repositoryId, rootNodeId),
                    "dependencies" => await GetDependencyVisualization(repositoryId, rootNodeId),
                    "calls" => await GetCallGraphVisualization(repositoryId, rootNodeId),
                    _ => throw new ArgumentException($"Unknown visualization type: {visualizationType}")
                };

                return Ok(visualizationData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating visualization for repository {RepositoryId}", repositoryId);
                return StatusCode(500, new { error = "Failed to generate visualization", message = ex.Message });
            }
        }

        private async Task<object> GetHierarchyVisualization(string repositoryId, string? rootNodeId)
        {
            var nodes = await _graphQueryService.FindNodesByNameAsync(repositoryId, "*", NodeType.Namespace);
            var relationships = new List<object>();

            foreach (var node in nodes)
            {
                var childNodes = await _graphQueryService.GetDependentsAsync(node.Id, 1);
                relationships.AddRange(childNodes.Select(child => new
                {
                    source = node.Id,
                    target = child.Id,
                    type = "contains"
                }));
            }

            return new { nodes, relationships };
        }

        private async Task<object> GetDependencyVisualization(string repositoryId, string? rootNodeId)
        {
            if (string.IsNullOrEmpty(rootNodeId))
            {
                var allNodes = await _graphQueryService.FindNodesByNameAsync(repositoryId, "*", null);
                rootNodeId = allNodes.FirstOrDefault()?.Id;
            }

            if (string.IsNullOrEmpty(rootNodeId))
                return new { nodes = new List<object>(), relationships = new List<object>() };

            var options = new SubgraphOptions
            {
                MaxDepth = 3,
                IncludedRelationshipTypes = new HashSet<RelationshipType> { RelationshipType.Uses, RelationshipType.References }
            };

            var subgraph = await _graphQueryService.ExtractSubgraphAsync(rootNodeId, options);
            return new
            {
                nodes = subgraph.Nodes,
                relationships = subgraph.Relationships.Select(r => new
                {
                    source = r.SourceNodeId,
                    target = r.TargetNodeId,
                    type = r.Type.ToString()
                })
            };
        }

        private async Task<object> GetCallGraphVisualization(string repositoryId, string? rootNodeId)
        {
            if (string.IsNullOrEmpty(rootNodeId))
            {
                var methods = await _graphQueryService.FindNodesByNameAsync(repositoryId, "*", NodeType.Method);
                rootNodeId = methods.FirstOrDefault()?.Id;
            }

            if (string.IsNullOrEmpty(rootNodeId))
                return new { nodes = new List<object>(), relationships = new List<object>() };

            var options = new SubgraphOptions
            {
                MaxDepth = 3,
                IncludedRelationshipTypes = new HashSet<RelationshipType> { RelationshipType.Calls }
            };

            var subgraph = await _graphQueryService.ExtractSubgraphAsync(rootNodeId, options);
            return new
            {
                nodes = subgraph.Nodes,
                relationships = subgraph.Relationships.Select(r => new
                {
                    source = r.SourceNodeId,
                    target = r.TargetNodeId,
                    type = r.Type.ToString()
                })
            };
        }
    }

    // Phase 5: DTOs for Graph Scanning
    public class GraphScanRequestDto
    {
        public ScanMode Mode { get; set; } = ScanMode.Incremental;
        public ProcessingConfiguration? Processing { get; set; }
        public AnalysisConfiguration? Analysis { get; set; }
        public RelationshipConfiguration? Relationships { get; set; }
        public MetricsConfiguration? Metrics { get; set; }
    }

    public class GraphQueryRequestDto
    {
        public string Query { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
        public int? Limit { get; set; }
        public string? Format { get; set; } = "json";
    }

    // DTOs for request bodies
    public class ImpactAnalysisRequestDto
    {
        public int MaxDepth { get; set; } = 3;
        public bool IncludeTests { get; set; } = true;
        public bool IncludeIndirectDependencies { get; set; } = true;
    }

    public class SubgraphRequestDto
    {
        public int MaxDepth { get; set; } = 2;
        public int MaxNodes { get; set; } = 100;
        public bool IncludeMetrics { get; set; } = false;
        public List<string>? IncludeNodeTypes { get; set; }
        public List<string>? IncludeRelationshipTypes { get; set; }
    }

}