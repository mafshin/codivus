using Xunit;
using FluentAssertions;
using Codivus.Graph.Interfaces;
using System;
using System.Reflection;

namespace Codivus.Graph.Tests.Interfaces
{
    public class GraphInterfaceTests
    {
        [Fact]
        public void IGraphStorageService_ShouldHaveRequiredMethods()
        {
            // Arrange
            var interfaceType = typeof(IGraphStorageService);

            // Act & Assert
            interfaceType.Should().NotBeNull();
            interfaceType.IsInterface.Should().BeTrue();
            
            // Check some core methods exist
            var initMethod = interfaceType.GetMethod("InitializeAsync");
            initMethod.Should().NotBeNull();
            
            var createNodeMethod = interfaceType.GetMethod("CreateNodeAsync");
            createNodeMethod.Should().NotBeNull();
        }

        [Fact]
        public void IRoslynAnalyzer_ShouldHaveRequiredMethods()
        {
            // Arrange
            var interfaceType = typeof(IRoslynAnalyzer);

            // Act & Assert
            interfaceType.Should().NotBeNull();
            interfaceType.IsInterface.Should().BeTrue();
            
            var analyzeFileMethod = interfaceType.GetMethod("AnalyzeFileAsync");
            analyzeFileMethod.Should().NotBeNull();
        }

        [Fact]
        public void ICodeGraphBuilder_ShouldHaveRequiredMethods()
        {
            // Arrange
            var interfaceType = typeof(ICodeGraphBuilder);

            // Act & Assert
            interfaceType.Should().NotBeNull();
            interfaceType.IsInterface.Should().BeTrue();
            
            var buildMethod = interfaceType.GetMethod("BuildGraphAsync");
            buildMethod.Should().NotBeNull();
        }

        [Fact]
        public void IGraphQueryService_ShouldHaveRequiredMethods()
        {
            // Arrange
            var interfaceType = typeof(IGraphQueryService);

            // Act & Assert
            interfaceType.Should().NotBeNull();
            interfaceType.IsInterface.Should().BeTrue();
            
            var getCallHierarchyMethod = interfaceType.GetMethod("GetCallHierarchyAsync");
            getCallHierarchyMethod.Should().NotBeNull();
        }

        [Fact]
        public void IGraphStorageService_ShouldInheritFromIDisposable()
        {
            // Act & Assert
            typeof(IGraphStorageService).Should().BeAssignableTo<IDisposable>();
        }
    }
}