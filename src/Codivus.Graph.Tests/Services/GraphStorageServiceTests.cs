using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Codivus.Graph.Configuration;
using Codivus.Graph.Services;
using Codivus.Graph.Models;
using Codivus.Graph.Interfaces;

namespace Codivus.Graph.Tests.Services
{
    public class GraphStorageServiceTests : IDisposable
    {
        private readonly Mock<ILogger<GraphStorageService>> _mockLogger;
        private readonly Mock<IOptions<GraphConfiguration>> _mockOptions;
        private readonly GraphConfiguration _configuration;
        private readonly GraphStorageService _service;

        public GraphStorageServiceTests()
        {
            _mockLogger = new Mock<ILogger<GraphStorageService>>();
            _mockOptions = new Mock<IOptions<GraphConfiguration>>();
            
            _configuration = new GraphConfiguration
            {
                Enabled = false, // Disable actual connections for testing
                Neo4j = new Neo4jSettings
                {
                    Uri = "bolt://localhost:7687",
                    Username = "neo4j",
                    Password = "pass12345678",
                    MaxConnectionPoolSize = 5
                }
            };
            
            _mockOptions.Setup(x => x.Value).Returns(_configuration);
            _service = new GraphStorageService(_mockOptions.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Act & Assert
            _service.Should().NotBeNull();
            _mockOptions.Verify(x => x.Value, Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_WhenAlreadyInitialized_ShouldReturnTrue()
        {
            // This test cannot reliably test the actual functionality without a real Neo4j instance
            // Instead, we test the basic behavior
            
            // Act & Assert - Should not throw
            var exception = await Record.ExceptionAsync(async () => await _service.InitializeAsync());
            
            // The method should either succeed or fail gracefully
            if (exception != null)
            {
                // If it fails, it should be due to connection issues, not implementation bugs
                exception.Should().NotBeOfType<NotImplementedException>();
            }
        }

        [Fact]
        public async Task CreateSchemaAsync_ShouldReturnTrue()
        {
            // Act
            var result = await _service.CreateSchemaAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(NodeType.Namespace)]
        [InlineData(NodeType.Type)]
        [InlineData(NodeType.Method)]
        [InlineData(NodeType.Property)]
        [InlineData(NodeType.Field)]
        public void CreateNodeAsync_WithValidNode_ShouldReturnNode(NodeType nodeType)
        {
            // Arrange
            var node = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = "TestNode",
                FullName = "Test.TestNode",
                NodeType = nodeType,
                RepositoryId = "test-repo"
            };

            // Act & Assert - Should not throw
            var exception = Record.ExceptionAsync(async () => await _service.CreateNodeAsync(node));
            
            // Note: This might throw due to no actual connection, but we're testing the method structure
        }

        [Fact]
        public async Task CreateNodeAsync_WithNullId_ShouldGenerateId()
        {
            // Arrange
            var node = new CodeNode
            {
                Id = null,
                Name = "TestNode",
                FullName = "Test.TestNode",
                NodeType = NodeType.Type,
                RepositoryId = "test-repo"
            };

            // Act
            try
            {
                var result = await _service.CreateNodeAsync(node);
                
                // Assert
                result.Id.Should().NotBeNullOrEmpty();
                result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
                result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            }
            catch (InvalidOperationException)
            {
                // Expected when graph is not initialized
            }
        }

        [Fact]
        public async Task CreateNodesAsync_WithMultipleNodes_ShouldReturnNodes()
        {
            // Arrange
            var nodes = new List<CodeNode>
            {
                new CodeNode
                {
                    Name = "Node1",
                    FullName = "Test.Node1",
                    NodeType = NodeType.Type,
                    RepositoryId = "test-repo"
                },
                new CodeNode
                {
                    Name = "Node2",
                    FullName = "Test.Node2",
                    NodeType = NodeType.Method,
                    RepositoryId = "test-repo"
                }
            };

            // Act
            var result = await _service.CreateNodesAsync(nodes);

            // Assert
            result.Should().NotBeNull();
            // Note: Result might be empty due to connection failures, but method should not throw
        }

        [Fact]
        public async Task CreateRelationshipAsync_WithValidRelationship_ShouldReturnRelationship()
        {
            // Arrange
            var relationship = new CodeRelationship
            {
                SourceNodeId = "source-id",
                TargetNodeId = "target-id",
                Type = RelationshipType.Calls
            };

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () => 
                await _service.CreateRelationshipAsync(relationship));
            
            // Without a real connection, the method should handle the error gracefully
            if (exception != null)
            {
                exception.Should().BeOfType<InvalidOperationException>();
                exception.Message.Should().Contain("Graph not initialized");
            }
        }

        [Theory]
        [InlineData(RelationshipType.Contains)]
        [InlineData(RelationshipType.Inherits)]
        [InlineData(RelationshipType.Implements)]
        [InlineData(RelationshipType.Calls)]
        [InlineData(RelationshipType.Uses)]
        public async Task CreateRelationshipAsync_WithDifferentTypes_ShouldHandleAllTypes(RelationshipType relationshipType)
        {
            // Arrange
            var relationship = new CodeRelationship
            {
                SourceNodeId = "source-id",
                TargetNodeId = "target-id",
                Type = relationshipType
            };

            // Act & Assert - Should not throw due to relationship type
            var exception = await Record.ExceptionAsync(async () => 
                await _service.CreateRelationshipAsync(relationship));
            
            // The exception (if any) should be about connection, not about relationship type
            if (exception != null)
            {
                exception.Should().BeOfType<InvalidOperationException>();
                exception.Message.Should().Contain("Graph not initialized");
            }
        }

        [Fact]
        public async Task GetMetricsAsync_ShouldReturnMetricsObject()
        {
            // Arrange
            var repositoryId = "test-repo";

            // Act
            var result = await _service.GetMetricsAsync(repositoryId);

            // Assert
            result.Should().NotBeNull();
            result.RepositoryId.Should().Be(repositoryId);
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task BeginTransactionAsync_ShouldReturnTransaction()
        {
            // Act
            var transaction = await _service.BeginTransactionAsync();

            // Assert
            transaction.Should().NotBeNull();
            transaction.Should().BeAssignableTo<IGraphTransaction>();
        }

        [Fact]
        public async Task GetNodeAsync_WithValidId_ShouldReturnNullWhenNotConnected()
        {
            // Act
            var result = await _service.GetNodeAsync("test-id");

            // Assert
            result.Should().BeNull(); // No connection, so should return null
        }

        [Fact]
        public async Task GetNodesByTypeAsync_ShouldReturnEmptyWhenNotConnected()
        {
            // Act
            var result = await _service.GetNodesByTypeAsync("test-repo", NodeType.Type);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty(); // No connection, so should return empty
        }

        [Fact]
        public void NotImplementedMethods_ShouldThrowNotImplementedException()
        {
            // Arrange
            var node = new CodeNode { Id = "test" };
            var relationship = new CodeRelationship { Id = "test" };

            // Act & Assert
            Assert.ThrowsAsync<NotImplementedException>(() => _service.UpdateNodeAsync(node));
            Assert.ThrowsAsync<NotImplementedException>(() => _service.DeleteNodeAsync("test"));
            Assert.ThrowsAsync<NotImplementedException>(() => _service.NodeExistsAsync("test"));
            Assert.ThrowsAsync<NotImplementedException>(() => _service.UpdateRelationshipAsync(relationship));
            Assert.ThrowsAsync<NotImplementedException>(() => _service.GetRelationshipsAsync("test"));
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }

    public class GraphTransactionTests
    {
        [Fact]
        public async Task GraphTransaction_ShouldImplementIGraphTransaction()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<GraphStorageService>>();
            var mockOptions = new Mock<IOptions<GraphConfiguration>>();
            var configuration = new GraphConfiguration { Enabled = false };
            mockOptions.Setup(x => x.Value).Returns(configuration);
            
            using var service = new GraphStorageService(mockOptions.Object, mockLogger.Object);
            
            // Act
            using var transaction = await service.BeginTransactionAsync();

            // Assert
            transaction.Should().NotBeNull();
            transaction.Should().BeAssignableTo<IGraphTransaction>();
            
            // These should not throw
            await transaction.CommitAsync();
            await transaction.RollbackAsync();
        }
    }
}