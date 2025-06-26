using Microsoft.AspNetCore.Mvc;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using System.ComponentModel.DataAnnotations;

namespace Codivus.API.Controllers
{
    /// <summary>
    /// API controller for graph-enhanced code scanning with LLM integration
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EnhancedScanningController : ControllerBase
    {
        private readonly IGraphEnhancedScanningService _enhancedScanningService;
        private readonly ILogger<EnhancedScanningController> _logger;

        public EnhancedScanningController(
            IGraphEnhancedScanningService enhancedScanningService,
            ILogger<EnhancedScanningController> logger)
        {
            _enhancedScanningService = enhancedScanningService;
            _logger = logger;
        }

        /// <summary>
        /// Performs enhanced scanning of a single file with graph context
        /// </summary>
        [HttpPost("scan-file")]
        public async Task<ActionResult<GraphEnhancedAnalysis>> ScanFileWithContext(
            [FromBody] ScanFileRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting enhanced file scan for {FilePath} in repository {RepositoryId}", 
                    request.FilePath, request.RepositoryId);

                var result = await _enhancedScanningService.ScanFileWithContextAsync(
                    request.RepositoryId,
                    request.FilePath,
                    request.Configuration,
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during enhanced file scan");
                return StatusCode(500, new { error = "Internal server error during enhanced file scan" });
            }
        }

        /// <summary>
        /// Performs enhanced scanning of multiple files with shared context
        /// </summary>
        [HttpPost("scan-files")]
        public async Task<ActionResult<IEnumerable<GraphEnhancedAnalysis>>> ScanFilesWithContext(
            [FromBody] ScanFilesRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting enhanced batch scan for {FileCount} files in repository {RepositoryId}", 
                    request.FilePaths.Count, request.RepositoryId);

                var result = await _enhancedScanningService.ScanFilesWithContextAsync(
                    request.RepositoryId,
                    request.FilePaths,
                    request.Configuration,
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during enhanced batch file scan");
                return StatusCode(500, new { error = "Internal server error during enhanced batch scan" });
            }
        }

        /// <summary>
        /// Performs architectural analysis of a component or namespace
        /// </summary>
        [HttpPost("analyze-architecture")]
        public async Task<ActionResult<ArchitecturalAnalysis>> AnalyzeArchitecture(
            [FromBody] ArchitecturalAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting architectural analysis for {ComponentPath} in repository {RepositoryId}", 
                    request.ComponentPath, request.RepositoryId);

                var result = await _enhancedScanningService.AnalyzeArchitectureAsync(
                    request.RepositoryId,
                    request.ComponentPath,
                    request.Options,
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during architectural analysis");
                return StatusCode(500, new { error = "Internal server error during architectural analysis" });
            }
        }

        /// <summary>
        /// Analyzes integration points and cross-component issues
        /// </summary>
        [HttpPost("analyze-integration")]
        public async Task<ActionResult<IntegrationAnalysis>> AnalyzeIntegration(
            [FromBody] IntegrationAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting integration analysis for {ComponentCount} components in repository {RepositoryId}", 
                    request.ComponentPaths.Count, request.RepositoryId);

                var result = await _enhancedScanningService.AnalyzeIntegrationAsync(
                    request.RepositoryId,
                    request.ComponentPaths,
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during integration analysis");
                return StatusCode(500, new { error = "Internal server error during integration analysis" });
            }
        }

        /// <summary>
        /// Performs dependency analysis with graph context
        /// </summary>
        [HttpPost("analyze-dependencies")]
        public async Task<ActionResult<DependencyAnalysis>> AnalyzeDependencies(
            [FromBody] DependencyAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting dependency analysis for {ComponentPath} in repository {RepositoryId}", 
                    request.ComponentPath, request.RepositoryId);

                var result = await _enhancedScanningService.AnalyzeDependenciesAsync(
                    request.RepositoryId,
                    request.ComponentPath,
                    request.MaxDepth,
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during dependency analysis");
                return StatusCode(500, new { error = "Internal server error during dependency analysis" });
            }
        }

        /// <summary>
        /// Gets enhanced scanning statistics and metrics
        /// </summary>
        [HttpGet("metrics/{repositoryId}")]
        public async Task<ActionResult<EnhancedScanningMetrics>> GetMetrics(
            string repositoryId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _enhancedScanningService.GetMetricsAsync(repositoryId, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving enhanced scanning metrics");
                return StatusCode(500, new { error = "Internal server error retrieving metrics" });
            }
        }
    }

    // Request DTOs
    public class ScanFileRequest
    {
        [Required]
        public string RepositoryId { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public Codivus.Graph.Models.GraphScanConfiguration? Configuration { get; set; }
    }

    public class ScanFilesRequest
    {
        [Required]
        public string RepositoryId { get; set; } = string.Empty;

        [Required]
        public List<string> FilePaths { get; set; } = new();

        public Codivus.Graph.Models.GraphScanConfiguration? Configuration { get; set; }
    }

    public class ArchitecturalAnalysisRequest
    {
        [Required]
        public string RepositoryId { get; set; } = string.Empty;

        [Required]
        public string ComponentPath { get; set; } = string.Empty;

        public ArchitecturalAnalysisOptions? Options { get; set; }
    }

    public class IntegrationAnalysisRequest
    {
        [Required]
        public string RepositoryId { get; set; } = string.Empty;

        [Required]
        public List<string> ComponentPaths { get; set; } = new();
    }

    public class DependencyAnalysisRequest
    {
        [Required]
        public string RepositoryId { get; set; } = string.Empty;

        [Required]
        public string ComponentPath { get; set; } = string.Empty;

        public int MaxDepth { get; set; } = 3;
    }
}