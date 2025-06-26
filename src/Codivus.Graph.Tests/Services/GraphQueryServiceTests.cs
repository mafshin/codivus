using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using Codivus.Graph.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Codivus.Graph.Tests.Services
{
    public class GraphQueryServiceTests
    {
        private readonly Mock<IGraphStorageService> _mockGraphStorageService;
        private readonly Mock<ILogger<GraphQueryService>> _mockLogger;
        private readonly GraphQueryService _service;

        public GraphQueryServiceTests()
        {
            _mockGraphStorageService = new Mock<IGraphStorageService>();
            _mockLogger = new Mock<ILogger<GraphQueryService>>();
            _service = new GraphQueryService(_mockGraphStorageService.Object, _mockLogger.Object);
        }

        #region FindNodesByNameAsync Tests

        [Fact]
        public async Task FindNodesByNameAsync_WithNodeType_FiltersCorrectly()
        {
            // Arrange
            var repositoryId = "repo1";
            var allTypeNodes = new List<CodeNode>
            {
                new CodeNode { Id = "1", Name = "TestClass", NodeType = NodeType.Type },
                new CodeNode { Id = "2", Name = "AnotherClass", NodeType = NodeType.Type },
                new CodeNode { Id = "3", Name = "TestMethod", NodeType = NodeType.Method }
            };

            _mockGraphStorageService
                .Setup(x => x.GetNodesByTypeAsync(repositoryId, NodeType.Type, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allTypeNodes.Where(n => n.NodeType == NodeType.Type));

            // Act
            var result = await _service.FindNodesByNameAsync(repositoryId, "Test", NodeType.Type);

            // Assert
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("TestClass");
        }

        [Fact]
        public async Task FindNodesByNameAsync_WithWildcard_ReturnsAllNodes()
        {
            // Arrange
            var repositoryId = "repo1";
            var allNodes = new List<CodeNode>
            {
                new CodeNode { Id = "1", Name = "Class1", NodeType = NodeType.Type },
                new CodeNode { Id = "2", Name = "Class2", NodeType = NodeType.Type }
            };

            _mockGraphStorageService
                .Setup(x => x.GetNodesByTypeAsync(repositoryId, NodeType.Type, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allNodes);

            // Act
            var result = await _service.FindNodesByNameAsync(repositoryId, "*", NodeType.Type);

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task FindNodesByNameAsync_WithLimit_RespectsLimit()
        {
            // Arrange
            var repositoryId = "repo1";
            var nodes = Enumerable.Range(1, 10)
                .Select(i => new CodeNode { Id = i.ToString(), Name = $"Class{i}", NodeType = NodeType.Type })
                .ToList();

            _mockGraphStorageService
                .Setup(x => x.GetNodesByTypeAsync(repositoryId, NodeType.Type, It.IsAny<CancellationToken>()))
                .ReturnsAsync(nodes);

            // Act
            var result = await _service.FindNodesByNameAsync(repositoryId, "*", NodeType.Type, limit: 5);

            // Assert
            result.Should().HaveCount(5);
        }

        #endregion

        #region GetDependenciesAsync Tests

        [Fact]
        public async Task GetDependenciesAsync_ReturnsCorrectDependencies()
        {
            // Arrange
            var nodeId = "node1";
            var relationships = new List<CodeRelationship>
            {
                new CodeRelationship { SourceNodeId = nodeId, TargetNodeId = "dep1", Type = RelationshipType.Uses },
                new CodeRelationship { SourceNodeId = nodeId, TargetNodeId = "dep2", Type = RelationshipType.References },
                new CodeRelationship { SourceNodeId = nodeId, TargetNodeId = "dep3", Type = RelationshipType.Dependency },
                new CodeRelationship { SourceNodeId = "other", TargetNodeId = nodeId, Type = RelationshipType.Uses }, // Incoming
                new CodeRelationship { SourceNodeId = nodeId, TargetNodeId = "dep4", Type = RelationshipType.Calls } // Different type
            };

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync(nodeId, null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(relationships);

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken ct) => new CodeNode { Id = id, Name = $"Node{id}" });

            // Act
            var result = await _service.GetDependenciesAsync(nodeId);

            // Assert
            result.Should().HaveCount(3);
            result.Select(n => n.Id).Should().BeEquivalentTo(new[] { "dep1", "dep2", "dep3" });
        }

        #endregion

        #region GetDependentsAsync Tests

        [Fact]
        public async Task GetDependentsAsync_ReturnsCorrectDependents()
        {
            // Arrange
            var nodeId = "node1";
            var relationships = new List<CodeRelationship>
            {
                new CodeRelationship { SourceNodeId = "dep1", TargetNodeId = nodeId, Type = RelationshipType.Uses },
                new CodeRelationship { SourceNodeId = "dep2", TargetNodeId = nodeId, Type = RelationshipType.References },
                new CodeRelationship { SourceNodeId = nodeId, TargetNodeId = "other", Type = RelationshipType.Uses }, // Outgoing
                new CodeRelationship { SourceNodeId = "dep3", TargetNodeId = nodeId, Type = RelationshipType.Calls } // Different type
            };

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync(nodeId, null, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(relationships);

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken ct) => new CodeNode { Id = id, Name = $"Node{id}" });

            // Act
            var result = await _service.GetDependentsAsync(nodeId);

            // Assert
            result.Should().HaveCount(2);
            result.Select(n => n.Id).Should().BeEquivalentTo(new[] { "dep1", "dep2" });
        }

        #endregion

        #region GetCallHierarchyAsync Tests

        [Fact]
        public async Task GetCallHierarchyAsync_ForCallers_ReturnsCorrectHierarchy()
        {
            // Arrange
            var methodId = "method1";
            var rootMethod = new CodeNode { Id = methodId, Name = "TestMethod", NodeType = NodeType.Method };
            
            var relationships = new List<CodeRelationship>
            {
                new CodeRelationship { SourceNodeId = "caller1", TargetNodeId = methodId, Type = RelationshipType.Calls },
                new CodeRelationship { SourceNodeId = "caller2", TargetNodeId = methodId, Type = RelationshipType.Calls }
            };

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(methodId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rootMethod);

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync(methodId, RelationshipType.Calls, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(relationships);

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync("caller1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeNode { Id = "caller1", Name = "CallerMethod1" });

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync("caller2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeNode { Id = "caller2", Name = "CallerMethod2" });

            // Act
            var result = await _service.GetCallHierarchyAsync(methodId, CallHierarchyDirection.Callers);

            // Assert
            result.RootMethod.Should().BeEquivalentTo(rootMethod);
            result.Nodes.Should().HaveCount(2);
            result.TotalNodes.Should().Be(2);
        }

        #endregion

        #region GetTypeHierarchyAsync Tests

        [Fact]
        public async Task GetTypeHierarchyAsync_ReturnsCompleteHierarchy()
        {
            // Arrange
            var typeId = "type1";
            var rootType = new CodeNode { Id = typeId, Name = "TestClass", NodeType = NodeType.Type };
            
            var relationships = new List<CodeRelationship>
            {
                // Base type
                new CodeRelationship { SourceNodeId = typeId, TargetNodeId = "base1", Type = RelationshipType.Inherits },
                // Derived type
                new CodeRelationship { SourceNodeId = "derived1", TargetNodeId = typeId, Type = RelationshipType.Inherits },
                // Implemented interface
                new CodeRelationship { SourceNodeId = typeId, TargetNodeId = "interface1", Type = RelationshipType.Implements },
                // Implementing type (if this is an interface)
                new CodeRelationship { SourceNodeId = "impl1", TargetNodeId = typeId, Type = RelationshipType.Implements }
            };

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(typeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rootType);

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync(typeId, null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(relationships);

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(It.Is<string>(id => id != typeId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken ct) => new CodeNode { Id = id, Name = $"Type{id}", NodeType = NodeType.Type });

            // Act
            var result = await _service.GetTypeHierarchyAsync(typeId);

            // Assert
            result.RootType.Should().BeEquivalentTo(rootType);
            result.BaseTypes.Should().HaveCount(1);
            result.DerivedTypes.Should().HaveCount(1);
            result.ImplementedInterfaces.Should().HaveCount(1);
            result.ImplementingTypes.Should().HaveCount(1);
        }

        #endregion

        #region AnalyzeImpactAsync Tests

        [Fact]
        public async Task AnalyzeImpactAsync_CalculatesImpactCorrectly()
        {
            // Arrange
            var nodeId = "node1";
            var sourceNode = new CodeNode { Id = nodeId, Name = "SourceNode", NodeType = NodeType.Method };
            var dependentNodes = new List<CodeNode>
            {
                new CodeNode { Id = "dep1", Name = "Dependent1" },
                new CodeNode { Id = "dep2", Name = "Dependent2" }
            };

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(nodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sourceNode);

            // Mock GetDependentsAsync through relationships
            var relationships = new List<CodeRelationship>
            {
                new CodeRelationship { SourceNodeId = "dep1", TargetNodeId = nodeId, Type = RelationshipType.Uses },
                new CodeRelationship { SourceNodeId = "dep2", TargetNodeId = nodeId, Type = RelationshipType.References }
            };

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync(nodeId, null, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(relationships);

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync("dep1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dependentNodes[0]);

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync("dep2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dependentNodes[1]);

            // Act
            var result = await _service.AnalyzeImpactAsync(nodeId);

            // Assert
            result.SourceNode.Should().BeEquivalentTo(sourceNode);
            result.DirectlyImpacted.Should().HaveCount(2);
            result.TotalImpactedNodes.Should().Be(2);
            result.ImpactScore.Should().Be(1.0); // 2 * 0.5
        }

        #endregion

        #region AnalyzeCouplingAsync Tests

        [Fact]
        public async Task AnalyzeCouplingAsync_CalculatesCouplingMetrics()
        {
            // Arrange
            var projectId = "project1";
            var typeNodes = new List<CodeNode>
            {
                new CodeNode { Id = "type1", Name = "Class1", NodeType = NodeType.Type },
                new CodeNode { Id = "type2", Name = "Class2", NodeType = NodeType.Type }
            };

            _mockGraphStorageService
                .Setup(x => x.GetNodesByTypeAsync(projectId, NodeType.Type, It.IsAny<CancellationToken>()))
                .ReturnsAsync(typeNodes);

            // Setup relationships for type1
            var type1Relationships = new List<CodeRelationship>
            {
                new CodeRelationship { SourceNodeId = "type1", TargetNodeId = "ext1", Type = RelationshipType.Uses },
                new CodeRelationship { SourceNodeId = "ext2", TargetNodeId = "type1", Type = RelationshipType.Uses }
            };

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync("type1", null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(type1Relationships);

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync("type1", null, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(type1Relationships);

            // Setup empty relationships for type2
            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync("type2", null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CodeRelationship>());

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken ct) => new CodeNode { Id = id, Name = $"Node{id}" });

            // Act
            var result = await _service.AnalyzeCouplingAsync(projectId);

            // Assert
            result.ProjectId.Should().Be(projectId);
            result.TypeCoupling.Should().HaveCount(2);
            result.TypeCoupling["type1"].AfferentCoupling.Should().Be(1);
            result.TypeCoupling["type1"].EfferentCoupling.Should().Be(1);
            result.TypeCoupling["type1"].InstabilityIndex.Should().Be(0.5);
        }

        #endregion

        #region ExtractSubgraphAsync Tests

        [Fact]
        public async Task ExtractSubgraphAsync_ReturnsSubgraphWithinDepth()
        {
            // Arrange
            var nodeId = "node1";
            var rootNode = new CodeNode { Id = nodeId, Name = "RootNode", NodeType = NodeType.Type };
            var options = new SubgraphOptions { MaxDepth = 2, MaxNodes = 10 };

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(nodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rootNode);

            var relationships = new List<CodeRelationship>
            {
                new CodeRelationship { Id = "rel1", SourceNodeId = nodeId, TargetNodeId = "node2", Type = RelationshipType.Uses }
            };

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync(nodeId, null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(relationships);

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync("node2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeNode { Id = "node2", Name = "ConnectedNode", NodeType = NodeType.Type });

            _mockGraphStorageService
                .Setup(x => x.GetRelationshipsAsync("node2", null, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CodeRelationship>());

            // Act
            var result = await _service.ExtractSubgraphAsync(nodeId, options);

            // Assert
            result.Nodes.Should().HaveCount(2);
            result.Relationships.Should().HaveCount(1);
            result.Nodes.Should().Contain(n => n.Id == nodeId);
            result.Nodes.Should().Contain(n => n.Id == "node2");
        }

        #endregion

        #region ExecuteCustomQueryAsync Tests

        [Fact]
        public async Task ExecuteCustomQueryAsync_ReturnsEmptyResults()
        {
            // Arrange
            var query = "g.V().hasLabel('type')";
            var parameters = new Dictionary<string, object> { { "limit", 10 } };

            // Act
            var result = await _service.ExecuteCustomQueryAsync(query, parameters);

            // Assert
            result.Should().BeEmpty();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("not yet implemented")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion
    }
}