using Xunit;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Codivus.Graph.Services;
using Codivus.Graph.Models;
using System.Linq;

namespace Codivus.Graph.Tests.Services
{
    public class SymbolExtractorsTests
    {
        [Fact]
        public void NamespaceExtractor_WithNamespace_ShouldExtractNode()
        {
            // Arrange
            var code = @"
namespace TestNamespace
{
    public class TestClass { }
}";
            var (semanticModel, syntaxTree) = CreateSemanticModel(code);
            var extractor = new NamespaceExtractor(semanticModel, "file1", "repo1");

            // Act
            extractor.Visit(syntaxTree.GetRoot());

            // Assert
            extractor.Nodes.Should().HaveCount(1);
            var node = extractor.Nodes.First();
            node.Name.Should().Be("TestNamespace");
            node.NodeType.Should().Be(NodeType.Namespace);
        }

        [Fact]
        public void TypeExtractor_WithClass_ShouldExtractNode()
        {
            // Arrange
            var code = @"
namespace TestNamespace
{
    public class TestClass { }
}";
            var (semanticModel, syntaxTree) = CreateSemanticModel(code);
            var extractor = new TypeExtractor(semanticModel, "file1", "repo1");

            // Act
            extractor.Visit(syntaxTree.GetRoot());

            // Assert
            extractor.Nodes.Should().HaveCount(1);
            var node = extractor.Nodes.First();
            node.Name.Should().Be("TestClass");
            node.NodeType.Should().Be(NodeType.Type);
        }

        [Fact]
        public void MemberExtractor_WithMethod_ShouldExtractNode()
        {
            // Arrange
            var code = @"
namespace TestNamespace
{
    public class TestClass 
    { 
        public void TestMethod() { }
    }
}";
            var (semanticModel, syntaxTree) = CreateSemanticModel(code);
            var extractor = new MemberExtractor(semanticModel, "file1", "repo1");

            // Act
            extractor.Visit(syntaxTree.GetRoot());

            // Assert
            extractor.Nodes.Should().HaveCountGreaterOrEqualTo(1);
            var methodNode = extractor.Nodes.FirstOrDefault(n => n.NodeType == NodeType.Method);
            methodNode.Should().NotBeNull();
            methodNode.Name.Should().Be("TestMethod");
        }

        private static (SemanticModel semanticModel, SyntaxTree syntaxTree) CreateSemanticModel(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("TestAssembly")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(syntaxTree);
            
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            return (semanticModel, syntaxTree);
        }
    }
}