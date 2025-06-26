using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Configuration;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using Codivus.Graph.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Codivus.Graph.Tests.Integration
{
    /// <summary>
    /// Integration tests for JanusGraph connectivity and operations.
    /// These tests require a running JanusGraph instance at host.docker.internal:8182
    /// and are disabled in CI/CD environments.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Category", "JanusGraph")]
    public class JanusGraphIntegrationTests : IDisposable
    {
        private readonly IGraphStorageService _graphStorageService;
        private readonly IGraphQueryService _graphQueryService;
        private readonly IOptions<GraphConfiguration> _options;
        private readonly string _testRepositoryId;

        public JanusGraphIntegrationTests()
        {
            // Skip tests if not in development environment
            if (IsRunningInCI())
            {
                Skip.If(true, "Integration tests are disabled in CI/CD environments");
            }

            _testRepositoryId = $"test-repo-{Guid.NewGuid()}";

            var configuration = new GraphConfiguration
            {
                Enabled = true,
                JanusGraph = new JanusGraphSettings
                {
                    Host = "host.docker.internal",
                    Port = 8182,
                    ConnectionPoolSize = 5,
                    ConnectionTimeout = 30000,
                    EnableSsl = false,
                    GraphName = "codivus_test"
                }
            };

            _options = Options.Create(configuration);
            
            var logger = new Mock<ILogger<GraphStorageService>>().Object;
            var queryLogger = new Mock<ILogger<GraphQueryService>>().Object;
            
            _graphStorageService = new GraphStorageService(_options, logger);
            _graphQueryService = new GraphQueryService(_graphStorageService, queryLogger);
        }

        private static bool IsRunningInCI()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));
        }

        [Fact]
        public async Task GraphStorageService_InitializeAsync_ShouldConnectSuccessfully()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Act
            var result = await _graphStorageService.InitializeAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GraphStorageService_CreateSchema_ShouldCreateIndicesAndLabels()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Arrange
            await _graphStorageService.InitializeAsync();

            // Act
            var result = await _graphStorageService.CreateSchemaAsync();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GraphStorageService_NodeOperations_ShouldWorkEndToEnd()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Arrange
            await _graphStorageService.InitializeAsync();
            await _graphStorageService.CreateSchemaAsync();

            var testNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeType = NodeType.Type,
                Name = "TestClass",
                FullName = "TestNamespace.TestClass",
                DisplayName = "TestClass",
                RepositoryId = _testRepositoryId,
                ProjectId = "test-project",
                FileId = "test-file.cs",
                StartLine = 10,
                EndLine = 50,
                Checksum = "abc123",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["accessibility"] = "public",
                    ["isAbstract"] = false
                }
            };

            try
            {
                // Act - Create
                var createdNode = await _graphStorageService.CreateNodeAsync(testNode);
                createdNode.Should().NotBeNull();
                createdNode.Id.Should().Be(testNode.Id);

                // Act - Get
                var retrievedNode = await _graphStorageService.GetNodeAsync(testNode.Id);
                retrievedNode.Should().NotBeNull();
                retrievedNode.Name.Should().Be(testNode.Name);
                retrievedNode.NodeType.Should().Be(testNode.NodeType);

                // Act - Exists
                var exists = await _graphStorageService.NodeExistsAsync(testNode.Id);
                exists.Should().BeTrue();

                // Act - Update
                testNode.Name = "UpdatedTestClass";
                var updatedNode = await _graphStorageService.UpdateNodeAsync(testNode);
                updatedNode.Name.Should().Be("UpdatedTestClass");

                // Act - Get by type
                var nodesByType = await _graphStorageService.GetNodesByTypeAsync(_testRepositoryId, NodeType.Type);
                nodesByType.Should().Contain(n => n.Id == testNode.Id);

                // Act - Delete
                var deleted = await _graphStorageService.DeleteNodeAsync(testNode.Id);
                deleted.Should().BeTrue();

                // Verify deletion
                var deletedNode = await _graphStorageService.GetNodeAsync(testNode.Id);
                deletedNode.Should().BeNull();
            }
            catch (Exception)
            {
                // Cleanup in case of test failure
                try
                {
                    await _graphStorageService.DeleteNodeAsync(testNode.Id);
                }
                catch
                {
                    // Ignore cleanup errors
                }
                throw;
            }
        }

        [Fact]
        public async Task GraphStorageService_RelationshipOperations_ShouldWorkEndToEnd()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Arrange
            await _graphStorageService.InitializeAsync();
            await _graphStorageService.CreateSchemaAsync();

            var sourceNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeType = NodeType.Type,
                Name = "SourceClass",
                FullName = "Test.SourceClass",
                DisplayName = "SourceClass",
                RepositoryId = _testRepositoryId,
                ProjectId = "test-project",
                FileId = "source.cs",
                Checksum = "src123",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var targetNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeType = NodeType.Type,
                Name = "TargetClass",
                FullName = "Test.TargetClass",
                DisplayName = "TargetClass",
                RepositoryId = _testRepositoryId,
                ProjectId = "test-project",
                FileId = "target.cs",
                Checksum = "tgt123",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var relationship = new CodeRelationship
            {
                Id = Guid.NewGuid().ToString(),
                SourceNodeId = sourceNode.Id,
                TargetNodeId = targetNode.Id,
                Type = RelationshipType.Uses,
                Context = "test context",
                Properties = new Dictionary<string, object>
                {
                    ["strength"] = 0.8,
                    ["usageCount"] = 5
                }
            };

            try
            {
                // Create nodes first
                await _graphStorageService.CreateNodeAsync(sourceNode);
                await _graphStorageService.CreateNodeAsync(targetNode);

                // Act - Create relationship
                var createdRelationship = await _graphStorageService.CreateRelationshipAsync(relationship);
                createdRelationship.Should().NotBeNull();
                createdRelationship.Id.Should().Be(relationship.Id);

                // Act - Check if relationship exists
                var exists = await _graphStorageService.RelationshipExistsAsync(
                    sourceNode.Id, targetNode.Id, RelationshipType.Uses);
                exists.Should().BeTrue();

                // Act - Get relationships
                var relationships = await _graphStorageService.GetRelationshipsAsync(
                    sourceNode.Id, RelationshipType.Uses, outgoing: true);
                relationships.Should().Contain(r => r.Id == relationship.Id);

                // Act - Update relationship
                relationship.Context = "updated context";
                var updatedRelationship = await _graphStorageService.UpdateRelationshipAsync(relationship);
                updatedRelationship.Context.Should().Be("updated context");

                // Act - Delete relationship
                var deleted = await _graphStorageService.DeleteRelationshipAsync(relationship.Id);
                deleted.Should().BeTrue();

                // Verify deletion
                var deletedExists = await _graphStorageService.RelationshipExistsAsync(
                    sourceNode.Id, targetNode.Id, RelationshipType.Uses);
                deletedExists.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                try
                {
                    await _graphStorageService.DeleteRelationshipAsync(relationship.Id);
                    await _graphStorageService.DeleteNodeAsync(sourceNode.Id);
                    await _graphStorageService.DeleteNodeAsync(targetNode.Id);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task GraphQueryService_FindNodesByName_ShouldFindCreatedNodes()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Arrange
            await _graphStorageService.InitializeAsync();
            await _graphStorageService.CreateSchemaAsync();

            var testNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeType = NodeType.Method,
                Name = "TestMethod",
                FullName = "TestClass.TestMethod",
                DisplayName = "TestMethod",
                RepositoryId = _testRepositoryId,
                ProjectId = "test-project",
                FileId = "test.cs",
                Checksum = "method123",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                // Create test node
                await _graphStorageService.CreateNodeAsync(testNode);

                // Act
                var foundNodes = await _graphQueryService.FindNodesByNameAsync(
                    _testRepositoryId, "Test", NodeType.Method, limit: 10);

                // Assert
                foundNodes.Should().Contain(n => n.Id == testNode.Id);
            }
            finally
            {
                // Cleanup
                try
                {
                    await _graphStorageService.DeleteNodeAsync(testNode.Id);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task GraphQueryService_GetDependencies_ShouldReturnRelatedNodes()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Arrange
            await _graphStorageService.InitializeAsync();
            await _graphStorageService.CreateSchemaAsync();

            var sourceNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeType = NodeType.Type,
                Name = "SourceType",
                FullName = "Test.SourceType",
                DisplayName = "SourceType",
                RepositoryId = _testRepositoryId,
                ProjectId = "test-project",
                FileId = "source.cs",
                Checksum = "src456",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var dependencyNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                NodeType = NodeType.Type,
                Name = "DependencyType",
                FullName = "Test.DependencyType",
                DisplayName = "DependencyType",
                RepositoryId = _testRepositoryId,
                ProjectId = "test-project",
                FileId = "dependency.cs",
                Checksum = "dep456",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var relationship = new CodeRelationship
            {
                Id = Guid.NewGuid().ToString(),
                SourceNodeId = sourceNode.Id,
                TargetNodeId = dependencyNode.Id,
                Type = RelationshipType.Uses,
                Context = "dependency relationship"
            };

            try
            {
                // Create nodes and relationship
                await _graphStorageService.CreateNodeAsync(sourceNode);
                await _graphStorageService.CreateNodeAsync(dependencyNode);
                await _graphStorageService.CreateRelationshipAsync(relationship);

                // Act
                var dependencies = await _graphQueryService.GetDependenciesAsync(sourceNode.Id, maxDepth: 1);

                // Assert
                dependencies.Should().Contain(n => n.Id == dependencyNode.Id);
            }
            finally
            {
                // Cleanup
                try
                {
                    await _graphStorageService.DeleteRelationshipAsync(relationship.Id);
                    await _graphStorageService.DeleteNodeAsync(sourceNode.Id);
                    await _graphStorageService.DeleteNodeAsync(dependencyNode.Id);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task GraphStorageService_MaintenanceOperations_ShouldCompleteSuccessfully()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Arrange
            await _graphStorageService.InitializeAsync();

            // Act & Assert - These should not throw exceptions
            await _graphStorageService.OptimizeIndicesAsync();
            var orphanedCount = await _graphStorageService.CleanupOrphanedNodesAsync();
            await _graphStorageService.UpdateStatisticsAsync();

            // Assert
            orphanedCount.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task GraphStorageService_GetMetrics_ShouldReturnValidMetrics()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Arrange
            await _graphStorageService.InitializeAsync();

            // Act
            var metrics = await _graphStorageService.GetMetricsAsync(_testRepositoryId);

            // Assert
            metrics.Should().NotBeNull();
            metrics.RepositoryId.Should().Be(_testRepositoryId);
            metrics.VertexCount.Should().BeGreaterOrEqualTo(0);
            metrics.EdgeCount.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task GraphStorageService_BatchOperations_ShouldWorkCorrectly()
        {
            Skip.If(IsRunningInCI(), "Integration test disabled in CI");

            // Arrange
            await _graphStorageService.InitializeAsync();
            await _graphStorageService.CreateSchemaAsync();

            var nodes = new List<CodeNode>();
            for (int i = 0; i < 5; i++)
            {
                nodes.Add(new CodeNode
                {
                    Id = Guid.NewGuid().ToString(),
                    NodeType = NodeType.Method,
                    Name = $"BatchMethod{i}",
                    FullName = $"Test.BatchMethod{i}",
                    DisplayName = $"BatchMethod{i}",
                    RepositoryId = _testRepositoryId,
                    ProjectId = "batch-test",
                    FileId = "batch.cs",
                    Checksum = $"batch{i}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            try
            {
                // Act - Batch create
                var createdNodes = await _graphStorageService.CreateNodesAsync(nodes);
                createdNodes.Should().HaveCount(5);

                // Act - Batch update
                foreach (var node in nodes)
                {
                    node.Name = node.Name + "_Updated";
                }
                var updatedCount = await _graphStorageService.UpdateNodesAsync(nodes);
                updatedCount.Should().Be(5);

                // Act - Batch delete
                var nodeIds = nodes.Select(n => n.Id).ToList();
                var deletedCount = await _graphStorageService.DeleteNodesAsync(nodeIds);
                deletedCount.Should().Be(5);
            }
            finally
            {
                // Cleanup
                try
                {
                    var nodeIds = nodes.Select(n => n.Id).ToList();
                    await _graphStorageService.DeleteNodesAsync(nodeIds);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        public void Dispose()
        {
            try
            {
                // Cleanup test repository data
                if (!IsRunningInCI())
                {
                    _graphStorageService?.ClearGraphAsync(_testRepositoryId)?.Wait(5000);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            _graphStorageService?.Dispose();
        }
    }

    /// <summary>
    /// Helper class for conditional test skipping
    /// </summary>
    public static class Skip
    {
        public static void If(bool condition, string reason)
        {
            if (condition)
            {
                throw new SkipException(reason);
            }
        }
    }

    /// <summary>
    /// Exception to indicate a test should be skipped
    /// </summary>
    public class SkipException : Exception
    {
        public SkipException(string reason) : base(reason)
        {
        }
    }
}