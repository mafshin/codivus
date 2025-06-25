using System;
using System.Collections.Generic;

namespace Codivus.Graph.Models
{
    public enum NodeType
    {
        Namespace,
        Type,
        Method,
        Property,
        Field,
        Parameter,
        File,
        Project,
        Assembly
    }

    public enum AccessibilityLevel
    {
        Private,
        Protected,
        Internal,
        Public,
        ProtectedInternal,
        PrivateProtected
    }

    public enum TypeKind
    {
        Class,
        Interface,
        Struct,
        Enum,
        Delegate
    }

    public class CodeNode
    {
        public string Id { get; set; }
        public NodeType NodeType { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string DisplayName { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string RepositoryId { get; set; }
        public string ProjectId { get; set; }
        public string FileId { get; set; }
        public int? StartLine { get; set; }
        public int? EndLine { get; set; }
        public string Checksum { get; set; }

        // Type-specific properties
        public TypeKind? TypeKind { get; set; }
        public AccessibilityLevel? Accessibility { get; set; }
        public bool? IsAbstract { get; set; }
        public bool? IsSealed { get; set; }
        public bool? IsStatic { get; set; }
        public bool? IsPartial { get; set; }
        public bool? IsGeneric { get; set; }
        public int? GenericParameterCount { get; set; }

        // Method-specific properties
        public string ReturnType { get; set; }
        public string Signature { get; set; }
        public bool? IsAsync { get; set; }
        public bool? IsOverride { get; set; }
        public bool? IsVirtual { get; set; }
        public int? CyclomaticComplexity { get; set; }
        public int? ParameterCount { get; set; }

        // Metrics
        public int? LineCount { get; set; }
        public int? CommentLineCount { get; set; }
        public double? MaintainabilityIndex { get; set; }
        public int? CouplingCount { get; set; }
    }
}