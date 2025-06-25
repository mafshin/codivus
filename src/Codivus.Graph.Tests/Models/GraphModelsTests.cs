using Xunit;
using FluentAssertions;
using Codivus.Graph.Models;

namespace Codivus.Graph.Tests.Models
{
    public class CodeNodeTests
    {
        [Fact]
        public void CodeNode_DefaultConstructor_ShouldInitializeCollections()
        {
            // Act
            var node = new CodeNode();

            // Assert
            node.Properties.Should().NotBeNull();
            node.Properties.Should().BeEmpty();
            node.CreatedAt.Should().Be(default);
            node.UpdatedAt.Should().Be(default);
        }

        [Fact]
        public void CodeNode_WithProperties_ShouldSetCorrectly()
        {
            // Arrange
            var id = Guid.NewGuid().ToString();
            var name = "TestClass";
            var fullName = "Namespace.TestClass";
            var repositoryId = "repo-123";

            // Act
            var node = new CodeNode
            {
                Id = id,
                Name = name,
                FullName = fullName,
                NodeType = NodeType.Type,
                RepositoryId = repositoryId,
                TypeKind = TypeKind.Class,
                Accessibility = AccessibilityLevel.Public,
                IsAbstract = false,
                IsSealed = true,
                LineCount = 100
            };

            // Assert
            node.Id.Should().Be(id);
            node.Name.Should().Be(name);
            node.FullName.Should().Be(fullName);
            node.NodeType.Should().Be(NodeType.Type);
            node.RepositoryId.Should().Be(repositoryId);
            node.TypeKind.Should().Be(TypeKind.Class);
            node.Accessibility.Should().Be(AccessibilityLevel.Public);
            node.IsAbstract.Should().BeFalse();
            node.IsSealed.Should().BeTrue();
            node.LineCount.Should().Be(100);
        }

        [Theory]
        [InlineData(NodeType.Namespace)]
        [InlineData(NodeType.Type)]
        [InlineData(NodeType.Method)]
        [InlineData(NodeType.Property)]
        [InlineData(NodeType.Field)]
        [InlineData(NodeType.Parameter)]
        [InlineData(NodeType.File)]
        [InlineData(NodeType.Project)]
        [InlineData(NodeType.Assembly)]
        public void CodeNode_WithDifferentNodeTypes_ShouldSetCorrectly(NodeType nodeType)
        {
            // Act
            var node = new CodeNode
            {
                NodeType = nodeType
            };

            // Assert
            node.NodeType.Should().Be(nodeType);
        }

        [Theory]
        [InlineData(AccessibilityLevel.Private)]
        [InlineData(AccessibilityLevel.Protected)]
        [InlineData(AccessibilityLevel.Internal)]
        [InlineData(AccessibilityLevel.Public)]
        [InlineData(AccessibilityLevel.ProtectedInternal)]
        [InlineData(AccessibilityLevel.PrivateProtected)]
        public void CodeNode_WithDifferentAccessibilityLevels_ShouldSetCorrectly(AccessibilityLevel accessibility)
        {
            // Act
            var node = new CodeNode
            {
                Accessibility = accessibility
            };

            // Assert
            node.Accessibility.Should().Be(accessibility);
        }

        [Theory]
        [InlineData(TypeKind.Class)]
        [InlineData(TypeKind.Interface)]
        [InlineData(TypeKind.Struct)]
        [InlineData(TypeKind.Enum)]
        [InlineData(TypeKind.Delegate)]
        public void CodeNode_WithDifferentTypeKinds_ShouldSetCorrectly(TypeKind typeKind)
        {
            // Act
            var node = new CodeNode
            {
                TypeKind = typeKind
            };

            // Assert
            node.TypeKind.Should().Be(typeKind);
        }

        [Fact]
        public void CodeNode_CustomProperties_ShouldStoreCorrectly()
        {
            // Arrange
            var node = new CodeNode();

            // Act
            node.Properties["customKey1"] = "customValue1";
            node.Properties["customKey2"] = 42;
            node.Properties["customKey3"] = true;

            // Assert
            node.Properties.Should().HaveCount(3);
            node.Properties["customKey1"].Should().Be("customValue1");
            node.Properties["customKey2"].Should().Be(42);
            node.Properties["customKey3"].Should().Be(true);
        }
    }

    public class CodeRelationshipTests
    {
        [Fact]
        public void CodeRelationship_DefaultConstructor_ShouldInitializeCollections()
        {
            // Act
            var relationship = new CodeRelationship();

            // Assert
            relationship.Properties.Should().NotBeNull();
            relationship.Properties.Should().BeEmpty();
            relationship.CreatedAt.Should().Be(default);
        }

        [Fact]
        public void CodeRelationship_WithProperties_ShouldSetCorrectly()
        {
            // Arrange
            var id = Guid.NewGuid().ToString();
            var sourceId = "source-node";
            var targetId = "target-node";
            var usageCount = 5;
            var context = "method call";

            // Act
            var relationship = new CodeRelationship
            {
                Id = id,
                SourceNodeId = sourceId,
                TargetNodeId = targetId,
                Type = RelationshipType.Calls,
                UsageCount = usageCount,
                Context = context,
                IsImplicit = false,
                Strength = 0.8
            };

            // Assert
            relationship.Id.Should().Be(id);
            relationship.SourceNodeId.Should().Be(sourceId);
            relationship.TargetNodeId.Should().Be(targetId);
            relationship.Type.Should().Be(RelationshipType.Calls);
            relationship.UsageCount.Should().Be(usageCount);
            relationship.Context.Should().Be(context);
            relationship.IsImplicit.Should().BeFalse();
            relationship.Strength.Should().Be(0.8);
        }

        [Theory]
        [InlineData(RelationshipType.Contains)]
        [InlineData(RelationshipType.Inherits)]
        [InlineData(RelationshipType.Implements)]
        [InlineData(RelationshipType.Calls)]
        [InlineData(RelationshipType.Uses)]
        [InlineData(RelationshipType.References)]
        [InlineData(RelationshipType.Declares)]
        [InlineData(RelationshipType.Overrides)]
        [InlineData(RelationshipType.Returns)]
        [InlineData(RelationshipType.Parameter)]
        [InlineData(RelationshipType.Throws)]
        [InlineData(RelationshipType.Attribute)]
        [InlineData(RelationshipType.GenericConstraint)]
        [InlineData(RelationshipType.Dependency)]
        public void CodeRelationship_WithDifferentTypes_ShouldSetCorrectly(RelationshipType relationshipType)
        {
            // Act
            var relationship = new CodeRelationship
            {
                Type = relationshipType
            };

            // Assert
            relationship.Type.Should().Be(relationshipType);
        }

        [Fact]
        public void CodeRelationship_CustomProperties_ShouldStoreCorrectly()
        {
            // Arrange
            var relationship = new CodeRelationship();

            // Act
            relationship.Properties["weight"] = 1.5;
            relationship.Properties["direction"] = "bidirectional";
            relationship.Properties["metadata"] = new { key = "value" };

            // Assert
            relationship.Properties.Should().HaveCount(3);
            relationship.Properties["weight"].Should().Be(1.5);
            relationship.Properties["direction"].Should().Be("bidirectional");
            relationship.Properties["metadata"].Should().NotBeNull();
        }
    }

    public class GraphSchemaTests
    {
        [Fact]
        public void GraphSchema_Constants_ShouldBeCorrect()
        {
            // Assert
            GraphSchema.GRAPH_NAME.Should().Be("codivus");
        }

        [Fact]
        public void GraphSchema_VertexLabels_ShouldContainAllNodeTypes()
        {
            // Assert
            GraphSchema.VertexLabels.Namespace.Should().Be("namespace");
            GraphSchema.VertexLabels.Type.Should().Be("type");
            GraphSchema.VertexLabels.Method.Should().Be("method");
            GraphSchema.VertexLabels.Property.Should().Be("property");
            GraphSchema.VertexLabels.Field.Should().Be("field");
            GraphSchema.VertexLabels.Parameter.Should().Be("parameter");
            GraphSchema.VertexLabels.File.Should().Be("file");
            GraphSchema.VertexLabels.Project.Should().Be("project");
            GraphSchema.VertexLabels.Assembly.Should().Be("assembly");
        }

        [Fact]
        public void GraphSchema_EdgeLabels_ShouldContainAllRelationshipTypes()
        {
            // Assert
            GraphSchema.EdgeLabels.Contains.Should().Be("contains");
            GraphSchema.EdgeLabels.Inherits.Should().Be("inherits");
            GraphSchema.EdgeLabels.Implements.Should().Be("implements");
            GraphSchema.EdgeLabels.Calls.Should().Be("calls");
            GraphSchema.EdgeLabels.Uses.Should().Be("uses");
            GraphSchema.EdgeLabels.References.Should().Be("references");
            GraphSchema.EdgeLabels.Declares.Should().Be("declares");
            GraphSchema.EdgeLabels.Overrides.Should().Be("overrides");
            GraphSchema.EdgeLabels.Returns.Should().Be("returns");
            GraphSchema.EdgeLabels.HasParameter.Should().Be("hasParameter");
            GraphSchema.EdgeLabels.Throws.Should().Be("throws");
            GraphSchema.EdgeLabels.HasAttribute.Should().Be("hasAttribute");
            GraphSchema.EdgeLabels.HasGenericConstraint.Should().Be("hasGenericConstraint");
            GraphSchema.EdgeLabels.DependsOn.Should().Be("dependsOn");
        }

        [Fact]
        public void GraphSchema_PropertyKeys_ShouldContainCommonProperties()
        {
            // Assert
            GraphSchema.PropertyKeys.Name.Should().Be("name");
            GraphSchema.PropertyKeys.FullName.Should().Be("fullName");
            GraphSchema.PropertyKeys.NodeType.Should().Be("nodeType");
            GraphSchema.PropertyKeys.RepositoryId.Should().Be("repositoryId");
            GraphSchema.PropertyKeys.CreatedAt.Should().Be("createdAt");
            GraphSchema.PropertyKeys.UpdatedAt.Should().Be("updatedAt");
        }

        [Fact]
        public void GraphSchema_Indexes_ShouldContainExpectedIndexes()
        {
            // Assert
            GraphSchema.Indexes.VertexIndexes.Should().ContainKeys("byFullName", "byRepository", "byProject", "byFile", "byType", "byChecksum");
            GraphSchema.Indexes.CompositeIndexes.Should().ContainKeys("repositoryAndType", "projectAndType", "fileAndLine");
        }
    }

    public class GraphMetricsTests
    {
        [Fact]
        public void GraphMetrics_DefaultConstructor_ShouldInitializeCollections()
        {
            // Act
            var metrics = new GraphMetrics();

            // Assert
            metrics.VertexCountByType.Should().NotBeNull();
            metrics.EdgeCountByType.Should().NotBeNull();
            metrics.VertexCountByType.Should().BeEmpty();
            metrics.EdgeCountByType.Should().BeEmpty();
        }

        [Fact]
        public void GraphMetrics_WithValues_ShouldSetCorrectly()
        {
            // Arrange
            var repositoryId = "test-repo";
            var timestamp = DateTime.UtcNow;

            // Act
            var metrics = new GraphMetrics
            {
                RepositoryId = repositoryId,
                Timestamp = timestamp,
                VertexCount = 1000,
                EdgeCount = 2500,
                TotalProjects = 5,
                TotalFiles = 200,
                TotalTypes = 150,
                TotalMethods = 800,
                AverageComplexity = 3.5,
                AverageCoupling = 2.1,
                ProcessingTimeMs = 15000,
                MemoryUsageBytes = 1024 * 1024 * 50, // 50MB
                ErrorCount = 2,
                WarningCount = 15
            };

            // Assert
            metrics.RepositoryId.Should().Be(repositoryId);
            metrics.Timestamp.Should().Be(timestamp);
            metrics.VertexCount.Should().Be(1000);
            metrics.EdgeCount.Should().Be(2500);
            metrics.TotalProjects.Should().Be(5);
            metrics.TotalFiles.Should().Be(200);
            metrics.TotalTypes.Should().Be(150);
            metrics.TotalMethods.Should().Be(800);
            metrics.AverageComplexity.Should().Be(3.5);
            metrics.AverageCoupling.Should().Be(2.1);
            metrics.ProcessingTimeMs.Should().Be(15000);
            metrics.MemoryUsageBytes.Should().Be(1024 * 1024 * 50);
            metrics.ErrorCount.Should().Be(2);
            metrics.WarningCount.Should().Be(15);
        }

        [Fact]
        public void GraphMetrics_CountsByType_ShouldStoreCorrectly()
        {
            // Arrange
            var metrics = new GraphMetrics();

            // Act
            metrics.VertexCountByType["Type"] = 150;
            metrics.VertexCountByType["Method"] = 800;
            metrics.EdgeCountByType["Calls"] = 1200;
            metrics.EdgeCountByType["Uses"] = 300;

            // Assert
            metrics.VertexCountByType.Should().HaveCount(2);
            metrics.VertexCountByType["Type"].Should().Be(150);
            metrics.VertexCountByType["Method"].Should().Be(800);
            metrics.EdgeCountByType.Should().HaveCount(2);
            metrics.EdgeCountByType["Calls"].Should().Be(1200);
            metrics.EdgeCountByType["Uses"].Should().Be(300);
        }
    }

    public class GraphQueryMetricsTests
    {
        [Fact]
        public void GraphQueryMetrics_DefaultConstructor_ShouldInitializeCollections()
        {
            // Act
            var metrics = new GraphQueryMetrics();

            // Assert
            metrics.QueryParameters.Should().NotBeNull();
            metrics.QueryParameters.Should().BeEmpty();
        }

        [Fact]
        public void GraphQueryMetrics_WithValues_ShouldSetCorrectly()
        {
            // Arrange
            var queryId = "query-123";
            var timestamp = DateTime.UtcNow;
            var queryType = "GetDependencies";

            // Act
            var metrics = new GraphQueryMetrics
            {
                QueryId = queryId,
                Timestamp = timestamp,
                QueryType = queryType,
                ExecutionTimeMs = 250,
                ResultCount = 42,
                TraversedVertices = 100,
                TraversedEdges = 150,
                FromCache = true
            };

            // Assert
            metrics.QueryId.Should().Be(queryId);
            metrics.Timestamp.Should().Be(timestamp);
            metrics.QueryType.Should().Be(queryType);
            metrics.ExecutionTimeMs.Should().Be(250);
            metrics.ResultCount.Should().Be(42);
            metrics.TraversedVertices.Should().Be(100);
            metrics.TraversedEdges.Should().Be(150);
            metrics.FromCache.Should().BeTrue();
        }
    }
}