using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.API.Services;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.API.Tests.Services
{
    public class RoslynAnalysisServiceTests
    {
        private readonly Mock<IRoslynAnalyzer> _mockRoslynAnalyzer;
        private readonly Mock<ILogger<RoslynAnalysisService>> _mockLogger;
        private readonly RoslynAnalysisService _service;

        public RoslynAnalysisServiceTests()
        {
            _mockRoslynAnalyzer = new Mock<IRoslynAnalyzer>();
            _mockLogger = new Mock<ILogger<RoslynAnalysisService>>();
            _service = new RoslynAnalysisService(_mockRoslynAnalyzer.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Act & Assert
            _service.Should().NotBeNull();
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithValidFile_ShouldReturnAnalysisResult()
        {
            // Arrange
            var filePath = "/test/file.cs";
            var repositoryId = "repo-123";
            var projectPath = "/test/project.csproj";

            var expectedResult = new CodeAnalysisResult
            {
                FileId = "file-1",
                FilePath = filePath,
                RepositoryId = repositoryId,
                ProjectId = "project",
                Nodes = new List<Graph.Models.CodeNode>
                {
                    new Graph.Models.CodeNode
                    {
                        Id = "node-1",
                        Name = "TestClass",
                        NodeType = Graph.Models.NodeType.Type
                    }
                }
            };

            _mockRoslynAnalyzer.Setup(x => x.AnalyzeFileAsync(
                    filePath,
                    repositoryId,
                    projectPath,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.AnalyzeFileAsync(filePath, repositoryId, projectPath);

            // Assert
            result.Should().NotBeNull();
            result.FileId.Should().Be(expectedResult.FileId);
            result.FilePath.Should().Be(filePath);
            result.RepositoryId.Should().Be(repositoryId);
            result.Nodes.Should().HaveCount(1);

            _mockRoslynAnalyzer.Verify(x => x.AnalyzeFileAsync(
                filePath,
                repositoryId,
                projectPath,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WhenAnalyzerThrows_ShouldReturnErrorResult()
        {
            // Arrange
            var filePath = "/test/file.cs";
            var repositoryId = "repo-123";
            var errorMessage = "Analysis failed";

            _mockRoslynAnalyzer.Setup(x => x.AnalyzeFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(errorMessage));

            // Act
            var result = await _service.AnalyzeFileAsync(filePath, repositoryId);

            // Assert
            result.Should().NotBeNull();
            result.FilePath.Should().Be(filePath);
            result.RepositoryId.Should().Be(repositoryId);
            result.Errors.Should().NotBeEmpty();
            result.Errors.First().Should().Contain(errorMessage);
        }

        [Fact]
        public async Task AnalyzeProjectAsync_WithValidProject_ShouldReturnResults()
        {
            // Arrange
            var projectPath = "/test/project.csproj";
            var repositoryId = "repo-123";

            var expectedResults = new List<CodeAnalysisResult>
            {
                new CodeAnalysisResult { FileId = "file-1", FilePath = "/test/file1.cs" },
                new CodeAnalysisResult { FileId = "file-2", FilePath = "/test/file2.cs" }
            };

            _mockRoslynAnalyzer.Setup(x => x.AnalyzeProjectAsync(
                    projectPath,
                    repositoryId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults);

            // Act
            var results = await _service.AnalyzeProjectAsync(projectPath, repositoryId);

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(2);

            _mockRoslynAnalyzer.Verify(x => x.AnalyzeProjectAsync(
                projectPath,
                repositoryId,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeProjectAsync_WhenAnalyzerThrows_ShouldReturnErrorResult()
        {
            // Arrange
            var projectPath = "/test/project.csproj";
            var repositoryId = "repo-123";
            var errorMessage = "Project analysis failed";

            _mockRoslynAnalyzer.Setup(x => x.AnalyzeProjectAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(errorMessage));

            // Act
            var results = await _service.AnalyzeProjectAsync(projectPath, repositoryId);

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(1);
            
            var errorResult = results.First();
            errorResult.ProjectId.Should().Be("project");
            errorResult.RepositoryId.Should().Be(repositoryId);
            errorResult.Errors.Should().NotBeEmpty();
            errorResult.Errors.First().Should().Contain(errorMessage);
        }

        [Fact]
        public async Task AnalyzeSolutionAsync_WithValidSolution_ShouldReturnResults()
        {
            // Arrange
            var solutionPath = "/test/solution.sln";
            var repositoryId = "repo-123";

            var expectedResults = new List<CodeAnalysisResult>
            {
                new CodeAnalysisResult { FileId = "file-1", FilePath = "/test/project1/file1.cs" },
                new CodeAnalysisResult { FileId = "file-2", FilePath = "/test/project2/file2.cs" }
            };

            _mockRoslynAnalyzer.Setup(x => x.AnalyzeSolutionAsync(
                    solutionPath,
                    repositoryId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults);

            // Act
            var results = await _service.AnalyzeSolutionAsync(solutionPath, repositoryId);

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(2);

            _mockRoslynAnalyzer.Verify(x => x.AnalyzeSolutionAsync(
                solutionPath,
                repositoryId,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeSolutionAsync_WhenAnalyzerThrows_ShouldReturnErrorResult()
        {
            // Arrange
            var solutionPath = "/test/solution.sln";
            var repositoryId = "repo-123";
            var errorMessage = "Solution analysis failed";

            _mockRoslynAnalyzer.Setup(x => x.AnalyzeSolutionAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception(errorMessage));

            // Act
            var results = await _service.AnalyzeSolutionAsync(solutionPath, repositoryId);

            // Assert
            results.Should().NotBeNull();
            results.Should().HaveCount(1);
            
            var errorResult = results.First();
            errorResult.RepositoryId.Should().Be(repositoryId);
            errorResult.Errors.Should().NotBeEmpty();
            errorResult.Errors.First().Should().Contain(errorMessage);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithoutProjectPath_ShouldCallAnalyzerCorrectly()
        {
            // Arrange
            var filePath = "/test/file.cs";
            var repositoryId = "repo-123";

            var expectedResult = new CodeAnalysisResult
            {
                FileId = "file-1",
                FilePath = filePath,
                RepositoryId = repositoryId,
                ProjectId = null
            };

            _mockRoslynAnalyzer.Setup(x => x.AnalyzeFileAsync(
                    filePath,
                    repositoryId,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.AnalyzeFileAsync(filePath, repositoryId);

            // Assert
            result.Should().NotBeNull();
            result.ProjectId.Should().BeNull();

            _mockRoslynAnalyzer.Verify(x => x.AnalyzeFileAsync(
                filePath,
                repositoryId,
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithCancellation_ShouldPassCancellationToken()
        {
            // Arrange
            var filePath = "/test/file.cs";
            var repositoryId = "repo-123";
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var expectedResult = new CodeAnalysisResult
            {
                FileId = "file-1",
                FilePath = filePath,
                RepositoryId = repositoryId
            };

            _mockRoslynAnalyzer.Setup(x => x.AnalyzeFileAsync(
                    filePath,
                    repositoryId,
                    null,
                    cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.AnalyzeFileAsync(filePath, repositoryId, null, cancellationToken);

            // Assert
            result.Should().NotBeNull();

            _mockRoslynAnalyzer.Verify(x => x.AnalyzeFileAsync(
                filePath,
                repositoryId,
                null,
                cancellationToken), Times.Once);
        }
    }
}