using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.Graph.Services;
using Codivus.Graph.Models;

namespace Codivus.Graph.Tests.Services
{
    public class ContextualPromptBuilderTests
    {
        private readonly Mock<ILogger<ContextualPromptBuilder>> _mockLogger;
        private readonly ContextualPromptBuilder _service;

        public ContextualPromptBuilderTests()
        {
            _mockLogger = new Mock<ILogger<ContextualPromptBuilder>>();
            _service = new ContextualPromptBuilder(_mockLogger.Object);
        }

        [Theory]
        [InlineData("security")]
        [InlineData("performance")]
        [InlineData("maintainability")]
        [InlineData("architecture")]
        [InlineData("integration")]
        public async Task BuildAnalysisPromptAsync_DifferentAnalysisTypes_ContainsCorrectInstructions(string analysisType)
        {
            // Arrange
            var code = "public class TestClass { }";
            var context = CreateTestContext();

            // Act
            var result = await _service.BuildAnalysisPromptAsync(code, context, analysisType);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("You are an expert code analyzer");
            result.Should().Contain($"Analyze the provided code for {analysisType} issues");
            result.Should().Contain("## Architectural Context");
            result.Should().Contain("## Code to Analyze");
            result.Should().Contain(code);
            result.Should().Contain("## Required Output Format");
            
            // Check for analysis-specific instructions
            if (analysisType == "security")
            {
                result.Should().Contain("Input Validation");
                result.Should().Contain("Authentication/Authorization");
            }
            else if (analysisType == "performance")
            {
                result.Should().Contain("Algorithmic Complexity");
                result.Should().Contain("Database Access");
            }
        }

        [Fact]
        public async Task BuildAnalysisPromptAsync_WithRelationships_IncludesKeyRelationships()
        {
            // Arrange
            var code = "public class TestClass { }";
            var context = CreateTestContext();

            // Act
            var result = await _service.BuildAnalysisPromptAsync(code, context, "general");

            // Assert
            result.Should().Contain("## Key Relationships");
            result.Should().Contain("TestClass inherits from BaseClass");
            result.Should().Contain("TestClass uses HelperClass");
        }

        [Fact]
        public async Task BuildArchitecturalPromptAsync_ValidContext_ContainsArchitectureAnalysis()
        {
            // Arrange
            var context = CreateTestContext();
            var focus = "patterns, coupling, cohesion";

            // Act
            var result = await _service.BuildArchitecturalPromptAsync(context, focus);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("You are a software architect");
            result.Should().Contain($"Focus your analysis on: {focus}");
            result.Should().Contain("## Component Structure");
            result.Should().Contain("**Namespaces:**");
            result.Should().Contain("## Dependency Patterns");
            result.Should().Contain("Architectural Pattern Identification");
            result.Should().Contain("Design Quality Assessment");
        }

        [Fact]
        public async Task BuildDependencyPromptAsync_ValidDependencies_ContainsDependencyAnalysis()
        {
            // Arrange
            var code = "public class TestClass { }";
            var dependencies = new[]
            {
                new CodeElementInfo
                {
                    ElementId = "dep1",
                    Name = "DependencyClass",
                    FullName = "Namespace.DependencyClass",
                    Type = NodeType.Type,
                    FilePath = "/src/dep.cs"
                },
                new CodeElementInfo
                {
                    ElementId = "dep2",
                    Name = "AnotherDependency",
                    FullName = "Namespace.AnotherDependency",
                    Type = NodeType.Type,
                    FilePath = "/src/another.cs"
                }
            };

            // Act
            var result = await _service.BuildDependencyPromptAsync(code, dependencies);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("You are analyzing code dependencies");
            result.Should().Contain("## Dependencies Context");
            result.Should().Contain("**Total Dependencies:** 2");
            result.Should().Contain("DependencyClass");
            result.Should().Contain("AnotherDependency");
            result.Should().Contain("Circular Dependencies");
            result.Should().Contain("Missing Abstractions");
        }

        [Fact]
        public async Task BuildIntegrationPromptAsync_ValidContext_ContainsIntegrationAnalysis()
        {
            // Arrange
            var code = "public class TestService { }";
            var context = CreateTestContextWithInterfaces();

            // Act
            var result = await _service.BuildIntegrationPromptAsync(code, context);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("You are an expert at detecting integration-level issues");
            result.Should().Contain("## Integration Context");
            result.Should().Contain("## Integration Patterns Detected");
            result.Should().Contain("**Interfaces:** 1");
            result.Should().Contain("Cross-Cutting Concerns");
            result.Should().Contain("Data Flow Issues");
            result.Should().Contain("Contract Violations");
        }

        [Fact]
        public async Task BuildAnalysisPromptAsync_LargeContext_LimitsOutput()
        {
            // Arrange
            var code = "public class TestClass { }";
            var context = CreateLargeTestContext();

            // Act
            var result = await _service.BuildAnalysisPromptAsync(code, context, "general");

            // Assert
            result.Should().NotBeNullOrEmpty();
            // Should limit components to 10
            var componentCount = result.Split(new[] { "- " }, StringSplitOptions.None).Length - 1;
            componentCount.Should().BeLessOrEqualTo(20); // Some buffer for other list items and context metadata
        }

        [Fact]
        public async Task BuildIntegrationPromptAsync_WithCrossFileRelationships_ShowsInteractions()
        {
            // Arrange
            var code = "public class TestService { }";
            var context = CreateTestContextWithCrossFileRelationships();

            // Act
            var result = await _service.BuildIntegrationPromptAsync(code, context);

            // Assert
            result.Should().Contain("## Key Component Interactions");
            result.Should().Contain("ServiceA (/src/serviceA.cs) → ServiceB (/src/serviceB.cs)");
        }

        private GraphContext CreateTestContext()
        {
            return new GraphContext
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
                        NodeType = NodeType.Type,
                        Properties = new Dictionary<string, object> { ["filePath"] = "/src/test.cs" }
                    },
                    new CodeNode
                    {
                        Id = "node2",
                        Name = "BaseClass",
                        FullName = "Namespace.BaseClass",
                        NodeType = NodeType.Type,
                        Properties = new Dictionary<string, object> { ["filePath"] = "/src/base.cs" }
                    },
                    new CodeNode
                    {
                        Id = "node3",
                        Name = "HelperClass",
                        FullName = "Namespace.HelperClass",
                        NodeType = NodeType.Type,
                        Properties = new Dictionary<string, object> { ["filePath"] = "/src/helper.cs" }
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
                    },
                    new CodeRelationship
                    {
                        Id = "rel2",
                        SourceNodeId = "node1",
                        TargetNodeId = "node3",
                        Type = RelationshipType.Uses
                    }
                }
            };
        }

        private GraphContext CreateTestContextWithInterfaces()
        {
            var context = CreateTestContext();
            context.Nodes.Add(new CodeNode
            {
                Id = "interface1",
                Name = "ITestInterface",
                FullName = "Namespace.ITestInterface",
                NodeType = NodeType.Type,
                TypeKind = TypeKind.Interface,
                Properties = new Dictionary<string, object> { ["filePath"] = "/src/interface.cs" }
            });
            context.Relationships.Add(new CodeRelationship
            {
                Id = "impl1",
                SourceNodeId = "node1",
                TargetNodeId = "interface1",
                Type = RelationshipType.Implements
            });
            return context;
        }

        private GraphContext CreateTestContextWithCrossFileRelationships()
        {
            return new GraphContext
            {
                RepositoryId = "test-repo",
                FocusFilePath = "/src/test.cs",
                Nodes = new List<CodeNode>
                {
                    new CodeNode
                    {
                        Id = "serviceA",
                        Name = "ServiceA",
                        FullName = "Namespace.ServiceA",
                        NodeType = NodeType.Type,
                        Properties = new Dictionary<string, object> { ["filePath"] = "/src/serviceA.cs" }
                    },
                    new CodeNode
                    {
                        Id = "serviceB",
                        Name = "ServiceB",
                        FullName = "Namespace.ServiceB",
                        NodeType = NodeType.Type,
                        Properties = new Dictionary<string, object> { ["filePath"] = "/src/serviceB.cs" }
                    }
                },
                Relationships = new List<CodeRelationship>
                {
                    new CodeRelationship
                    {
                        Id = "cross1",
                        SourceNodeId = "serviceA",
                        TargetNodeId = "serviceB",
                        Type = RelationshipType.Uses
                    }
                }
            };
        }

        private GraphContext CreateLargeTestContext()
        {
            var context = new GraphContext
            {
                RepositoryId = "test-repo",
                FocusFilePath = "/src/test.cs",
                Nodes = new List<CodeNode>(),
                Relationships = new List<CodeRelationship>()
            };

            // Add many nodes
            for (int i = 0; i < 50; i++)
            {
                context.Nodes.Add(new CodeNode
                {
                    Id = $"node{i}",
                    Name = $"Class{i}",
                    FullName = $"Namespace.Class{i}",
                    NodeType = NodeType.Type,
                    Properties = new Dictionary<string, object> { ["filePath"] = $"/src/class{i}.cs" }
                });
            }

            return context;
        }
    }
}