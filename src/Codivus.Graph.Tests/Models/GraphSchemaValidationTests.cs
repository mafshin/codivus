using Xunit;
using FluentAssertions;
using Codivus.Graph.Models;
using System;
using System.Linq;
using System.Reflection;

namespace Codivus.Graph.Tests.Models
{
    public class GraphSchemaValidationTests
    {
        [Fact]
        public void GraphSchema_VertexLabels_ShouldContainAllRequiredLabels()
        {
            // Act & Assert
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
        public void GraphSchema_EdgeLabels_ShouldContainAllRequiredLabels()
        {
            // Act & Assert
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
        public void GraphSchema_PropertyKeys_ShouldContainRequiredProperties()
        {
            // Act & Assert - Common properties
            GraphSchema.PropertyKeys.Name.Should().Be("name");
            GraphSchema.PropertyKeys.FullName.Should().Be("fullName");
            GraphSchema.PropertyKeys.NodeType.Should().Be("nodeType");
            GraphSchema.PropertyKeys.RepositoryId.Should().Be("repositoryId");
            GraphSchema.PropertyKeys.FileId.Should().Be("fileId");
            GraphSchema.PropertyKeys.ProjectId.Should().Be("projectId");
            GraphSchema.PropertyKeys.StartLine.Should().Be("startLine");
            GraphSchema.PropertyKeys.CreatedAt.Should().Be("createdAt");
            GraphSchema.PropertyKeys.UpdatedAt.Should().Be("updatedAt");
            
            // Type properties
            GraphSchema.PropertyKeys.TypeKind.Should().Be("typeKind");
            GraphSchema.PropertyKeys.Accessibility.Should().Be("accessibility");
            GraphSchema.PropertyKeys.IsAbstract.Should().Be("isAbstract");
            GraphSchema.PropertyKeys.IsSealed.Should().Be("isSealed");
            GraphSchema.PropertyKeys.IsStatic.Should().Be("isStatic");
            
            // Method properties
            GraphSchema.PropertyKeys.ReturnType.Should().Be("returnType");
            GraphSchema.PropertyKeys.Signature.Should().Be("signature");
            GraphSchema.PropertyKeys.LineCount.Should().Be("lineCount");
            GraphSchema.PropertyKeys.CyclomaticComplexity.Should().Be("cyclomaticComplexity");
        }

        [Fact]
        public void GraphSchema_Indexes_ShouldIncludePerformanceCriticalProperties()
        {
            // Act & Assert
            var vertexIndexes = GraphSchema.Indexes.VertexIndexes;
            var compositeIndexes = GraphSchema.Indexes.CompositeIndexes;
            
            vertexIndexes.Should().NotBeEmpty("VertexIndexes should be defined");
            compositeIndexes.Should().NotBeEmpty("CompositeIndexes should be defined");
            
            // Check for critical indexes
            vertexIndexes.Should().ContainKey("byRepository");
            vertexIndexes.Should().ContainKey("byFullName");
            vertexIndexes.Should().ContainKey("byType");
            vertexIndexes.Should().ContainKey("byFile");
            
            compositeIndexes.Should().ContainKey("repositoryAndType");
        }

        [Fact]
        public void GraphSchema_ShouldSupportGraphTraversalPatterns()
        {
            // Act & Assert - Verify that the schema supports common graph traversal patterns
            
            // 1. Finding all methods in a class
            GraphSchema.EdgeLabels.Contains.Should().Be("contains");
            GraphSchema.VertexLabels.Type.Should().Be("type");
            GraphSchema.VertexLabels.Method.Should().Be("method");
            
            // 2. Finding inheritance hierarchies
            GraphSchema.EdgeLabels.Inherits.Should().Be("inherits");
            GraphSchema.EdgeLabels.Implements.Should().Be("implements");
            
            // 3. Finding method call chains
            GraphSchema.EdgeLabels.Calls.Should().Be("calls");
            
            // 4. Finding usage relationships
            GraphSchema.EdgeLabels.Uses.Should().Be("uses");
            GraphSchema.EdgeLabels.References.Should().Be("references");
        }

        [Fact]
        public void GraphSchema_ShouldSupportMetricsCalculation()
        {
            // Act & Assert - Verify schema supports metrics calculation
            
            // Properties needed for complexity metrics
            GraphSchema.PropertyKeys.LineCount.Should().Be("lineCount");
            GraphSchema.PropertyKeys.CyclomaticComplexity.Should().Be("cyclomaticComplexity");
            
            // Properties needed for coupling metrics
            GraphSchema.PropertyKeys.CouplingCount.Should().Be("couplingCount");
            
            // Properties needed for cohesion metrics
            GraphSchema.PropertyKeys.IsStatic.Should().Be("isStatic");
            GraphSchema.PropertyKeys.Accessibility.Should().Be("accessibility");
        }

        [Fact]
        public void GraphSchema_ShouldSupportVersioning()
        {
            // Act & Assert - Verify schema supports version tracking
            GraphSchema.PropertyKeys.CreatedAt.Should().Be("createdAt");
            GraphSchema.PropertyKeys.UpdatedAt.Should().Be("updatedAt");
            GraphSchema.PropertyKeys.Checksum.Should().Be("checksum");
        }

        [Fact]
        public void GraphSchema_PropertyKeys_ShouldFollowNamingConventions()
        {
            // Arrange
            var propertyKeyType = typeof(GraphSchema.PropertyKeys);
            var keyFields = propertyKeyType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .ToList();
            
            // Act & Assert
            foreach (var field in keyFields)
            {
                var value = (string)field.GetValue(null);
                
                // Schema keys should follow camelCase convention
                value.Should().MatchRegex(@"^[a-z][a-zA-Z0-9]*$", 
                    $"Schema key '{value}' should only contain letters and numbers, starting with lowercase (camelCase)");
            }
        }

        [Fact]
        public void GraphSchema_Labels_ShouldFollowNamingConventions()
        {
            // Arrange
            var vertexLabelType = typeof(GraphSchema.VertexLabels);
            var edgeLabelType = typeof(GraphSchema.EdgeLabels);
            
            var vertexLabels = vertexLabelType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToList();
                
            var edgeLabels = edgeLabelType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToList();
            
            var allLabels = vertexLabels.Concat(edgeLabels).ToList();

            // Act & Assert
            foreach (var label in allLabels)
            {
                // Labels should follow camelCase convention
                label.Should().MatchRegex(@"^[a-z][a-zA-Z0-9]*$", 
                    $"Label '{label}' should only contain letters and numbers, starting with lowercase (camelCase)");
            }
        }

        [Fact]
        public void GraphSchema_VertexLabels_ShouldHaveUniqueValues()
        {
            // Arrange
            var vertexLabelType = typeof(GraphSchema.VertexLabels);
            var labels = vertexLabelType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToList();

            // Act
            var uniqueLabels = labels.Distinct().ToList();

            // Assert
            labels.Should().HaveSameCount(uniqueLabels, "All vertex labels should be unique");
        }

        [Fact]
        public void GraphSchema_EdgeLabels_ShouldHaveUniqueValues()
        {
            // Arrange
            var edgeLabelType = typeof(GraphSchema.EdgeLabels);
            var labels = edgeLabelType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToList();

            // Act
            var uniqueLabels = labels.Distinct().ToList();

            // Assert
            labels.Should().HaveSameCount(uniqueLabels, "All edge labels should be unique");
        }

        [Fact]
        public void GraphSchema_GraphName_ShouldBeCorrect()
        {
            // Act & Assert
            GraphSchema.GRAPH_NAME.Should().Be("codivus");
        }
    }
}