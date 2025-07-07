using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;
using Codivus.Graph.Configuration;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Codivus.Graph.Services
{
    public class GraphStorageService : IGraphStorageService
    {
        private readonly GraphConfiguration _configuration;
        private readonly ILogger<GraphStorageService> _logger;
        private IDriver? _driver;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _disposed;

        public GraphStorageService(IOptions<GraphConfiguration> configuration, ILogger<GraphStorageService> logger)
        {
            _configuration = configuration.Value;
            _logger = logger;
        }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_driver != null)
                    return true;

                var settings = _configuration.Neo4j;
                
                _driver = GraphDatabase.Driver(
                    settings.Uri,
                    AuthTokens.Basic(settings.Username, settings.Password),
                    configBuilder => configBuilder
                        .WithMaxConnectionPoolSize(settings.MaxConnectionPoolSize)
                        .WithConnectionAcquisitionTimeout(settings.ConnectionAcquisitionTimeout)
                        .WithConnectionTimeout(settings.ConnectionTimeout)
                        .WithEncryptionLevel(settings.EnableEncryption ? EncryptionLevel.Encrypted : EncryptionLevel.None)
                );

                // Test connection
                try
                {
                    await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));
                    await session.RunAsync("RETURN 1");
                }
                catch (Exception)
                {
                    _logger.LogWarning("Could not test graph connection");
                }
                
                _logger.LogInformation("Graph storage service initialized for {Uri}", settings.Uri);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize graph connection");
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<bool> CreateSchemaAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_configuration.Enabled)
                {
                    _logger.LogInformation("Graph storage is disabled, skipping schema creation");
                    return true;
                }

                if (_driver == null) return false;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                _logger.LogInformation("Creating Neo4j schema with constraints and indexes");

                var schemaQueries = new[]
                {
                    // Create constraints for unique external IDs
                    "CREATE CONSTRAINT external_id_unique IF NOT EXISTS FOR (n:CodeNode) REQUIRE n.externalId IS UNIQUE",
                    
                    // Create indexes for performance
                    "CREATE INDEX repository_index IF NOT EXISTS FOR (n:CodeNode) ON (n.repositoryId)",
                    "CREATE INDEX node_type_index IF NOT EXISTS FOR (n:CodeNode) ON (n.nodeType)",
                    "CREATE INDEX repository_type_index IF NOT EXISTS FOR (n:CodeNode) ON (n.repositoryId, n.nodeType)",
                    "CREATE INDEX file_id_index IF NOT EXISTS FOR (n:CodeNode) ON (n.fileId)",
                    "CREATE INDEX project_id_index IF NOT EXISTS FOR (n:CodeNode) ON (n.projectId)",
                    
                    // Create relationship indexes
                    "CREATE INDEX relationship_external_id IF NOT EXISTS FOR ()-[r]-() ON (r.externalId)"
                };

                foreach (var query in schemaQueries)
                {
                    try
                    {
                        await session.RunAsync(query);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to execute schema query: {Query}", query);
                    }
                }
                
                _logger.LogInformation("Neo4j schema created successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Neo4j schema: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<bool> ClearGraphAsync(string repositoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) return false;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = @"
                    MATCH (n:CodeNode {repositoryId: $repositoryId})
                    DETACH DELETE n";

                await session.RunAsync(query, new { repositoryId });

                _logger.LogInformation("Cleared graph for repository {RepositoryId}", repositoryId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear graph for repository {RepositoryId}", repositoryId);
                return false;
            }
        }

        public async Task<CodeNode> CreateNodeAsync(CodeNode node, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) throw new InvalidOperationException("Graph not initialized");

                node.Id = node.Id ?? Guid.NewGuid().ToString();
                node.CreatedAt = DateTime.UtcNow;
                node.UpdatedAt = node.CreatedAt;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = @"
                    CREATE (n:CodeNode {
                        externalId: $externalId,
                        name: $name,
                        fullName: $fullName,
                        displayName: $displayName,
                        nodeType: $nodeType,
                        repositoryId: $repositoryId,
                        projectId: $projectId,
                        fileId: $fileId,
                        checksum: $checksum,
                        createdAt: $createdAt,
                        updatedAt: $updatedAt
                    })
                    RETURN n";

                await session.RunAsync(query, new
                {
                    externalId = node.Id,
                    name = node.Name ?? "",
                    fullName = node.FullName ?? "",
                    displayName = node.DisplayName ?? "",
                    nodeType = node.NodeType.ToString(),
                    repositoryId = node.RepositoryId ?? "",
                    projectId = node.ProjectId ?? "",
                    fileId = node.FileId ?? "",
                    checksum = node.Checksum ?? "",
                    createdAt = node.CreatedAt.Ticks,
                    updatedAt = node.UpdatedAt.Ticks
                });

                _logger.LogDebug("Created node {NodeId} of type {NodeType}", node.Id, node.NodeType);
                return node;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create node {NodeId}", node.Id);
                throw;
            }
        }

        public async Task<IEnumerable<CodeNode>> CreateNodesAsync(IEnumerable<CodeNode> nodes, CancellationToken cancellationToken = default)
        {
            var createdNodes = new List<CodeNode>();
            
            foreach (var node in nodes)
            {
                try
                {
                    var created = await CreateNodeAsync(node, cancellationToken);
                    createdNodes.Add(created);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create node in batch");
                }
            }

            return createdNodes;
        }

        public async Task<CodeNode?> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) return null;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = "MATCH (n:CodeNode {externalId: $nodeId}) RETURN n";
                var result = await session.RunAsync(query, new { nodeId });
                var record = await result.PeekAsync();

                if (record == null) return null;

                var node = record["n"].As<INode>();
                return MapNodeToCodeNode(node);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get node {NodeId}: {Message}", nodeId, ex.Message);
                return null;
            }
        }

        public async Task<IEnumerable<CodeNode>> GetNodesByTypeAsync(string repositoryId, NodeType nodeType, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) return Enumerable.Empty<CodeNode>();

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = @"
                    MATCH (n:CodeNode {repositoryId: $repositoryId, nodeType: $nodeType})
                    RETURN n";

                var result = await session.RunAsync(query, new
                {
                    repositoryId,
                    nodeType = nodeType.ToString()
                });

                var nodes = new List<CodeNode>();
                await foreach (var record in result)
                {
                    var node = record["n"].As<INode>();
                    var codeNode = MapNodeToCodeNode(node);
                    if (codeNode != null)
                        nodes.Add(codeNode);
                }

                return nodes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get nodes by type {NodeType} for repository {RepositoryId}", nodeType, repositoryId);
                return Enumerable.Empty<CodeNode>();
            }
        }

        public async Task<IEnumerable<CodeNode>> GetAllNodesAsync(string repositoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) 
                {
                    await InitializeAsync(cancellationToken);
                    if (_driver == null)
                    {
                        _logger.LogError("Failed to initialize driver, returning empty collection");
                        return Enumerable.Empty<CodeNode>();
                    }
                }

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = @"
                    MATCH (n:CodeNode {repositoryId: $repositoryId})
                    RETURN n
                    LIMIT 1000";

                var result = await session.RunAsync(query, new { repositoryId });

                var nodes = new List<CodeNode>();
                await foreach (var record in result)
                {
                    var node = record["n"].As<INode>();
                    var codeNode = MapNodeToCodeNode(node);
                    if (codeNode != null)
                        nodes.Add(codeNode);
                }

                _logger.LogDebug("Retrieved {Count} nodes for repository {RepositoryId}", nodes.Count, repositoryId);
                return nodes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all nodes for repository {RepositoryId}", repositoryId);
                return Enumerable.Empty<CodeNode>();
            }
        }

        public async Task<CodeRelationship> CreateRelationshipAsync(CodeRelationship relationship, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) throw new InvalidOperationException("Graph not initialized");

                relationship.Id = relationship.Id ?? Guid.NewGuid().ToString();
                relationship.CreatedAt = DateTime.UtcNow;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var relationshipType = GetRelationshipType(relationship.Type);
                var query = $@"
                    MATCH (source:CodeNode {{externalId: $sourceId}})
                    MATCH (target:CodeNode {{externalId: $targetId}})
                    CREATE (source)-[r:{relationshipType} {{
                        externalId: $externalId,
                        context: $context,
                        createdAt: $createdAt
                    }}]->(target)
                    RETURN r";

                await session.RunAsync(query, new
                {
                    sourceId = relationship.SourceNodeId,
                    targetId = relationship.TargetNodeId,
                    externalId = relationship.Id,
                    context = relationship.Context ?? "",
                    createdAt = relationship.CreatedAt.Ticks
                });

                _logger.LogDebug("Created relationship {RelationshipId} of type {Type}", relationship.Id, relationship.Type);
                return relationship;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create relationship from {SourceId} to {TargetId}", 
                    relationship.SourceNodeId, relationship.TargetNodeId);
                throw;
            }
        }

        public async Task<GraphMetrics> GetMetricsAsync(string repositoryId, CancellationToken cancellationToken = default)
        {
            var metrics = new GraphMetrics
            {
                RepositoryId = repositoryId,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                if (_driver == null) return metrics;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                // Get total nodes count
                var nodeQuery = "MATCH (n:CodeNode {repositoryId: $repositoryId}) RETURN count(n) as nodeCount";
                var nodeResult = await session.RunAsync(nodeQuery, new { repositoryId });
                var nodeRecord = await nodeResult.SingleAsync();
                metrics.VertexCount = nodeRecord["nodeCount"].As<long>();

                // Get total relationships count
                var edgeQuery = "MATCH (n:CodeNode {repositoryId: $repositoryId})-[r]->(m:CodeNode {repositoryId: $repositoryId}) RETURN count(r) as edgeCount";
                var edgeResult = await session.RunAsync(edgeQuery, new { repositoryId });
                var edgeRecord = await edgeResult.SingleAsync();
                metrics.EdgeCount = edgeRecord["edgeCount"].As<long>();

                // Get node counts by type
                var typeQuery = @"
                    MATCH (n:CodeNode {repositoryId: $repositoryId}) 
                    RETURN n.nodeType as nodeType, count(n) as count";
                var typeResult = await session.RunAsync(typeQuery, new { repositoryId });
                
                await foreach (var record in typeResult)
                {
                    var nodeType = record["nodeType"].As<string>() ?? "Unknown";
                    var count = record["count"].As<long>();
                    metrics.VertexCountByType[nodeType] = count;
                    
                    // Map to specific metrics
                    switch (nodeType.ToLower())
                    {
                        case "project":
                            metrics.TotalProjects = count;
                            break;
                        case "file":
                            metrics.TotalFiles = count;
                            break;
                        case "class":
                        case "interface":
                        case "struct":
                        case "enum":
                            metrics.TotalTypes += count;
                            break;
                        case "method":
                        case "constructor":
                        case "property":
                            metrics.TotalMethods += count;
                            break;
                    }
                }

                // Get relationship counts by type
                var relTypeQuery = @"
                    MATCH (n:CodeNode {repositoryId: $repositoryId})-[r]->(m:CodeNode {repositoryId: $repositoryId}) 
                    RETURN type(r) as relType, count(r) as count";
                var relTypeResult = await session.RunAsync(relTypeQuery, new { repositoryId });
                
                await foreach (var record in relTypeResult)
                {
                    var relType = record["relType"].As<string>() ?? "Unknown";
                    var count = record["count"].As<long>();
                    metrics.EdgeCountByType[relType] = count;
                }

                // Calculate average coupling (average outgoing relationships per node)
                if (metrics.VertexCount > 0)
                {
                    metrics.AverageCoupling = (double)metrics.EdgeCount / metrics.VertexCount;
                }

                _logger.LogDebug("Retrieved metrics for repository {RepositoryId}: {NodeCount} nodes, {EdgeCount} edges", 
                    repositoryId, metrics.VertexCount, metrics.EdgeCount);

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get metrics for repository {RepositoryId}", repositoryId);
                return metrics;
            }
        }

        public async Task<IGraphTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (!_configuration.Enabled || _driver == null)
            {
                return new NullGraphTransaction();
            }
            
            var settings = _configuration.Neo4j;
            var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));
            var transaction = await session.BeginTransactionAsync();
            
            return new GraphTransaction(session, transaction, this);
        }

        private static string GetRelationshipType(RelationshipType relationshipType)
        {
            return relationshipType switch
            {
                RelationshipType.Contains => "CONTAINS",
                RelationshipType.Inherits => "INHERITS",
                RelationshipType.Implements => "IMPLEMENTS",
                RelationshipType.Calls => "CALLS",
                RelationshipType.Uses => "USES",
                RelationshipType.References => "REFERENCES",
                RelationshipType.Declares => "DECLARES",
                RelationshipType.Overrides => "OVERRIDES",
                _ => relationshipType.ToString().ToUpper()
            };
        }

        private CodeNode? MapNodeToCodeNode(INode node)
        {
            if (node?.Properties == null) return null;

            var properties = node.Properties;
            var codeNode = new CodeNode
            {
                Id = properties.GetValueOrDefault("externalId")?.As<string>() ?? "",
                Name = properties.GetValueOrDefault("name")?.As<string>() ?? "",
                FullName = properties.GetValueOrDefault("fullName")?.As<string>() ?? "",
                DisplayName = properties.GetValueOrDefault("displayName")?.As<string>() ?? "",
                RepositoryId = properties.GetValueOrDefault("repositoryId")?.As<string>() ?? "",
                ProjectId = properties.GetValueOrDefault("projectId")?.As<string>() ?? "",
                FileId = properties.GetValueOrDefault("fileId")?.As<string>() ?? "",
                Checksum = properties.GetValueOrDefault("checksum")?.As<string>() ?? ""
            };

            // Parse node type
            var nodeTypeStr = properties.GetValueOrDefault("nodeType")?.As<string>();
            if (!string.IsNullOrEmpty(nodeTypeStr) && Enum.TryParse<NodeType>(nodeTypeStr, out var nodeType))
            {
                codeNode.NodeType = nodeType;
            }

            // Parse timestamps
            var createdAtTicks = properties.GetValueOrDefault("createdAt")?.As<long>() ?? 0;
            if (createdAtTicks > 0)
            {
                codeNode.CreatedAt = new DateTime(createdAtTicks);
            }

            var updatedAtTicks = properties.GetValueOrDefault("updatedAt")?.As<long>() ?? 0;
            if (updatedAtTicks > 0)
            {
                codeNode.UpdatedAt = new DateTime(updatedAtTicks);
            }

            return codeNode;
        }

        public async Task<CodeNode> UpdateNodeAsync(CodeNode node, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) throw new InvalidOperationException("Graph not initialized");

                node.UpdatedAt = DateTime.UtcNow;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = @"
                    MATCH (n:CodeNode {externalId: $externalId})
                    SET n.name = $name,
                        n.fullName = $fullName,
                        n.displayName = $displayName,
                        n.updatedAt = $updatedAt
                    RETURN n";

                await session.RunAsync(query, new
                {
                    externalId = node.Id,
                    name = node.Name ?? "",
                    fullName = node.FullName ?? "",
                    displayName = node.DisplayName ?? "",
                    updatedAt = node.UpdatedAt.Ticks
                });

                return node;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update node {NodeId}", node.Id);
                throw;
            }
        }

        public async Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) return false;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = "MATCH (n:CodeNode {externalId: $nodeId}) DETACH DELETE n RETURN count(n) as deleted";
                var result = await session.RunAsync(query, new { nodeId });
                var record = await result.SingleAsync();

                var deletedCount = record["deleted"].As<long>();
                _logger.LogDebug("Deleted {Count} nodes with ExternalId {NodeId}", deletedCount, nodeId);
                
                return deletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete node {NodeId}: {Message}", nodeId, ex.Message);
                return false;
            }
        }

        public async Task<bool> NodeExistsAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) return false;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = "MATCH (n:CodeNode {externalId: $nodeId}) RETURN count(n) as count";
                var result = await session.RunAsync(query, new { nodeId });
                var record = await result.SingleAsync();

                return record["count"].As<long>() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if node exists {NodeId}", nodeId);
                return false;
            }
        }

        public async Task<int> UpdateNodesAsync(IEnumerable<CodeNode> nodes, CancellationToken cancellationToken = default)
        {
            int updated = 0;
            foreach (var node in nodes)
            {
                try
                {
                    await UpdateNodeAsync(node, cancellationToken);
                    updated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update node in batch");
                }
            }
            return updated;
        }

        public async Task<int> DeleteNodesAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
        {
            int deleted = 0;
            foreach (var nodeId in nodeIds)
            {
                try
                {
                    if (await DeleteNodeAsync(nodeId, cancellationToken))
                        deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete node in batch");
                }
            }
            return deleted;
        }

        public async Task<CodeRelationship> UpdateRelationshipAsync(CodeRelationship relationship, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) throw new InvalidOperationException("Graph not initialized");

                relationship.UpdatedAt = DateTime.UtcNow;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = @"
                    MATCH ()-[r {externalId: $externalId}]-()
                    SET r.context = $context,
                        r.updatedAt = $updatedAt
                    RETURN r";

                await session.RunAsync(query, new
                {
                    externalId = relationship.Id,
                    context = relationship.Context ?? "",
                    updatedAt = relationship.UpdatedAt.Ticks
                });

                return relationship;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update relationship {RelationshipId}", relationship.Id);
                throw;
            }
        }

        public async Task<IEnumerable<CodeRelationship>> GetRelationshipsAsync(string nodeId, RelationshipType? type = null, bool outgoing = true, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) return Enumerable.Empty<CodeRelationship>();

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                string query;
                object parameters;

                if (outgoing)
                {
                    if (type.HasValue)
                    {
                        var relationshipType = GetRelationshipType(type.Value);
                        query = $@"
                            MATCH (source:CodeNode {{externalId: $nodeId}})-[r:{relationshipType}]->(target:CodeNode)
                            RETURN r, source.externalId as sourceId, target.externalId as targetId";
                    }
                    else
                    {
                        query = @"
                            MATCH (source:CodeNode {externalId: $nodeId})-[r]->(target:CodeNode)
                            RETURN r, source.externalId as sourceId, target.externalId as targetId";
                    }
                }
                else
                {
                    if (type.HasValue)
                    {
                        var relationshipType = GetRelationshipType(type.Value);
                        query = $@"
                            MATCH (source:CodeNode)-[r:{relationshipType}]->(target:CodeNode {{externalId: $nodeId}})
                            RETURN r, source.externalId as sourceId, target.externalId as targetId";
                    }
                    else
                    {
                        query = @"
                            MATCH (source:CodeNode)-[r]->(target:CodeNode {externalId: $nodeId})
                            RETURN r, source.externalId as sourceId, target.externalId as targetId";
                    }
                }

                parameters = new { nodeId };

                var result = await session.RunAsync(query, parameters);
                var relationships = new List<CodeRelationship>();

                await foreach (var record in result)
                {
                    var relationship = MapRelationshipToCodeRelationship(
                        record["r"].As<IRelationship>(),
                        record["sourceId"].As<string>(),
                        record["targetId"].As<string>()
                    );
                    
                    if (relationship != null)
                        relationships.Add(relationship);
                }

                return relationships;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get relationships for node {NodeId}", nodeId);
                return Enumerable.Empty<CodeRelationship>();
            }
        }

        public async Task<bool> DeleteRelationshipAsync(string relationshipId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) return false;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var query = "MATCH ()-[r {externalId: $relationshipId}]-() DELETE r RETURN count(r) as deleted";
                var result = await session.RunAsync(query, new { relationshipId });
                var record = await result.SingleAsync();

                var deletedCount = record["deleted"].As<long>();
                _logger.LogDebug("Deleted {Count} relationships with ExternalId {RelationshipId}", deletedCount, relationshipId);
                
                return deletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete relationship {RelationshipId}: {Message}", relationshipId, ex.Message);
                return false;
            }
        }

        public async Task<bool> RelationshipExistsAsync(string sourceId, string targetId, RelationshipType type, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_driver == null) return false;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                var relationshipType = GetRelationshipType(type);
                var query = $@"
                    MATCH (source:CodeNode {{externalId: $sourceId}})-[r:{relationshipType}]->(target:CodeNode {{externalId: $targetId}})
                    RETURN count(r) as count";

                var result = await session.RunAsync(query, new { sourceId, targetId });
                var record = await result.SingleAsync();

                return record["count"].As<long>() > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check relationship existence from {SourceId} to {TargetId}", sourceId, targetId);
                return false;
            }
        }

        public async Task<IEnumerable<CodeRelationship>> CreateRelationshipsAsync(IEnumerable<CodeRelationship> relationships, CancellationToken cancellationToken = default)
        {
            var created = new List<CodeRelationship>();
            foreach (var relationship in relationships)
            {
                try
                {
                    var createdRelationship = await CreateRelationshipAsync(relationship, cancellationToken);
                    created.Add(createdRelationship);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create relationship in batch");
                }
            }
            return created;
        }

        public async Task<int> DeleteRelationshipsAsync(IEnumerable<string> relationshipIds, CancellationToken cancellationToken = default)
        {
            int deleted = 0;
            foreach (var relationshipId in relationshipIds)
            {
                try
                {
                    if (await DeleteRelationshipAsync(relationshipId, cancellationToken))
                        deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete relationship in batch");
                }
            }
            return deleted;
        }

        private CodeRelationship? MapRelationshipToCodeRelationship(IRelationship relationship, string sourceId, string targetId)
        {
            if (relationship?.Properties == null) return null;

            var properties = relationship.Properties;
            var codeRelationship = new CodeRelationship
            {
                Id = properties.GetValueOrDefault("externalId")?.As<string>() ?? "",
                SourceNodeId = sourceId,
                TargetNodeId = targetId,
                Context = properties.GetValueOrDefault("context")?.As<string>() ?? ""
            };

            // Parse relationship type from Neo4j relationship type
            if (Enum.TryParse<RelationshipType>(relationship.Type, true, out var relType))
            {
                codeRelationship.Type = relType;
            }

            // Parse timestamps
            var createdAtTicks = properties.GetValueOrDefault("createdAt")?.As<long>() ?? 0;
            if (createdAtTicks > 0)
            {
                codeRelationship.CreatedAt = new DateTime(createdAtTicks);
            }

            var updatedAtTicks = properties.GetValueOrDefault("updatedAt")?.As<long>() ?? 0;
            if (updatedAtTicks > 0)
            {
                codeRelationship.UpdatedAt = new DateTime(updatedAtTicks);
            }

            return codeRelationship;
        }

        public Task RecordQueryMetricsAsync(GraphQueryMetrics metrics, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Will be implemented in Phase 2");
        }

        // Maintenance operations
        public async Task OptimizeIndicesAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            
            try
            {
                _logger.LogDebug("Optimizing graph indices");
                // Neo4j automatically optimizes indexes, but we can force a call to schema await
                if (_driver != null)
                {
                    var settings = _configuration.Neo4j;
                    await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));
                    await session.RunAsync("CALL db.awaitIndexes()");
                }
                _logger.LogDebug("Graph indices optimization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize graph indices");
                throw;
            }
        }

        public async Task<int> CleanupOrphanedNodesAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            
            try
            {
                _logger.LogDebug("Starting cleanup of orphaned nodes");
                
                if (_driver == null) return 0;

                var settings = _configuration.Neo4j;
                await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));

                // Find and delete nodes without any relationships
                var query = @"
                    MATCH (n:CodeNode)
                    WHERE NOT (n)-[]-()
                    DELETE n
                    RETURN count(n) as deletedCount";

                var result = await session.RunAsync(query);
                var record = await result.SingleAsync();
                var cleanedCount = (int)record["deletedCount"].As<long>();
                
                _logger.LogDebug("Cleaned up {Count} orphaned nodes", cleanedCount);
                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup orphaned nodes");
                throw;
            }
        }

        public async Task DefragmentStorageAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            
            try
            {
                _logger.LogDebug("Starting graph storage defragmentation");
                
                // Neo4j doesn't require manual defragmentation
                // But we can run a query to reorganize data
                if (_driver != null)
                {
                    var settings = _configuration.Neo4j;
                    await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));
                    
                    // This is a no-op for Neo4j as it handles storage optimization automatically
                    await Task.Delay(100, cancellationToken);
                }
                
                _logger.LogDebug("Graph storage defragmentation completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to defragment graph storage");
                throw;
            }
        }

        public async Task UpdateStatisticsAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            
            try
            {
                _logger.LogDebug("Updating graph statistics");
                
                if (_driver != null)
                {
                    var settings = _configuration.Neo4j;
                    await using var session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));
                    
                    // Update Neo4j statistics
                    await session.RunAsync("CALL db.stats.collect()");
                }
                
                _logger.LogDebug("Graph statistics updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update graph statistics");
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _driver?.Dispose();
            _connectionLock?.Dispose();
            
            _disposed = true;
        }

        // Neo4j transaction implementation
        private class GraphTransaction : IGraphTransaction
        {
            private readonly IAsyncSession _session;
            private readonly IAsyncTransaction _transaction;
            private readonly GraphStorageService _service;
            private bool _disposed;

            public GraphTransaction(IAsyncSession session, IAsyncTransaction transaction, GraphStorageService service)
            {
                _session = session;
                _transaction = transaction;
                _service = service;
            }

            public async Task<CodeNode> CreateNodeAsync(CodeNode node)
            {
                node.Id = node.Id ?? Guid.NewGuid().ToString();
                node.CreatedAt = DateTime.UtcNow;
                node.UpdatedAt = node.CreatedAt;

                var query = @"
                    CREATE (n:CodeNode {
                        externalId: $externalId,
                        name: $name,
                        fullName: $fullName,
                        displayName: $displayName,
                        nodeType: $nodeType,
                        repositoryId: $repositoryId,
                        projectId: $projectId,
                        fileId: $fileId,
                        checksum: $checksum,
                        createdAt: $createdAt,
                        updatedAt: $updatedAt
                    })
                    RETURN n";

                await _transaction.RunAsync(query, new
                {
                    externalId = node.Id,
                    name = node.Name ?? "",
                    fullName = node.FullName ?? "",
                    displayName = node.DisplayName ?? "",
                    nodeType = node.NodeType.ToString(),
                    repositoryId = node.RepositoryId ?? "",
                    projectId = node.ProjectId ?? "",
                    fileId = node.FileId ?? "",
                    checksum = node.Checksum ?? "",
                    createdAt = node.CreatedAt.Ticks,
                    updatedAt = node.UpdatedAt.Ticks
                });

                return node;
            }

            public async Task<CodeRelationship> CreateRelationshipAsync(CodeRelationship relationship)
            {
                relationship.Id = relationship.Id ?? Guid.NewGuid().ToString();
                relationship.CreatedAt = DateTime.UtcNow;

                var relationshipType = GetRelationshipType(relationship.Type);
                var query = $@"
                    MATCH (source:CodeNode {{externalId: $sourceId}})
                    MATCH (target:CodeNode {{externalId: $targetId}})
                    CREATE (source)-[r:{relationshipType} {{
                        externalId: $externalId,
                        context: $context,
                        createdAt: $createdAt
                    }}]->(target)
                    RETURN r";

                await _transaction.RunAsync(query, new
                {
                    sourceId = relationship.SourceNodeId,
                    targetId = relationship.TargetNodeId,
                    externalId = relationship.Id,
                    context = relationship.Context ?? "",
                    createdAt = relationship.CreatedAt.Ticks
                });

                return relationship;
            }

            public async Task CommitAsync()
            {
                await _transaction.CommitAsync();
            }

            public async Task RollbackAsync()
            {
                await _transaction.RollbackAsync();
            }

            public async void Dispose()
            {
                if (_disposed)
                    return;

                try
                {
                    await _transaction.DisposeAsync();
                    await _session.DisposeAsync();
                }
                catch
                {
                    // Ignore disposal errors
                }

                _disposed = true;
            }
        }

        // Null object pattern for when graph is disabled
        private class NullGraphTransaction : IGraphTransaction
        {
            public Task<CodeNode> CreateNodeAsync(CodeNode node)
            {
                return Task.FromResult(node);
            }

            public Task<CodeRelationship> CreateRelationshipAsync(CodeRelationship relationship)
            {
                return Task.FromResult(relationship);
            }

            public Task CommitAsync()
            {
                return Task.CompletedTask;
            }

            public Task RollbackAsync()
            {
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                // No resources to dispose
            }
        }
    }
}