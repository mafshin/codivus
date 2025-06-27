using System.Collections.Generic;

namespace Codivus.Graph.Models
{
    public class GraphSchema
    {
        public const string GRAPH_NAME = "codivus";
        
        // Vertex Labels
        public static class VertexLabels
        {
            public const string Namespace = "namespace";
            public const string Type = "type";
            public const string Method = "method";
            public const string Property = "property";
            public const string Field = "field";
            public const string Parameter = "parameter";
            public const string File = "file";
            public const string Project = "project";
            public const string Assembly = "assembly";
        }

        // Edge Labels
        public static class EdgeLabels
        {
            public const string Contains = "contains";
            public const string Inherits = "inherits";
            public const string Implements = "implements";
            public const string Calls = "calls";
            public const string Uses = "uses";
            public const string References = "references";
            public const string Declares = "declares";
            public const string Overrides = "overrides";
            public const string Returns = "returns";
            public const string HasParameter = "hasParameter";
            public const string Throws = "throws";
            public const string HasAttribute = "hasAttribute";
            public const string HasGenericConstraint = "hasGenericConstraint";
            public const string DependsOn = "dependsOn";
        }

        // Property Keys
        public static class PropertyKeys
        {
            // Common properties
            public const string ExternalId = "externalId"; // Custom ID to avoid conflict with JanusGraph's internal ID
            public const string Name = "name";
            public const string FullName = "fullName";
            public const string DisplayName = "displayName";
            public const string NodeType = "nodeType";
            public const string RepositoryId = "repositoryId";
            public const string ProjectId = "projectId";
            public const string FileId = "fileId";
            public const string StartLine = "startLine";
            public const string EndLine = "endLine";
            public const string Checksum = "checksum";
            public const string CreatedAt = "createdAt";
            public const string UpdatedAt = "updatedAt";

            // Type properties
            public const string TypeKind = "typeKind";
            public const string Accessibility = "accessibility";
            public const string IsAbstract = "isAbstract";
            public const string IsSealed = "isSealed";
            public const string IsStatic = "isStatic";
            public const string IsPartial = "isPartial";
            public const string IsGeneric = "isGeneric";
            public const string GenericParameterCount = "genericParameterCount";

            // Method properties
            public const string ReturnType = "returnType";
            public const string Signature = "signature";
            public const string IsAsync = "isAsync";
            public const string IsOverride = "isOverride";
            public const string IsVirtual = "isVirtual";
            public const string CyclomaticComplexity = "cyclomaticComplexity";
            public const string ParameterCount = "parameterCount";

            // Metrics
            public const string LineCount = "lineCount";
            public const string CommentLineCount = "commentLineCount";
            public const string MaintainabilityIndex = "maintainabilityIndex";
            public const string CouplingCount = "couplingCount";

            // Relationship properties
            public const string UsageCount = "usageCount";
            public const string Context = "context";
            public const string IsImplicit = "isImplicit";
            public const string Strength = "strength";
        }

        // Indexes
        public static class Indexes
        {
            public static readonly Dictionary<string, List<string>> VertexIndexes = new()
            {
                { "byFullName", new List<string> { PropertyKeys.FullName } },
                { "byRepository", new List<string> { PropertyKeys.RepositoryId } },
                { "byProject", new List<string> { PropertyKeys.ProjectId } },
                { "byFile", new List<string> { PropertyKeys.FileId } },
                { "byType", new List<string> { PropertyKeys.NodeType } },
                { "byChecksum", new List<string> { PropertyKeys.Checksum } }
            };

            public static readonly Dictionary<string, List<string>> CompositeIndexes = new()
            {
                { "repositoryAndType", new List<string> { PropertyKeys.RepositoryId, PropertyKeys.NodeType } },
                { "projectAndType", new List<string> { PropertyKeys.ProjectId, PropertyKeys.NodeType } },
                { "fileAndLine", new List<string> { PropertyKeys.FileId, PropertyKeys.StartLine } }
            };
        }
    }
}