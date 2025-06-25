using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codivus.API.Interfaces;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.API.Services
{
    /// <summary>
    /// Service for performing Roslyn-based code analysis integrated with graph storage
    /// </summary>
    public class RoslynAnalysisService : IRoslynAnalysisService
    {
        private readonly IRoslynAnalyzer _roslynAnalyzer;
        private readonly ILogger<RoslynAnalysisService> _logger;

        public RoslynAnalysisService(
            IRoslynAnalyzer roslynAnalyzer,
            ILogger<RoslynAnalysisService> logger)
        {
            _roslynAnalyzer = roslynAnalyzer;
            _logger = logger;
        }

        /// <summary>
        /// Analyzes a single file and returns the analysis result
        /// </summary>
        public async Task<CodeAnalysisResult> AnalyzeFileAsync(
            string filePath, 
            string repositoryId,
            string? projectPath = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting Roslyn analysis for file {FilePath}", filePath);
                
                var result = await _roslynAnalyzer.AnalyzeFileAsync(
                    filePath, 
                    repositoryId, 
                    projectPath, 
                    cancellationToken);

                _logger.LogDebug("Completed analysis for {FilePath}: {NodeCount} nodes, {RelationshipCount} relationships",
                    filePath, result.Nodes.Count, result.Relationships.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze file {FilePath}", filePath);
                
                // Return error result
                var errorResult = new CodeAnalysisResult
                {
                    FileId = Guid.NewGuid().ToString(),
                    FilePath = filePath,
                    RepositoryId = repositoryId,
                    ProjectId = projectPath != null ? Path.GetFileNameWithoutExtension(projectPath) : null
                };
                errorResult.Errors.Add($"Analysis failed: {ex.Message}");
                
                return errorResult;
            }
        }

        /// <summary>
        /// Analyzes all files in a project
        /// </summary>
        public async Task<IEnumerable<CodeAnalysisResult>> AnalyzeProjectAsync(
            string projectPath,
            string repositoryId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting Roslyn analysis for project {ProjectPath}", projectPath);
                
                var results = await _roslynAnalyzer.AnalyzeProjectAsync(
                    projectPath, 
                    repositoryId, 
                    cancellationToken);

                var resultList = results.ToList();
                _logger.LogInformation("Completed project analysis for {ProjectPath}: {FileCount} files analyzed",
                    projectPath, resultList.Count);

                return resultList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze project {ProjectPath}", projectPath);
                
                // Return error result
                var errorResult = new CodeAnalysisResult
                {
                    ProjectId = Path.GetFileNameWithoutExtension(projectPath),
                    RepositoryId = repositoryId
                };
                errorResult.Errors.Add($"Project analysis failed: {ex.Message}");
                
                return new[] { errorResult };
            }
        }

        /// <summary>
        /// Analyzes all projects in a solution
        /// </summary>
        public async Task<IEnumerable<CodeAnalysisResult>> AnalyzeSolutionAsync(
            string solutionPath,
            string repositoryId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting Roslyn analysis for solution {SolutionPath}", solutionPath);
                
                var results = await _roslynAnalyzer.AnalyzeSolutionAsync(
                    solutionPath, 
                    repositoryId, 
                    cancellationToken);

                var resultList = results.ToList();
                _logger.LogInformation("Completed solution analysis for {SolutionPath}: {FileCount} files analyzed",
                    solutionPath, resultList.Count);

                return resultList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze solution {SolutionPath}", solutionPath);
                
                // Return error result
                var errorResult = new CodeAnalysisResult
                {
                    RepositoryId = repositoryId
                };
                errorResult.Errors.Add($"Solution analysis failed: {ex.Message}");
                
                return new[] { errorResult };
            }
        }
    }
}