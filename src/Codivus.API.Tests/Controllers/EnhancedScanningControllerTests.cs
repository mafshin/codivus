using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Codivus.API.Controllers;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.API.Tests.Controllers
{
    public class EnhancedScanningControllerTests
    {
        private readonly Mock<IGraphEnhancedScanningService> _mockScanningService;
        private readonly Mock<ILogger<EnhancedScanningController>> _mockLogger;
        private readonly EnhancedScanningController _controller;

        public EnhancedScanningControllerTests()
        {
            _mockScanningService = new Mock<IGraphEnhancedScanningService>();
            _mockLogger = new Mock<ILogger<EnhancedScanningController>>();
            _controller = new EnhancedScanningController(_mockScanningService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ScanFileWithContext_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ScanFileRequest
            {
                RepositoryId = "test-repo",
                FilePath = "/src/test.cs",
                Configuration = new Codivus.Graph.Models.GraphScanConfiguration
                {
                    MaxDepth = 2,
                    AnalysisTypes = new[] { "general", "architecture" }
                }
            };

            var expectedAnalysis = CreateTestAnalysis(request.RepositoryId, request.FilePath);

            _mockScanningService
                .Setup(x => x.ScanFileWithContextAsync(
                    request.RepositoryId,
                    request.FilePath,
                    request.Configuration,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedAnalysis);

            // Act
            var result = await _controller.ScanFileWithContext(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedAnalysis);
        }

        [Fact]
        public async Task ScanFileWithContext_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ScanFileRequest
            {
                RepositoryId = "test-repo",
                FilePath = "/src/test.cs"
            };

            _mockScanningService
                .Setup(x => x.ScanFileWithContextAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Codivus.Graph.Models.GraphScanConfiguration>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.ScanFileWithContext(request);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task ScanFilesWithContext_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ScanFilesRequest
            {
                RepositoryId = "test-repo",
                FilePaths = new List<string> { "/src/test1.cs", "/src/test2.cs" },
                Configuration = new Codivus.Graph.Models.GraphScanConfiguration { MaxDepth = 2 }
            };

            var expectedAnalyses = new[]
            {
                CreateTestAnalysis(request.RepositoryId, request.FilePaths[0]),
                CreateTestAnalysis(request.RepositoryId, request.FilePaths[1])
            };

            _mockScanningService
                .Setup(x => x.ScanFilesWithContextAsync(
                    request.RepositoryId,
                    request.FilePaths,
                    request.Configuration,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedAnalyses);

            // Act
            var result = await _controller.ScanFilesWithContext(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var returnedAnalyses = okResult!.Value as IEnumerable<GraphEnhancedAnalysis>;
            returnedAnalyses!.Should().HaveCount(2);
        }

        [Fact]
        public async Task AnalyzeArchitecture_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ArchitecturalAnalysisRequest
            {
                RepositoryId = "test-repo",
                ComponentPath = "/src/component.cs",
                Options = new ArchitecturalAnalysisOptions
                {
                    IncludePatternDetection = true,
                    AnalyzeCoupling = true
                }
            };

            var expectedAnalysis = new ArchitecturalAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                RepositoryId = request.RepositoryId,
                ComponentPath = request.ComponentPath,
                AnalyzedAt = DateTime.UtcNow,
                DetectedPatterns = new List<ArchitecturalPattern>(),
                Issues = new List<ArchitecturalIssue>(),
                Metrics = new ArchitecturalMetrics()
            };

            _mockScanningService
                .Setup(x => x.AnalyzeArchitectureAsync(
                    request.RepositoryId,
                    request.ComponentPath,
                    request.Options,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedAnalysis);

            // Act
            var result = await _controller.AnalyzeArchitecture(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedAnalysis);
        }

        [Fact]
        public async Task AnalyzeIntegration_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new IntegrationAnalysisRequest
            {
                RepositoryId = "test-repo",
                ComponentPaths = new List<string> { "/src/service1.cs", "/src/service2.cs" }
            };

            var expectedAnalysis = new IntegrationAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                RepositoryId = request.RepositoryId,
                ComponentPaths = request.ComponentPaths,
                AnalyzedAt = DateTime.UtcNow,
                Issues = new List<IntegrationIssue>(),
                Metrics = new IntegrationMetrics()
            };

            _mockScanningService
                .Setup(x => x.AnalyzeIntegrationAsync(
                    request.RepositoryId,
                    request.ComponentPaths,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedAnalysis);

            // Act
            var result = await _controller.AnalyzeIntegration(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedAnalysis);
        }

        [Fact]
        public async Task AnalyzeDependencies_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new DependencyAnalysisRequest
            {
                RepositoryId = "test-repo",
                ComponentPath = "/src/component.cs",
                MaxDepth = 3
            };

            var expectedAnalysis = new DependencyAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                RepositoryId = request.RepositoryId,
                ComponentPath = request.ComponentPath,
                AnalyzedAt = DateTime.UtcNow,
                Dependencies = new List<DependencyInfo>(),
                Metrics = new DependencyMetrics()
            };

            _mockScanningService
                .Setup(x => x.AnalyzeDependenciesAsync(
                    request.RepositoryId,
                    request.ComponentPath,
                    request.MaxDepth,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedAnalysis);

            // Act
            var result = await _controller.AnalyzeDependencies(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedAnalysis);
        }

        [Fact]
        public async Task GetMetrics_ValidRepositoryId_ReturnsOkResult()
        {
            // Arrange
            var repositoryId = "test-repo";
            var expectedMetrics = new EnhancedScanningMetrics
            {
                RepositoryId = repositoryId,
                GeneratedAt = DateTime.UtcNow,
                TotalFilesScanned = 10,
                IssuesFound = 5,
                ArchitecturalInsights = 3
            };

            _mockScanningService
                .Setup(x => x.GetMetricsAsync(repositoryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedMetrics);

            // Act
            var result = await _controller.GetMetrics(repositoryId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            okResult!.Value.Should().Be(expectedMetrics);
        }

        [Fact]
        public async Task GetMetrics_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var repositoryId = "test-repo";

            _mockScanningService
                .Setup(x => x.GetMetricsAsync(repositoryId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Metrics error"));

            // Act
            var result = await _controller.GetMetrics(repositoryId);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task AnalyzeArchitecture_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ArchitecturalAnalysisRequest
            {
                RepositoryId = "test-repo",
                ComponentPath = "/src/component.cs"
            };

            _mockScanningService
                .Setup(x => x.AnalyzeArchitectureAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ArchitecturalAnalysisOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Architecture analysis error"));

            // Act
            var result = await _controller.AnalyzeArchitecture(request);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task AnalyzeIntegration_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new IntegrationAnalysisRequest
            {
                RepositoryId = "test-repo",
                ComponentPaths = new List<string> { "/src/service.cs" }
            };

            _mockScanningService
                .Setup(x => x.AnalyzeIntegrationAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Integration analysis error"));

            // Act
            var result = await _controller.AnalyzeIntegration(request);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task AnalyzeDependencies_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new DependencyAnalysisRequest
            {
                RepositoryId = "test-repo",
                ComponentPath = "/src/component.cs"
            };

            _mockScanningService
                .Setup(x => x.AnalyzeDependenciesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Dependency analysis error"));

            // Act
            var result = await _controller.AnalyzeDependencies(request);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
        }

        private GraphEnhancedAnalysis CreateTestAnalysis(string repositoryId, string filePath)
        {
            return new GraphEnhancedAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                RepositoryId = repositoryId,
                FilePath = filePath,
                AnalyzedAt = DateTime.UtcNow,
                Context = new GraphContext
                {
                    RepositoryId = repositoryId,
                    FocusFilePath = filePath,
                    Nodes = new List<CodeNode>(),
                    Relationships = new List<CodeRelationship>()
                },
                Issues = new List<ContextualIssue>
                {
                    new ContextualIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Type = "test_issue",
                        Severity = "medium",
                        Message = "Test issue",
                        FilePath = filePath,
                        ConfidenceScore = 0.8
                    }
                },
                Insights = new List<IntegrationInsight>
                {
                    new IntegrationInsight
                    {
                        InsightId = Guid.NewGuid().ToString(),
                        Type = "architectural",
                        Title = "Test insight",
                        Description = "Test insight description",
                        ImportanceScore = 0.7
                    }
                },
                Metrics = new AnalysisMetrics
                {
                    NodesAnalyzed = 5,
                    RelationshipsAnalyzed = 3,
                    IssuesFound = 1,
                    InsightsGenerated = 1
                }
            };
        }
    }
}