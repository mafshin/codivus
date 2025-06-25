using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services;

/// <summary>
/// Detects relationships between code symbols
/// </summary>
internal class RelationshipDetector : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, CodeNode> _nodesByFullName;
    
    public List<CodeRelationship> Relationships { get; } = new();

    public RelationshipDetector(SemanticModel semanticModel, Dictionary<string, CodeNode> nodesByFullName)
    {
        _semanticModel = semanticModel;
        _nodesByFullName = nodesByFullName;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
        if (symbol != null)
        {
            DetectTypeRelationships(symbol, node);
        }

        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
        if (symbol != null)
        {
            DetectTypeRelationships(symbol, node);
        }

        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
        if (symbol != null)
        {
            DetectMethodRelationships(symbol, node);
        }

        base.VisitMethodDeclaration(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        DetectMethodCallRelationships(node);
        base.VisitInvocationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        DetectMemberAccessRelationships(node);
        base.VisitMemberAccessExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        DetectConstructorCallRelationships(node);
        base.VisitObjectCreationExpression(node);
    }

    public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        DetectVariableTypeRelationships(node);
        base.VisitVariableDeclaration(node);
    }

    private void DetectTypeRelationships(INamedTypeSymbol symbol, TypeDeclarationSyntax node)
    {
        var sourceNode = FindNodeBySymbol(symbol);
        if (sourceNode == null) return;

        // Inheritance relationships
        if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            var baseTypeNode = FindNodeBySymbol(symbol.BaseType);
            if (baseTypeNode != null)
            {
                CreateRelationship(sourceNode, baseTypeNode, RelationshipType.Inherits, "inheritance");
            }
        }

        // Interface implementation relationships
        foreach (var interfaceType in symbol.Interfaces)
        {
            var interfaceNode = FindNodeBySymbol(interfaceType);
            if (interfaceNode != null)
            {
                CreateRelationship(sourceNode, interfaceNode, RelationshipType.Implements, "interface implementation");
            }
        }

        // Containment relationships (namespace contains type)
        if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
        {
            var namespaceNode = FindNodeBySymbol(symbol.ContainingNamespace);
            if (namespaceNode != null)
            {
                CreateRelationship(namespaceNode, sourceNode, RelationshipType.Contains, "namespace containment");
            }
        }

        // Generic type constraints
        foreach (var typeParameter in symbol.TypeParameters)
        {
            foreach (var constraint in typeParameter.ConstraintTypes)
            {
                var constraintNode = FindNodeBySymbol(constraint);
                if (constraintNode != null)
                {
                    CreateRelationship(sourceNode, constraintNode, RelationshipType.GenericConstraint, "generic constraint");
                }
            }
        }
    }

    private void DetectMethodRelationships(IMethodSymbol symbol, MethodDeclarationSyntax node)
    {
        var sourceNode = FindNodeBySymbol(symbol);
        if (sourceNode == null) return;

        // Containment (type contains method)
        if (symbol.ContainingType != null)
        {
            var containingTypeNode = FindNodeBySymbol(symbol.ContainingType);
            if (containingTypeNode != null)
            {
                CreateRelationship(containingTypeNode, sourceNode, RelationshipType.Contains, "type contains method");
            }
        }

        // Return type relationship
        if (symbol.ReturnType.SpecialType != SpecialType.System_Void)
        {
            var returnTypeNode = FindNodeBySymbol(symbol.ReturnType);
            if (returnTypeNode != null)
            {
                CreateRelationship(sourceNode, returnTypeNode, RelationshipType.Returns, "return type");
            }
        }

        // Parameter type relationships
        foreach (var parameter in symbol.Parameters)
        {
            var parameterTypeNode = FindNodeBySymbol(parameter.Type);
            if (parameterTypeNode != null)
            {
                CreateRelationship(sourceNode, parameterTypeNode, RelationshipType.Parameter, "parameter type");
            }
        }

        // Override relationships
        if (symbol.IsOverride && symbol.OverriddenMethod != null)
        {
            var overriddenMethodNode = FindNodeBySymbol(symbol.OverriddenMethod);
            if (overriddenMethodNode != null)
            {
                CreateRelationship(sourceNode, overriddenMethodNode, RelationshipType.Overrides, "method override");
            }
        }
    }

    private void DetectMethodCallRelationships(InvocationExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IMethodSymbol calledMethod)
        {
            var calledNode = FindNodeBySymbol(calledMethod);
            if (calledNode != null)
            {
                // Find the containing method of this call
                var containingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (containingMethod != null)
                {
                    var containingMethodSymbol = _semanticModel.GetDeclaredSymbol(containingMethod);
                    var callingNode = FindNodeBySymbol(containingMethodSymbol);
                    
                    if (callingNode != null)
                    {
                        CreateRelationship(callingNode, calledNode, RelationshipType.Calls, "method call");
                    }
                }
            }
        }
    }

    private void DetectMemberAccessRelationships(MemberAccessExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol != null)
        {
            var accessedNode = FindNodeBySymbol(symbolInfo.Symbol);
            if (accessedNode != null)
            {
                // Find the containing method of this access
                var containingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (containingMethod != null)
                {
                    var containingMethodSymbol = _semanticModel.GetDeclaredSymbol(containingMethod);
                    var accessingNode = FindNodeBySymbol(containingMethodSymbol);
                    
                    if (accessingNode != null)
                    {
                        CreateRelationship(accessingNode, accessedNode, RelationshipType.Uses, "member access");
                    }
                }
            }
        }
    }

    private void DetectConstructorCallRelationships(ObjectCreationExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IMethodSymbol constructor)
        {
            var typeNode = FindNodeBySymbol(constructor.ContainingType);
            if (typeNode != null)
            {
                // Find the containing method of this constructor call
                var containingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (containingMethod != null)
                {
                    var containingMethodSymbol = _semanticModel.GetDeclaredSymbol(containingMethod);
                    var callingNode = FindNodeBySymbol(containingMethodSymbol);
                    
                    if (callingNode != null)
                    {
                        CreateRelationship(callingNode, typeNode, RelationshipType.Uses, "object creation");
                    }
                }
            }
        }
    }

    private void DetectVariableTypeRelationships(VariableDeclarationSyntax node)
    {
        var typeInfo = _semanticModel.GetTypeInfo(node.Type);
        if (typeInfo.Type != null)
        {
            var typeNode = FindNodeBySymbol(typeInfo.Type);
            if (typeNode != null)
            {
                // Find the containing method or type
                var containingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (containingMethod != null)
                {
                    var containingMethodSymbol = _semanticModel.GetDeclaredSymbol(containingMethod);
                    var containerNode = FindNodeBySymbol(containingMethodSymbol);
                    
                    if (containerNode != null)
                    {
                        CreateRelationship(containerNode, typeNode, RelationshipType.Uses, "variable declaration");
                    }
                }
            }
        }
    }

    private CodeNode? FindNodeBySymbol(ISymbol? symbol)
    {
        if (symbol == null) return null;
        
        var fullName = symbol.ToDisplayString();
        return _nodesByFullName.GetValueOrDefault(fullName);
    }

    private void CreateRelationship(
        CodeNode sourceNode, 
        CodeNode targetNode, 
        RelationshipType type, 
        string context)
    {
        // Avoid duplicate relationships
        var existingRelationship = Relationships.FirstOrDefault(r =>
            r.SourceNodeId == sourceNode.Id &&
            r.TargetNodeId == targetNode.Id &&
            r.Type == type);

        if (existingRelationship != null)
        {
            existingRelationship.UsageCount++;
            return;
        }

        var relationship = new CodeRelationship
        {
            Id = Guid.NewGuid().ToString(),
            SourceNodeId = sourceNode.Id,
            TargetNodeId = targetNode.Id,
            Type = type,
            Context = context,
            UsageCount = 1,
            CreatedAt = DateTime.UtcNow
        };

        Relationships.Add(relationship);
    }
}