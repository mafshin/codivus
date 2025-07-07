using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    public interface IGraphStorageService : IDisposable
    {
        Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
        Task<bool> CreateSchemaAsync(CancellationToken cancellationToken = default);
        Task<bool> ClearGraphAsync(string repositoryId, CancellationToken cancellationToken = default);
        
        // Node operations
        Task<CodeNode> CreateNodeAsync(CodeNode node, CancellationToken cancellationToken = default);
        Task<CodeNode> UpdateNodeAsync(CodeNode node, CancellationToken cancellationToken = default);
        Task<CodeNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default);
        Task<IEnumerable<CodeNode>> GetNodesByTypeAsync(string repositoryId, NodeType nodeType, CancellationToken cancellationToken = default);
        Task<IEnumerable<CodeNode>> GetAllNodesAsync(string repositoryId, CancellationToken cancellationToken = default);
        Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default);
        Task<bool> NodeExistsAsync(string nodeId, CancellationToken cancellationToken = default);
        
        // Batch node operations
        Task<IEnumerable<CodeNode>> CreateNodesAsync(IEnumerable<CodeNode> nodes, CancellationToken cancellationToken = default);
        Task<int> UpdateNodesAsync(IEnumerable<CodeNode> nodes, CancellationToken cancellationToken = default);
        Task<int> DeleteNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default);
        
        // Relationship operations
        Task<CodeRelationship> CreateRelationshipAsync(CodeRelationship relationship, CancellationToken cancellationToken = default);
        Task<CodeRelationship> UpdateRelationshipAsync(CodeRelationship relationship, CancellationToken cancellationToken = default);
        Task<IEnumerable<CodeRelationship>> GetRelationshipsAsync(string nodeId, RelationshipType? type = null, bool outgoing = true, CancellationToken cancellationToken = default);
        Task<bool> DeleteRelationshipAsync(string relationshipId, CancellationToken cancellationToken = default);
        Task<bool> RelationshipExistsAsync(string sourceId, string targetId, RelationshipType type, CancellationToken cancellationToken = default);
        
        // Batch relationship operations
        Task<IEnumerable<CodeRelationship>> CreateRelationshipsAsync(IEnumerable<CodeRelationship> relationships, CancellationToken cancellationToken = default);
        Task<int> DeleteRelationshipsAsync(IEnumerable<string> relationshipIds, CancellationToken cancellationToken = default);
        
        // Metrics
        Task<GraphMetrics> GetMetricsAsync(string repositoryId, CancellationToken cancellationToken = default);
        Task RecordQueryMetricsAsync(GraphQueryMetrics metrics, CancellationToken cancellationToken = default);
        
        // Transactions
        Task<IGraphTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        // Maintenance operations
        Task OptimizeIndicesAsync(CancellationToken cancellationToken = default);
        Task<int> CleanupOrphanedNodesAsync(CancellationToken cancellationToken = default);
        Task DefragmentStorageAsync(CancellationToken cancellationToken = default);
        Task UpdateStatisticsAsync(CancellationToken cancellationToken = default);
    }

    public interface IGraphTransaction : IDisposable
    {
        Task<CodeNode> CreateNodeAsync(CodeNode node);
        Task<CodeRelationship> CreateRelationshipAsync(CodeRelationship relationship);
        Task CommitAsync();
        Task RollbackAsync();
    }
}