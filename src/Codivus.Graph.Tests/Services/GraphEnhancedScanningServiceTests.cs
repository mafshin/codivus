using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Moq.Protected;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.Graph.Services;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Tests.Services
{
    public class GraphEnhancedScanningServiceTests
    {
        private readonly Mock<IGraphEmbeddingService> _mockEmbeddingService;
        private readonly Mock<IContextualPromptBuilder> _mockPromptBuilder;
        private readonly Mock<IGraphQueryService> _mockGraphQueryService;
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<GraphEnhancedScanningService>> _mockLogger;
        private readonly GraphEnhancedScanningService _service;

        public GraphEnhancedScanningServiceTests()
        {
            _mockEmbeddingService = new Mock<IGraphEmbeddingService>();
            _mockPromptBuilder = new Mock<IContextualPromptBuilder>();
            _mockGraphQueryService = new Mock<IGraphQueryService>();
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            _mockLogger = new Mock<ILogger<GraphEnhancedScanningService>>();
            
            _service = new GraphEnhancedScanningService(
                _mockEmbeddingService.Object,
                _mockPromptBuilder.Object,
                _mockGraphQueryService.Object,
                _httpClient,
                _mockLogger.Object);
        }

        [Fact]
        public async Task ScanFileWithContextAsync_ValidFile_ReturnsAnalysis()
        {
            // Arrange
            var repositoryId = "test-repo";
            var filePath = "/src/test.cs";
            var configuration = new Codivus.Graph.Models.GraphScanConfiguration
            {
                MaxDepth = 2,
                AnalysisTypes = new[] { "general", "architecture" }
            };

            var context = CreateTestContext();
            var architecture = CreateTestArchitecture();
            var prompt = "Test prompt for analysis";
            var llmResponse = CreateMockLLMResponse();

            SetupMocks(context, architecture, prompt, llmResponse);

            // Act
            var result = await _service.ScanFileWithContextAsync(repositoryId, filePath, configuration);

            // Assert
            result.Should().NotBeNull();
            result.RepositoryId.Should().Be(repositoryId);
            result.FilePath.Should().Be(filePath);
            result.Context.Should().Be(context);
            result.Architecture.Should().Be(architecture);
            result.Issues.Should().HaveCountGreaterOrEqualTo(1);
            result.Insights.Should().HaveCountGreaterOrEqualTo(1);
            result.Metrics.Should().NotBeNull();
            result.Metrics.NodesAnalyzed.Should().Be(context.Nodes.Count);
        }

        [Fact]
        public async Task ScanFileWithContextAsync_LLMError_ReturnsAnalysisWithoutLLMResults()
        {
            // Arrange
            var repositoryId = "test-repo";
            var filePath = "/src/test.cs";
            var context = CreateTestContext();
            var architecture = CreateTestArchitecture();
            var prompt = "Test prompt";

            _mockEmbeddingService
                .Setup(x => x.ExtractContextAsync(repositoryId, filePath, 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(context);

            _mockEmbeddingService
                .Setup(x => x.AnalyzeArchitectureAsync(context, It.IsAny<CancellationToken>()))
                .ReturnsAsync(architecture);

            _mockPromptBuilder
                .Setup(x => x.BuildAnalysisPromptAsync(It.IsAny<string>(), context, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(prompt);

            // Setup HTTP client to return error
            SetupHttpClientError();

            // Act
            var result = await _service.ScanFileWithContextAsync(repositoryId, filePath);

            // Assert
            result.Should().NotBeNull();
            result.Issues.Should().BeEmpty(); // No LLM results
            result.Insights.Should().BeEmpty();
        }

        [Fact]
        public async Task ScanFilesWithContextAsync_MultipleFiles_ReturnsMultipleAnalyses()
        {
            // Arrange
            var repositoryId = "test-repo";
            var filePaths = new[] { "/src/test1.cs", "/src/test2.cs" };
            var context = CreateTestContext();
            var architecture = CreateTestArchitecture();

            SetupMocksForMultipleFiles(context, architecture);

            // Act
            var results = await _service.ScanFilesWithContextAsync(repositoryId, filePaths);

            // Assert
            var analysesList = results.ToList();
            analysesList.Should().HaveCount(2);
            analysesList.All(a => a.RepositoryId == repositoryId).Should().BeTrue();
        }

        [Fact]
        public async Task AnalyzeArchitectureAsync_ValidComponent_ReturnsArchitecturalAnalysis()
        {
            // Arrange
            var repositoryId = "test-repo";
            var componentPath = "/src/component.cs";
            var options = new ArchitecturalAnalysisOptions
            {
                IncludePatternDetection = true,
                AnalyzeCoupling = true,
                CheckSOLIDPrinciples = true
            };

            var context = CreateTestContext();
            var prompt = "Architectural analysis prompt";
            var llmResponse = "Architectural analysis response";

            _mockEmbeddingService
                .Setup(x => x.ExtractContextAsync(repositoryId, componentPath, 3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(context);

            _mockPromptBuilder
                .Setup(x => x.BuildArchitecturalPromptAsync(context, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(prompt);

            SetupHttpClientSuccess(llmResponse);

            // Act
            var result = await _service.AnalyzeArchitectureAsync(repositoryId, componentPath, options);

            // Assert
            result.Should().NotBeNull();
            result.RepositoryId.Should().Be(repositoryId);
            result.ComponentPath.Should().Be(componentPath);
            result.AnalysisId.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task AnalyzeIntegrationAsync_MultipleComponents_ReturnsIntegrationAnalysis()
        {
            // Arrange
            var repositoryId = "test-repo";
            var componentPaths = new[] { "/src/service1.cs", "/src/service2.cs" };
            var context = CreateTestContext();

            _mockEmbeddingService
                .Setup(x => x.ExtractContextAsync(repositoryId, It.IsAny<string>(), 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(context);

            _mockPromptBuilder
                .Setup(x => x.BuildIntegrationPromptAsync(It.IsAny<string>(), It.IsAny<GraphContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Integration prompt");

            SetupHttpClientSuccess("Integration response");

            // Act
            var result = await _service.AnalyzeIntegrationAsync(repositoryId, componentPaths);

            // Assert
            result.Should().NotBeNull();
            result.RepositoryId.Should().Be(repositoryId);
            result.ComponentPaths.Should().BeEquivalentTo(componentPaths);
            result.AnalysisId.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task AnalyzeDependenciesAsync_ValidComponent_ReturnsDependencyAnalysis()
        {
            // Arrange
            var repositoryId = "test-repo";
            var componentPath = "/src/component.cs";
            var context = CreateTestContext();
            var relatedElements = new[]
            {
                new CodeElementInfo
                {
                    ElementId = "related1",
                    Name = "RelatedClass",
                    Type = NodeType.Type
                }
            };

            _mockEmbeddingService
                .Setup(x => x.ExtractContextAsync(repositoryId, componentPath, 3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(context);

            _mockEmbeddingService
                .Setup(x => x.FindRelatedElementsAsync(repositoryId, context.FocusElementId, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(relatedElements);

            _mockPromptBuilder
                .Setup(x => x.BuildDependencyPromptAsync(It.IsAny<string>(), relatedElements, It.IsAny<CancellationToken>()))
                .ReturnsAsync("Dependency prompt");

            SetupHttpClientSuccess("Dependency response");

            // Act
            var result = await _service.AnalyzeDependenciesAsync(repositoryId, componentPath);

            // Assert
            result.Should().NotBeNull();
            result.RepositoryId.Should().Be(repositoryId);
            result.ComponentPath.Should().Be(componentPath);
            result.AnalysisId.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GetMetricsAsync_ValidRepository_ReturnsMetrics()
        {
            // Arrange
            var repositoryId = "test-repo";

            // Act
            var result = await _service.GetMetricsAsync(repositoryId);

            // Assert
            result.Should().NotBeNull();
            result.RepositoryId.Should().Be(repositoryId);
            result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task ScanFileWithContextAsync_ExceptionDuringExtraction_ReturnsAnalysisWithError()
        {
            // Arrange
            var repositoryId = "test-repo";
            var filePath = "/src/error.cs";

            _mockEmbeddingService
                .Setup(x => x.ExtractContextAsync(repositoryId, filePath, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Extraction failed"));

            // Act
            var result = await _service.ScanFileWithContextAsync(repositoryId, filePath);

            // Assert
            result.Should().NotBeNull();
            result.Issues.Should().HaveCount(1);
            result.Issues.First().Type.Should().Be("analysis_error");
            result.Issues.First().Severity.Should().Be("high");
        }

        private void SetupMocks(GraphContext context, ArchitecturalSummary architecture, string prompt, string llmResponse)
        {
            _mockEmbeddingService
                .Setup(x => x.ExtractContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(context);

            _mockEmbeddingService
                .Setup(x => x.AnalyzeArchitectureAsync(context, It.IsAny<CancellationToken>()))
                .ReturnsAsync(architecture);

            _mockPromptBuilder
                .Setup(x => x.BuildAnalysisPromptAsync(It.IsAny<string>(), context, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(prompt);

            SetupHttpClientSuccess(llmResponse);
        }

        private void SetupMocksForMultipleFiles(GraphContext context, ArchitecturalSummary architecture)
        {
            _mockEmbeddingService
                .Setup(x => x.ExtractContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(context);

            _mockEmbeddingService
                .Setup(x => x.AnalyzeArchitectureAsync(It.IsAny<GraphContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(architecture);

            _mockPromptBuilder
                .Setup(x => x.BuildAnalysisPromptAsync(It.IsAny<string>(), It.IsAny<GraphContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Test prompt");

            SetupHttpClientSuccess(CreateMockLLMResponse());
        }

        private void SetupHttpClientSuccess(string response)
        {
            // Create a proper OpenAI-compatible response with the LLM content
            var openAiResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = response
                        }
                    }
                }
            };
            
            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(openAiResponse);
            var mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse);
        }

        private void SetupHttpClientError()
        {
            var mockResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(mockResponse);
        }

        private string CreateMockLLMResponse()
        {
            return @"{
                ""issues"": [{
                    ""type"": ""test_issue"",
                    ""severity"": ""medium"",
                    ""message"": ""Test issue message"",
                    ""description"": ""Test issue description"",
                    ""lineNumber"": 10,
                    ""affectedComponents"": [""TestClass""],
                    ""impact"": ""Test impact"",
                    ""recommendations"": [""Test recommendation""],
                    ""confidenceScore"": 0.8
                }],
                ""insights"": [{
                    ""type"": ""architectural"",
                    ""title"": ""Test insight"",
                    ""description"": ""Test insight description"",
                    ""involvedElements"": [""TestClass""],
                    ""recommendation"": ""Test insight recommendation"",
                    ""importanceScore"": 0.7
                }]
            }";
        }

        private GraphContext CreateTestContext()
        {
            return new GraphContext
            {
                RepositoryId = "test-repo",
                FocusFilePath = "/src/test.cs",
                FocusElementId = "node1",
                MaxDepth = 2,
                ExtractedAt = DateTime.UtcNow,
                Nodes = new List<CodeNode>
                {
                    new CodeNode
                    {
                        Id = "node1",
                        Name = "TestClass",
                        FullName = "Namespace.TestClass",
                        NodeType = NodeType.Type
                    }
                },
                Relationships = new List<CodeRelationship>
                {
                    new CodeRelationship
                    {
                        Id = "rel1",
                        SourceNodeId = "node1",
                        TargetNodeId = "node2",
                        Type = RelationshipType.Uses
                    }
                }
            };
        }

        private ArchitecturalSummary CreateTestArchitecture()
        {
            return new ArchitecturalSummary
            {
                Pattern = "MVC",
                Components = new List<string> { "TestClass" },
                Layers = new List<string> { "Namespace" },
                KeyDependencies = new List<DependencyInfo>(),
                PotentialIssues = new List<string>(),
                Recommendations = new List<string>(),
                Metrics = new Dictionary<string, object>()
            };
        }
    }
}