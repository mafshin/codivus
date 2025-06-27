using System;
using System.Collections.Generic;

namespace Codivus.Graph.Models
{
    public enum RelationshipType
    {
        Contains,
        Inherits,
        Implements,
        Calls,
        Uses,
        References,
        Declares,
        Overrides,
        Returns,
        Parameter,
        Throws,
        Attribute,
        GenericConstraint,
        Dependency
    }

    public class CodeRelationship
    {
        public string Id { get; set; }
        public string SourceNodeId { get; set; }
        public string TargetNodeId { get; set; }
        public RelationshipType Type { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UsageCount { get; set; }
        public string Context { get; set; }
        public int? StartLine { get; set; }
        public int? EndLine { get; set; }
        public int? StartColumn { get; set; }
        public int? EndColumn { get; set; }
        public bool IsImplicit { get; set; }
        public double? Strength { get; set; } // For weighted relationships
    }
}