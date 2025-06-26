using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.Graph.Services;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Tests.Services
{
    public class GraphEmbeddingServiceTests
    {
        private readonly Mock<IGraphQueryService> _mockGraphQueryService;
        private readonly Mock<IGraphStorageService> _mockGraphStorageService;
        private readonly Mock<ILogger<GraphEmbeddingService>> _mockLogger;
        private readonly GraphEmbeddingService _service;

        public GraphEmbeddingServiceTests()
        {
            _mockGraphQueryService = new Mock<IGraphQueryService>();
            _mockGraphStorageService = new Mock<IGraphStorageService>();
            _mockLogger = new Mock<ILogger<GraphEmbeddingService>>();
            _service = new GraphEmbeddingService(
                _mockGraphQueryService.Object,
                _mockGraphStorageService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task ExtractContextAsync_ValidFile_ReturnsPopulatedContext()
        {
            // Arrange
            var repositoryId = "test-repo";
            var filePath = "/src/test.cs";
            var nodeId = "node1";
            
            var testNode = new CodeNode
            {
                Id = nodeId,
                Name = "TestClass",
                FullName = "Namespace.TestClass",
                NodeType = NodeType.Type,
                Properties = new Dictionary<string, object> { ["filePath"] = filePath }
            };

            var subgraph = new Subgraph
            {
                Nodes = new List<CodeNode> { testNode },
                Relationships = new List<CodeRelationship>
                {
                    new CodeRelationship
                    {
                        Id = "rel1",
                        SourceNodeId = nodeId,
                        TargetNodeId = "node2",
                        Type = RelationshipType.Uses
                    }
                }
            };

            _mockGraphQueryService
                .Setup(x => x.FindNodesByNameAsync(repositoryId, filePath, null, 1000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { testNode });

            _mockGraphQueryService
                .Setup(x => x.ExtractSubgraphAsync(nodeId, It.IsAny<SubgraphOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(subgraph);

            // Act
            var result = await _service.ExtractContextAsync(repositoryId, filePath, 2);

            // Assert
            result.Should().NotBeNull();
            result.RepositoryId.Should().Be(repositoryId);
            result.FocusFilePath.Should().Be(filePath);
            result.FocusElementId.Should().Be(nodeId);
            result.Nodes.Should().HaveCount(1);
            result.Relationships.Should().HaveCount(1);
            result.Statistics.Should().NotBeNull();
        }

        [Fact]
        public async Task ExtractContextAsync_NoNodesFound_ReturnsEmptyContext()
        {
            // Arrange
            var repositoryId = "test-repo";
            var filePath = "/src/missing.cs";

            _mockGraphQueryService
                .Setup(x => x.FindNodesByNameAsync(repositoryId, filePath, null, 1000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<CodeNode>());

            // Act
            var result = await _service.ExtractContextAsync(repositoryId, filePath, 2);

            // Assert
            result.Should().NotBeNull();
            result.Nodes.Should().BeEmpty();
            result.Relationships.Should().BeEmpty();
        }

        [Fact]
        public async Task GenerateEmbeddingsAsync_ValidContext_ReturnsEmbedding()
        {
            // Arrange
            var context = new GraphContext
            {
                RepositoryId = "test-repo",
                FocusFilePath = "/src/test.cs",
                FocusElementId = "node1",
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
                        Type = RelationshipType.Inherits
                    }
                }
            };

            // Act
            var result = await _service.GenerateEmbeddingsAsync(context);

            // Assert
            result.Should().NotBeNull();
            result.ContextId.Should().NotBeNullOrEmpty();
            result.SerializedGraph.Should().NotBeNullOrEmpty();
            result.ArchitecturalSummary.Should().NotBeNullOrEmpty();
            result.Dependencies.Should().NotBeNull();
            result.KeyConcepts.Should().NotBeNull();
            result.EmbeddingMetadata.Should().ContainKey("nodeCount");
            result.EmbeddingMetadata["nodeCount"].Should().Be(1);
        }

        [Fact]
        public async Task SerializeContextForLLMAsync_ValidContext_ReturnsMarkdown()
        {
            // Arrange
            var context = new GraphContext
            {
                RepositoryId = "test-repo",
                FocusFilePath = "/src/test.cs",
                Nodes = new List<CodeNode>
                {
                    new CodeNode
                    {
                        Id = "node1",
                        Name = "TestMethod",
                        FullName = "Namespace.TestClass.TestMethod",
                        NodeType = NodeType.Method,
                        Signature = "public void TestMethod()",
                        Properties = new Dictionary<string, object> { ["filePath"] = "/src/test.cs" }
                    }
                },
                Relationships = new List<CodeRelationship>
                {
                    new CodeRelationship
                    {
                        Id = "rel1",
                        SourceNodeId = "node1",
                        TargetNodeId = "node2",
                        Type = RelationshipType.Calls
                    }
                }
            };

            // Act
            var result = await _service.SerializeContextForLLMAsync(context);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("# Code Graph Context");
            result.Should().Contain("Repository: test-repo");
            result.Should().Contain("TestMethod");
            result.Should().Contain("public void TestMethod()");
            result.Should().Contain("## Relationships");
        }

        [Fact]
        public async Task FindRelatedElementsAsync_ValidElement_ReturnsRelatedElements()
        {
            // Arrange
            var repositoryId = "test-repo";
            var elementId = "node1";
            
            var dependencies = new[]
            {
                new CodeNode
                {
                    Id = "dep1",
                    Name = "DependencyClass",
                    FullName = "Namespace.DependencyClass",
                    NodeType = NodeType.Type,
                    Properties = new Dictionary<string, object> { ["filePath"] = "/src/dep.cs" }
                }
            };

            var dependents = new[]
            {
                new CodeNode
                {
                    Id = "dependent1",
                    Name = "DependentClass",
                    FullName = "Namespace.DependentClass",
                    NodeType = NodeType.Type,
                    Signature = "public class DependentClass",
                    Properties = new Dictionary<string, object> { ["filePath"] = "/src/dependent.cs" }
                }
            };

            _mockGraphQueryService
                .Setup(x => x.GetDependenciesAsync(elementId, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dependencies);

            _mockGraphQueryService
                .Setup(x => x.GetDependentsAsync(elementId, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dependents);

            // Act
            var result = await _service.FindRelatedElementsAsync(repositoryId, elementId, 10);

            // Assert
            result.Should().NotBeNull();
            var elements = result.ToList();
            elements.Should().HaveCount(2);
            elements.Should().Contain(e => e.Name == "DependencyClass");
            elements.Should().Contain(e => e.Name == "DependentClass");
            elements.All(e => e.RelevanceScore > 0).Should().BeTrue();
        }

        [Fact]
        public async Task AnalyzeArchitectureAsync_ValidContext_ReturnsArchitecturalSummary()
        {
            // Arrange
            var context = new GraphContext
            {
                RepositoryId = "test-repo",
                Nodes = new List<CodeNode>
                {
                    new CodeNode
                    {
                        Id = "node1",
                        Name = "Controller",
                        FullName = "App.Controllers.Controller",
                        NodeType = NodeType.Type
                    },
                    new CodeNode
                    {
                        Id = "node2",
                        Name = "Service",
                        FullName = "App.Services.Service",
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

            // Act
            var result = await _service.AnalyzeArchitectureAsync(context);

            // Assert
            result.Should().NotBeNull();
            result.Components.Should().HaveCount(2);
            result.Layers.Should().Contain("App.Controllers");
            result.Layers.Should().Contain("App.Services");
            result.Pattern.Should().Be("MVC/Layered Architecture");
            result.KeyDependencies.Should().HaveCount(1);
            result.Metrics.Should().ContainKey("totalTypes");
        }

        [Fact]
        public async Task ExtractContextAsync_ExceptionThrown_ThrowsException()
        {
            // Arrange
            var repositoryId = "test-repo";
            var filePath = "/src/error.cs";

            _mockGraphQueryService
                .Setup(x => x.FindNodesByNameAsync(repositoryId, filePath, null, 1000, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.ExtractContextAsync(repositoryId, filePath, 2));
            
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}