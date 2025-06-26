using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services;

/// <summary>
/// Extracts namespace declarations from syntax trees
/// </summary>
public class NamespaceExtractor : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _fileId;
    private readonly string _repositoryId;
    
    public List<CodeNode> Nodes { get; } = new();

    public NamespaceExtractor(SemanticModel semanticModel, string fileId, string repositoryId)
    {
        _semanticModel = semanticModel;
        _fileId = fileId;
        _repositoryId = repositoryId;
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetSymbolInfo(node.Name).Symbol as INamespaceSymbol;
        if (symbol != null)
        {
            var namespaceNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                NodeType = NodeType.Namespace,
                RepositoryId = _repositoryId,
                FileId = _fileId,
                StartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            namespaceNode.Properties["IsGlobalNamespace"] = symbol.IsGlobalNamespace;
            Nodes.Add(namespaceNode);
        }

        base.VisitNamespaceDeclaration(node);
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetSymbolInfo(node.Name).Symbol as INamespaceSymbol;
        if (symbol != null)
        {
            var namespaceNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                NodeType = NodeType.Namespace,
                RepositoryId = _repositoryId,
                FileId = _fileId,
                StartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            namespaceNode.Properties["IsGlobalNamespace"] = symbol.IsGlobalNamespace;
            namespaceNode.Properties["IsFileScoped"] = true;
            Nodes.Add(namespaceNode);
        }

        base.VisitFileScopedNamespaceDeclaration(node);
    }
}

/// <summary>
/// Extracts type declarations (classes, interfaces, structs, enums) from syntax trees
/// </summary>
public class TypeExtractor : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _fileId;
    private readonly string _repositoryId;
    
    public List<CodeNode> Nodes { get; } = new();

    public TypeExtractor(SemanticModel semanticModel, string fileId, string repositoryId)
    {
        _semanticModel = semanticModel;
        _fileId = fileId;
        _repositoryId = repositoryId;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        CreateTypeNode(node, Models.TypeKind.Class);
        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        CreateTypeNode(node, Models.TypeKind.Interface);
        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        CreateTypeNode(node, Models.TypeKind.Struct);
        base.VisitStructDeclaration(node);
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        CreateEnumNode(node);
        base.VisitEnumDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        CreateTypeNode(node, Models.TypeKind.Class); // Records are classes
        base.VisitRecordDeclaration(node);
    }

    private void CreateEnumNode(EnumDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
        if (symbol != null)
        {
            var typeNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                NodeType = NodeType.Type,
                RepositoryId = _repositoryId,
                FileId = _fileId,
                TypeKind = Models.TypeKind.Enum,
                Accessibility = MapAccessibility(symbol.DeclaredAccessibility),
                IsAbstract = symbol.IsAbstract,
                IsSealed = symbol.IsSealed,
                IsStatic = symbol.IsStatic,
                StartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                LineCount = node.GetLocation().GetLineSpan().EndLinePosition.Line - node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add additional properties
            typeNode.Properties["IsGeneric"] = symbol.IsGenericType;
            typeNode.Properties["TypeParameterCount"] = symbol.TypeParameters.Length;
            
            if (node.Modifiers.Any())
            {
                typeNode.Properties["Modifiers"] = string.Join(", ", node.Modifiers.Select(m => m.Text));
            }

            Nodes.Add(typeNode);
        }
    }

    private void CreateTypeNode(TypeDeclarationSyntax node, Models.TypeKind typeKind)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
        if (symbol != null)
        {
            var typeNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                NodeType = NodeType.Type,
                RepositoryId = _repositoryId,
                FileId = _fileId,
                TypeKind = typeKind,
                Accessibility = MapAccessibility(symbol.DeclaredAccessibility),
                IsAbstract = symbol.IsAbstract,
                IsSealed = symbol.IsSealed,
                IsStatic = symbol.IsStatic,
                StartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                LineCount = node.GetLocation().GetLineSpan().EndLinePosition.Line - node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add additional properties
            typeNode.Properties["IsGeneric"] = symbol.IsGenericType;
            typeNode.Properties["TypeParameterCount"] = symbol.TypeParameters.Length;
            typeNode.Properties["IsRecord"] = symbol.IsRecord;
            
            if (node.Modifiers.Any())
            {
                typeNode.Properties["Modifiers"] = string.Join(", ", node.Modifiers.Select(m => m.Text));
            }

            Nodes.Add(typeNode);
        }
    }

    private static AccessibilityLevel MapAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Private => AccessibilityLevel.Private,
            Accessibility.Protected => AccessibilityLevel.Protected,
            Accessibility.Internal => AccessibilityLevel.Internal,
            Accessibility.Public => AccessibilityLevel.Public,
            Accessibility.ProtectedOrInternal => AccessibilityLevel.ProtectedInternal,
            Accessibility.ProtectedAndInternal => AccessibilityLevel.PrivateProtected,
            _ => AccessibilityLevel.Private
        };
    }
}

/// <summary>
/// Extracts member declarations (methods, properties, fields) from syntax trees
/// </summary>
public class MemberExtractor : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly string _fileId;
    private readonly string _repositoryId;
    
    public List<CodeNode> Nodes { get; } = new();

    public MemberExtractor(SemanticModel semanticModel, string fileId, string repositoryId)
    {
        _semanticModel = semanticModel;
        _fileId = fileId;
        _repositoryId = repositoryId;
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
        if (symbol != null)
        {
            var methodNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                NodeType = NodeType.Method,
                RepositoryId = _repositoryId,
                FileId = _fileId,
                Accessibility = MapAccessibility(symbol.DeclaredAccessibility),
                IsAbstract = symbol.IsAbstract,
                IsStatic = symbol.IsStatic,
                StartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                LineCount = node.GetLocation().GetLineSpan().EndLinePosition.Line - node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add method-specific properties
            methodNode.Properties["IsGeneric"] = symbol.IsGenericMethod;
            methodNode.Properties["ParameterCount"] = symbol.Parameters.Length;
            methodNode.Properties["ReturnType"] = symbol.ReturnType.ToDisplayString();
            methodNode.Properties["IsAsync"] = symbol.IsAsync;
            methodNode.Properties["IsVirtual"] = symbol.IsVirtual;
            methodNode.Properties["IsOverride"] = symbol.IsOverride;

            Nodes.Add(methodNode);

            // Extract parameters
            foreach (var parameter in symbol.Parameters)
            {
                var paramNode = new CodeNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = parameter.Name,
                    FullName = $"{symbol.ToDisplayString()}.{parameter.Name}",
                    NodeType = NodeType.Parameter,
                    RepositoryId = _repositoryId,
                    FileId = _fileId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                paramNode.Properties["ParameterType"] = parameter.Type.ToDisplayString();
                paramNode.Properties["IsOptional"] = parameter.IsOptional;
                paramNode.Properties["HasDefaultValue"] = parameter.HasExplicitDefaultValue;
                if (parameter.HasExplicitDefaultValue)
                {
                    paramNode.Properties["DefaultValue"] = parameter.ExplicitDefaultValue?.ToString() ?? "null";
                }

                Nodes.Add(paramNode);
            }
        }

        base.VisitMethodDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as IPropertySymbol;
        if (symbol != null)
        {
            var propertyNode = new CodeNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                NodeType = NodeType.Property,
                RepositoryId = _repositoryId,
                FileId = _fileId,
                Accessibility = MapAccessibility(symbol.DeclaredAccessibility),
                IsAbstract = symbol.IsAbstract,
                IsStatic = symbol.IsStatic,
                StartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                LineCount = node.GetLocation().GetLineSpan().EndLinePosition.Line - node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            propertyNode.Properties["PropertyType"] = symbol.Type.ToDisplayString();
            propertyNode.Properties["IsReadOnly"] = symbol.IsReadOnly;
            propertyNode.Properties["IsWriteOnly"] = symbol.IsWriteOnly;
            propertyNode.Properties["IsVirtual"] = symbol.IsVirtual;
            propertyNode.Properties["IsOverride"] = symbol.IsOverride;

            Nodes.Add(propertyNode);
        }

        base.VisitPropertyDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        foreach (var variable in node.Declaration.Variables)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
            if (symbol != null)
            {
                var fieldNode = new CodeNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = symbol.Name,
                    FullName = symbol.ToDisplayString(),
                    NodeType = NodeType.Field,
                    RepositoryId = _repositoryId,
                    FileId = _fileId,
                    Accessibility = MapAccessibility(symbol.DeclaredAccessibility),
                    IsStatic = symbol.IsStatic,
                    StartLine = variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                fieldNode.Properties["FieldType"] = symbol.Type.ToDisplayString();
                fieldNode.Properties["IsReadOnly"] = symbol.IsReadOnly;
                fieldNode.Properties["IsConst"] = symbol.IsConst;
                fieldNode.Properties["IsVolatile"] = symbol.IsVolatile;

                Nodes.Add(fieldNode);
            }
        }

        base.VisitFieldDeclaration(node);
    }

    private static AccessibilityLevel MapAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Private => AccessibilityLevel.Private,
            Accessibility.Protected => AccessibilityLevel.Protected,
            Accessibility.Internal => AccessibilityLevel.Internal,
            Accessibility.Public => AccessibilityLevel.Public,
            Accessibility.ProtectedOrInternal => AccessibilityLevel.ProtectedInternal,
            Accessibility.ProtectedAndInternal => AccessibilityLevel.PrivateProtected,
            _ => AccessibilityLevel.Private
        };
    }
}
// Aliases for test compatibility
public class ClassExtractor : TypeExtractor
{
    public ClassExtractor(SemanticModel semanticModel, string fileId, string repositoryId) 
        : base(semanticModel, fileId, repositoryId)
    {
    }
}

public class MethodExtractor : MemberExtractor
{
    public MethodExtractor(SemanticModel semanticModel, string fileId, string repositoryId) 
        : base(semanticModel, fileId, repositoryId)
    {
    }
}

public class PropertyExtractor : MemberExtractor
{
    public PropertyExtractor(SemanticModel semanticModel, string fileId, string repositoryId) 
        : base(semanticModel, fileId, repositoryId)
    {
    }
}

public class FieldExtractor : MemberExtractor
{
    public FieldExtractor(SemanticModel semanticModel, string fileId, string repositoryId) 
        : base(semanticModel, fileId, repositoryId)
    {
    }
}
