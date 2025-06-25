using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.Graph.Services;
using Codivus.Graph.Models;
using System.IO;
using System.Text;

namespace Codivus.Graph.Tests.Services
{
    public class RoslynAnalyzerTests : IDisposable
    {
        private readonly Mock<ILogger<RoslynAnalyzer>> _mockLogger;
        private readonly RoslynAnalyzer _analyzer;
        private readonly string _tempDirectory;

        public RoslynAnalyzerTests()
        {
            _mockLogger = new Mock<ILogger<RoslynAnalyzer>>();
            _analyzer = new RoslynAnalyzer(_mockLogger.Object);
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Act & Assert
            _analyzer.Should().NotBeNull();
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithSimpleClass_ShouldExtractCorrectNodes()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public string Name { get; set; }
        
        public void TestMethod()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";
            var filePath = Path.Combine(_tempDirectory, "TestClass.cs");
            await File.WriteAllTextAsync(filePath, sourceCode);

            // Act
            var result = await _analyzer.AnalyzeFileAsync(filePath, "test-repo");

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().BeEmpty();
            result.FilePath.Should().Be(filePath);
            result.RepositoryId.Should().Be("test-repo");
            
            // Should have file, namespace, class, property, and method nodes
            result.Nodes.Should().HaveCountGreaterOrEqualTo(5);
            
            // Verify file node
            var fileNode = result.Nodes.FirstOrDefault(n => n.NodeType == NodeType.File);
            fileNode.Should().NotBeNull();
            fileNode.Name.Should().Be("TestClass.cs");

            // Verify namespace node
            var namespaceNode = result.Nodes.FirstOrDefault(n => n.NodeType == NodeType.Namespace);
            namespaceNode.Should().NotBeNull();
            namespaceNode.FullName.Should().Be("TestNamespace");

            // Verify class node
            var classNode = result.Nodes.FirstOrDefault(n => n.NodeType == NodeType.Type);
            classNode.Should().NotBeNull();
            classNode.Name.Should().Be("TestClass");
            classNode.TypeKind.Should().Be(TypeKind.Class);
            classNode.Accessibility.Should().Be(AccessibilityLevel.Public);

            // Verify property node
            var propertyNode = result.Nodes.FirstOrDefault(n => n.NodeType == NodeType.Property);
            propertyNode.Should().NotBeNull();
            propertyNode.Name.Should().Be("Name");

            // Verify method node
            var methodNode = result.Nodes.FirstOrDefault(n => n.NodeType == NodeType.Method);
            methodNode.Should().NotBeNull();
            methodNode.Name.Should().Be("TestMethod");
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithInterface_ShouldExtractCorrectNodes()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public interface ITestInterface
    {
        string GetData();
        int Count { get; }
    }
}";
            var filePath = Path.Combine(_tempDirectory, "ITestInterface.cs");
            await File.WriteAllTextAsync(filePath, sourceCode);

            // Act
            var result = await _analyzer.AnalyzeFileAsync(filePath, "test-repo");

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().BeEmpty();
            
            var interfaceNode = result.Nodes.FirstOrDefault(n => n.NodeType == NodeType.Type);
            interfaceNode.Should().NotBeNull();
            interfaceNode.Name.Should().Be("ITestInterface");
            interfaceNode.TypeKind.Should().Be(TypeKind.Interface);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithInheritance_ShouldDetectRelationships()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class BaseClass
    {
        public virtual void DoSomething() { }
    }

    public class DerivedClass : BaseClass
    {
        public override void DoSomething() { }
    }
}";
            var filePath = Path.Combine(_tempDirectory, "Inheritance.cs");
            await File.WriteAllTextAsync(filePath, sourceCode);

            // Act
            var result = await _analyzer.AnalyzeFileAsync(filePath, "test-repo");

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().BeEmpty();
            result.Relationships.Should().NotBeEmpty();
            
            // Should have inheritance relationship
            var inheritanceRelationship = result.Relationships
                .FirstOrDefault(r => r.Type == RelationshipType.Inherits);
            inheritanceRelationship.Should().NotBeNull();
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithMethodCalls_ShouldDetectCallRelationships()
        {
            // Arrange
            var sourceCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void Method1()
        {
            Method2();
            Console.WriteLine(""Test"");
        }

        public void Method2()
        {
        }
    }
}";
            var filePath = Path.Combine(_tempDirectory, "MethodCalls.cs");
            await File.WriteAllTextAsync(filePath, sourceCode);

            // Act
            var result = await _analyzer.AnalyzeFileAsync(filePath, "test-repo");

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().BeEmpty();
            result.Relationships.Should().NotBeEmpty();
            
            // Should have call relationships
            var callRelationships = result.Relationships
                .Where(r => r.Type == RelationshipType.Calls)
                .ToList();
            callRelationships.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithComplexTypes_ShouldCalculateMetrics()
        {
            // Arrange
            var sourceCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class ComplexClass
    {
        private readonly List<string> _items = new();
        public string Name { get; set; }
        public int Count => _items.Count;

        public void AddItem(string item)
        {
            if (string.IsNullOrEmpty(item))
                throw new ArgumentException(""Item cannot be null or empty"");
            
            _items.Add(item);
        }

        public void ProcessItems()
        {
            foreach (var item in _items)
            {
                if (item.Length > 10)
                {
                    Console.WriteLine($""Long item: {item}"");
                }
                else
                {
                    Console.WriteLine($""Short item: {item}"");
                }
            }
        }
    }
}";
            var filePath = Path.Combine(_tempDirectory, "ComplexClass.cs");
            await File.WriteAllTextAsync(filePath, sourceCode);

            // Act
            var result = await _analyzer.AnalyzeFileAsync(filePath, "test-repo");

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().BeEmpty();
            result.Metrics.Should().NotBeNull();
            result.Metrics.LinesOfCode.Should().BeGreaterThan(0);
            result.Metrics.TypeCount.Should().Be(1);
            result.Metrics.MethodCount.Should().BeGreaterThan(0);
            result.Metrics.PropertyCount.Should().BeGreaterThan(0);
            result.Metrics.FieldCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithNonExistentFile_ShouldReturnError()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_tempDirectory, "NonExistent.cs");

            // Act
            var result = await _analyzer.AnalyzeFileAsync(nonExistentPath, "test-repo");

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().NotBeEmpty();
            result.Errors.First().Should().Contain("File not found");
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithInvalidSyntax_ShouldHandleGracefully()
        {
            // Arrange
            var invalidCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public void InvalidMethod(
        // Missing closing parenthesis and brace
";
            var filePath = Path.Combine(_tempDirectory, "InvalidSyntax.cs");
            await File.WriteAllTextAsync(filePath, invalidCode);

            // Act
            var result = await _analyzer.AnalyzeFileAsync(filePath, "test-repo");

            // Assert
            result.Should().NotBeNull();
            // Even with syntax errors, we should get some basic structure
            result.FilePath.Should().Be(filePath);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithGenericTypes_ShouldExtractGenerics()
        {
            // Arrange
            var sourceCode = @"
using System.Collections.Generic;

namespace TestNamespace
{
    public class GenericClass<T, U> where T : class
    {
        private readonly Dictionary<T, U> _items = new();

        public void Add<V>(T key, U value, V extra) where V : struct
        {
            _items[key] = value;
        }

        public U Get(T key)
        {
            return _items.TryGetValue(key, out var value) ? value : default(U);
        }
    }
}";
            var filePath = Path.Combine(_tempDirectory, "GenericClass.cs");
            await File.WriteAllTextAsync(filePath, sourceCode);

            // Act
            var result = await _analyzer.AnalyzeFileAsync(filePath, "test-repo");

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().BeEmpty();
            
            var classNode = result.Nodes.FirstOrDefault(n => n.NodeType == NodeType.Type);
            classNode.Should().NotBeNull();
            classNode.Properties.Should().ContainKey("IsGeneric");
            classNode.Properties["IsGeneric"].Should().Be(true);
        }

        [Fact]
        public async Task ExtractSymbols_WithSampleCode_ShouldReturnAllSymbols()
        {
            // Arrange
            var sourceCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string Property { get; set; }
        private int _field;

        public void Method(string parameter)
        {
        }
    }
}";
            var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(sourceCode);
            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("test", new[] { syntaxTree });
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Act
            var nodes = _analyzer.ExtractSymbols(syntaxTree, semanticModel, "file-id", "repo-id").ToList();

            // Assert
            nodes.Should().NotBeEmpty();
            
            // Should have different types of nodes
            nodes.Should().Contain(n => n.NodeType == NodeType.File);
            nodes.Should().Contain(n => n.NodeType == NodeType.Namespace);
            nodes.Should().Contain(n => n.NodeType == NodeType.Type);
            nodes.Should().Contain(n => n.NodeType == NodeType.Property);
            nodes.Should().Contain(n => n.NodeType == NodeType.Field);
            nodes.Should().Contain(n => n.NodeType == NodeType.Method);
            nodes.Should().Contain(n => n.NodeType == NodeType.Parameter);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
    }
}