# JanusGraph to Neo4j Migration Plan

## Executive Summary

This document outlines a comprehensive migration plan to replace the existing JanusGraph implementation in Codivus with Neo4j. The migration will maintain all current functionality while improving performance, simplifying configuration, and reducing complexity.

**Definition of Done:**
- ✅ No occurrences of "Janus" in the codebase after migration
- ✅ Fully working CLI and User Interface graph functionality
- ✅ All tests passing
- ✅ Neo4j integration tests assume Neo4j server running on localhost:7474 (HTTP) and localhost:7687 (Bolt)

## Current State Analysis

### JanusGraph Implementation Overview

The current Codivus codebase has a comprehensive JanusGraph integration spanning **21 files** across the following components:

#### Core Implementation Files (8 files)
1. **GraphStorageService.cs** - Main graph operations service (1,200 lines)
2. **JanusGraphSerializers.cs** - Custom serialization for JanusGraph-specific types
3. **GraphConfiguration.cs** - Configuration models and settings
4. **GraphScanningConfiguration.cs** - Scanning-specific configuration
5. **GraphSchema.cs** - Schema definitions and property keys
6. **StatusCommandService.cs** - Health check functionality
7. **ConfigurationService.cs** - Configuration management
8. **GraphSettings.vue** - Frontend configuration component

#### Test Infrastructure (7 files)
1. **JanusGraphIntegrationTests.cs** - Comprehensive integration test suite
2. **JanusGraphTestHelper.cs** - Test utilities and connectivity helpers
3. **JanusGraphTestContainer.cs** - Docker test container management
4. **GraphConfigurationTests.cs** - Configuration testing
5. **GraphStorageServiceTests.cs** - Service unit tests
6. **GraphStorageMaintenanceTests.cs** - Maintenance operation tests
7. **BasicIntegrationTests.cs** - Basic integration scenarios

#### Configuration Files (2 files)
1. **appsettings.json** (CLI) - Application configuration
2. **appsettings.json** (API) - API configuration

#### Documentation Files (4 files)
1. **README.md** - Project documentation
2. **plan.md** - Implementation plan
3. **codivus-presentation.html** - Presentation documentation
4. **graph.js** - Frontend store configuration

### Current Graph Operations

The existing JanusGraph implementation supports:

#### Node Operations
- **CRUD Operations**: Create, Read, Update, Delete nodes
- **Batch Operations**: Bulk create, update, and delete
- **Node Types**: Namespace, Type, Method, Property, Field, Parameter, File, Project, Assembly
- **Node Properties**: ExternalId, Name, FullName, DisplayName, NodeType, RepositoryId, ProjectId, FileId, Checksum, CreatedAt, UpdatedAt

#### Relationship Operations
- **CRUD Operations**: Create, Read, Update, Delete relationships
- **Relationship Types**: Contains, Inherits, Implements, Calls, Uses, References, Declares, Overrides
- **Relationship Properties**: ExternalId, Context, CreatedAt, UpdatedAt
- **Traversal Operations**: Outgoing/incoming relationship queries

#### Schema Management
- **Dynamic Schema Creation**: Property keys, vertex labels, edge labels
- **Index Management**: Composite indexes for performance
- **Schema Validation**: Property existence checks

#### Advanced Features
- **Transaction Support**: Basic transaction implementation
- **Metrics Collection**: Graph statistics and performance monitoring
- **Maintenance Operations**: Index optimization, orphan cleanup, defragmentation
- **Health Checks**: Connection validation and status reporting

### Dependencies and Technologies

#### Current Stack
- **Graph Database**: JanusGraph 1.0.0
- **Client Library**: Gremlin.Net (Apache TinkerPop)
- **Serialization**: GraphSON3 with custom deserializers
- **Connection**: WebSocket-based Gremlin client
- **Testing**: Docker containers for integration tests

## Migration Strategy

### Phase 1: Neo4j Driver Integration (Week 1-2)

#### 1.1 Replace Core Dependencies
**Target Files:**
- `src/Codivus.Graph/Codivus.Graph.csproj`

**Actions:**
- Remove `Gremlin.Net` package
- Add `Neo4j.Driver` package (latest stable version)
- Remove custom JanusGraph serializers dependency

#### 1.2 Configuration Migration
**Target Files:**
- `src/Codivus.Graph/Configuration/GraphConfiguration.cs`
- `src/Codivus.Graph/Configuration/GraphScanningConfiguration.cs`
- `src/Codivus.CLI/appsettings.json`
- `src/Codivus.API/appsettings.json`

**Actions:**
```csharp
// Replace JanusGraphSettings with Neo4jSettings
public class Neo4jSettings
{
    public string Uri { get; set; } = "bolt://localhost:7687";
    public string Username { get; set; } = "neo4j";
    public string Password { get; set; } = "pass12345678";
    public string Database { get; set; } = "codivus";
    public int MaxConnectionPoolSize { get; set; } = 50;
    public TimeSpan ConnectionAcquisitionTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableEncryption { get; set; } = false;
    public string TrustStrategy { get; set; } = "TrustAllCertificates"; // TrustAllCertificates, TrustSystemCaSignedCertificates
}
```

**Configuration Update:**
```json
{
  "Graph": {
    "Enabled": true,
    "Neo4j": {
      "Uri": "bolt://localhost:7687",
      "Username": "neo4j",
      "Password": "pass12345678",
      "Database": "codivus",
      "MaxConnectionPoolSize": 50,
      "ConnectionAcquisitionTimeout": "00:01:00",
      "ConnectionTimeout": "00:00:30",
      "EnableEncryption": false,
      "TrustStrategy": "TrustAllCertificates"
    }
  }
}
```

### Phase 2: Core Service Migration (Week 2-3)

#### 2.1 GraphStorageService Rewrite
**Target File:** `src/Codivus.Graph/Services/GraphStorageService.cs`

**Key Changes:**
1. **Connection Management**
   ```csharp
   private IDriver? _driver;
   private IAsyncSession? _session;
   
   public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
   {
       var settings = _configuration.Neo4j;
       _driver = GraphDatabase.Driver(
           settings.Uri,
           AuthTokens.Basic(settings.Username, settings.Password),
           config => config
               .WithMaxConnectionPoolSize(settings.MaxConnectionPoolSize)
               .WithConnectionAcquisitionTimeout(settings.ConnectionAcquisitionTimeout)
               .WithConnectionTimeout(settings.ConnectionTimeout)
               .WithEncryptionLevel(settings.EnableEncryption ? EncryptionLevel.Encrypted : EncryptionLevel.None)
       );
       
       _session = _driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));
       return true;
   }
   ```

2. **Schema Creation with Cypher**
   ```csharp
   public async Task<bool> CreateSchemaAsync(CancellationToken cancellationToken = default)
   {
       var cypherQueries = new[]
       {
           // Create constraints for unique external IDs
           "CREATE CONSTRAINT external_id_unique IF NOT EXISTS FOR (n:CodeNode) REQUIRE n.externalId IS UNIQUE",
           
           // Create indexes for performance
           "CREATE INDEX repository_index IF NOT EXISTS FOR (n:CodeNode) ON (n.repositoryId)",
           "CREATE INDEX node_type_index IF NOT EXISTS FOR (n:CodeNode) ON (n.nodeType)",
           "CREATE INDEX repository_type_index IF NOT EXISTS FOR (n:CodeNode) ON (n.repositoryId, n.nodeType)",
           
           // Create relationship type indexes
           "CREATE INDEX relationship_external_id IF NOT EXISTS FOR ()-[r]-() ON (r.externalId)"
       };
       
       foreach (var query in cypherQueries)
       {
           await _session.RunAsync(query);
       }
       return true;
   }
   ```

3. **Node Operations with Cypher**
   ```csharp
   public async Task<CodeNode> CreateNodeAsync(CodeNode node, CancellationToken cancellationToken = default)
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
           
       await _session.RunAsync(query, new
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
   ```

4. **Relationship Operations with Cypher**
   ```csharp
   public async Task<CodeRelationship> CreateRelationshipAsync(CodeRelationship relationship, CancellationToken cancellationToken = default)
   {
       relationship.Id = relationship.Id ?? Guid.NewGuid().ToString();
       relationship.CreatedAt = DateTime.UtcNow;
       
       var query = @"
           MATCH (source:CodeNode {externalId: $sourceId})
           MATCH (target:CodeNode {externalId: $targetId})
           CREATE (source)-[r:" + GetRelationshipType(relationship.Type) + @" {
               externalId: $externalId,
               context: $context,
               createdAt: $createdAt
           }]->(target)
           RETURN r";
           
       await _session.RunAsync(query, new
       {
           sourceId = relationship.SourceNodeId,
           targetId = relationship.TargetNodeId,
           externalId = relationship.Id,
           context = relationship.Context ?? "",
           createdAt = relationship.CreatedAt.Ticks
       });
       
       return relationship;
   }
   ```

#### 2.2 Remove JanusGraph Serializers
**Target File:** `src/Codivus.Graph/Serializers/JanusGraphSerializers.cs`

**Action:** Delete entire file - Neo4j driver handles serialization automatically.

### Phase 3: Test Migration (Week 3-4)

#### 3.1 Integration Test Migration
**Target Files:**
- `src/Codivus.Graph.Tests/Integration/JanusGraphIntegrationTests.cs` → `Neo4jIntegrationTests.cs`
- `src/Codivus.Graph.Tests/Helpers/JanusGraphTestHelper.cs` → `Neo4jTestHelper.cs`
- `src/Codivus.CLI.Tests/Helpers/JanusGraphTestContainer.cs` → `Neo4jTestContainer.cs`

**Key Changes:**

1. **Test Container Migration**
   ```csharp
   public class Neo4jTestContainer : IAsyncDisposable
   {
       private const string Neo4jImage = "neo4j:5.15-community";
       private const int HttpPort = 7474;
       private const int BoltPort = 7687;
       
       public async Task<bool> StartAsync()
       {
           // Use testcontainers-dotnet for Neo4j
           var container = new ContainerBuilder()
               .WithImage(Neo4jImage)
               .WithPortBinding(HttpPort, HttpPort)
               .WithPortBinding(BoltPort, BoltPort)
               .WithEnvironment("NEO4J_AUTH", "neo4j/testpassword")
               .WithEnvironment("NEO4J_PLUGINS", "[\"apoc\"]")
               .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(BoltPort))
               .Build();
               
           await container.StartAsync();
           return true;
       }
   }
   ```

2. **Test Helper Migration**
   ```csharp
   public static class Neo4jTestHelper
   {
       public static async Task<bool> IsNeo4jAvailableAsync(string uri = "bolt://localhost:7687")
       {
           try
           {
               using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic("neo4j", "pass12345678"));
               using var session = driver.AsyncSession();
               await session.RunAsync("RETURN 1");
               return true;
           }
           catch
           {
               return false;
           }
       }
   }
   ```

#### 3.2 Test Data Migration
**Actions:**
- Update all test assertions to work with Neo4j data structures
- Replace Gremlin traversals with Cypher queries in tests
- Update test collection attributes and categories
- Ensure tests assume Neo4j running on localhost:7474 (HTTP) and localhost:7687 (Bolt)

### Phase 4: Frontend Migration (Week 4)

#### 4.1 Vue.js Component Updates
**Target Files:**
- `src/Codivus.UI/src/components/graph/GraphSettings.vue`
- `src/Codivus.UI/src/store/graph.js`

**Changes:**
1. **Settings Component Update**
   ```vue
   <template>
     <div class="graph-settings">
       <h3>Neo4j Configuration</h3>
       <form @submit.prevent="saveSettings">
         <div class="form-group">
           <label>Connection URI:</label>
           <input v-model="localSettings.neo4j.uri" type="text" placeholder="bolt://localhost:7687" />
         </div>
         <div class="form-group">
           <label>Username:</label>
           <input v-model="localSettings.neo4j.username" type="text" placeholder="neo4j" />
         </div>
         <div class="form-group">
           <label>Password:</label>
           <input v-model="localSettings.neo4j.password" type="pass12345678" />
         </div>
         <div class="form-group">
           <label>Database:</label>
           <input v-model="localSettings.neo4j.database" type="text" placeholder="codivus" />
         </div>
       </form>
     </div>
   </template>
   ```

2. **Store Update**
   ```javascript
   const state = {
     settings: {
       neo4j: {
         uri: 'bolt://localhost:7687',
         username: 'neo4j',
         password: '',
         database: 'codivus',
         maxConnectionPoolSize: 50,
         enableEncryption: false
       }
     }
   }
   ```

### Phase 5: CLI and Service Updates (Week 5)

#### 5.1 Status Command Migration
**Target File:** `src/Codivus.CLI/Services/StatusCommandService.cs`

**Changes:**
```csharp
private async Task<bool> CheckNeo4jHealthAsync()
{
    try
    {
        var settings = _configuration.Graph.Neo4j;
        using var driver = GraphDatabase.Driver(
            settings.Uri,
            AuthTokens.Basic(settings.Username, settings.Password)
        );
        
        using var session = driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));
        var result = await session.RunAsync("RETURN 1 as health");
        var record = await result.SingleAsync();
        
        return record["health"].As<int>() == 1;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Neo4j health check failed");
        return false;
    }
}
```

#### 5.2 Configuration Service Migration
**Target File:** `src/Codivus.CLI/Services/ConfigurationService.cs`

**Actions:**
- Replace JanusGraph default configuration with Neo4j defaults
- Update configuration validation logic
- Update configuration display methods

### Phase 6: Documentation Updates (Week 5)

#### 6.1 Documentation Migration
**Target Files:**
- `README.md`
- `plan.md` 
- `src/docs/codivus-presentation.html`

**Actions:**
- Replace all JanusGraph references with Neo4j
- Update setup instructions
- Update architecture diagrams
- Update feature descriptions

#### 6.2 Code Comments and Inline Documentation
**Actions:**
- Search and replace all code comments mentioning JanusGraph
- Update XML documentation
- Update method descriptions

## Risk Mitigation

### Data Migration Considerations
1. **Schema Compatibility**: Neo4j's property graph model is compatible with the current schema
2. **Performance**: Neo4j typically offers better performance for read-heavy workloads
3. **Transaction Support**: Neo4j provides ACID transactions by default

### Testing Strategy
1. **Parallel Testing**: Run tests against both databases during transition
2. **Data Validation**: Verify data integrity after migration
3. **Performance Testing**: Benchmark operations to ensure performance improvements

### Rollback Plan
1. **Branch Strategy**: Maintain JanusGraph implementation in separate branch
2. **Feature Flags**: Use configuration to switch between implementations
3. **Gradual Migration**: Migrate components incrementally

## Success Criteria Validation

### Definition of Done Checklist

#### ✅ No Occurrences of "Janus" in Codebase
**Validation Commands:**
```bash
# Search for any remaining JanusGraph references
grep -r -i "janus" src/ --exclude-dir=.git
rg -i "janus" src/
```

**Expected Result:** No matches found

#### ✅ Fully Working CLI and User Interface Graph
**Validation Steps:**
1. CLI graph scanning commands execute successfully
2. Web UI graph visualization displays correctly
3. Graph configuration UI works with Neo4j settings
4. Real-time graph updates function properly

**Test Commands:**
```bash
# CLI functionality
./codivus scan --repository-path /path/to/repo --enable-graph
./codivus graph --status

# Integration test
dotnet test src/Codivus.Graph.Tests/ --filter "Category=Neo4j"
```

#### ✅ All Tests Passing
**Validation Commands:**
```bash
# Backend tests
dotnet test src/Codivus.API.Tests/
dotnet test src/Codivus.Graph.Tests/
dotnet test src/Codivus.CLI.Tests/

# Frontend tests  
cd src/Codivus.UI && npm test
```

**Expected Result:** All tests pass with Neo4j integration

#### ✅ Neo4j Integration Test Assumptions
**Test Environment Requirements:**
- Neo4j server running on `localhost:7474` (HTTP)
- Neo4j server running on `localhost:7687` (Bolt) 
- Default credentials: `neo4j/password`
- Database: `codivus`

**Validation:**
```csharp
[Fact]
[Trait("Category", "Neo4j")]
public async Task Integration_Test_Assumes_Neo4j_LocalHost()
{
    // Test assumes Neo4j running on localhost:7474 and localhost:7687
    var helper = new Neo4jTestHelper();
    var httpAvailable = await helper.IsNeo4jHttpAvailableAsync("http://localhost:7474");
    var boltAvailable = await helper.IsNeo4jBoltAvailableAsync("bolt://localhost:7687");
    
    Assert.True(httpAvailable, "Neo4j HTTP endpoint should be available on localhost:7474");
    Assert.True(boltAvailable, "Neo4j Bolt endpoint should be available on localhost:7687");
}
```

## Timeline Summary

| Phase | Duration | Deliverables |
|-------|----------|--------------|
| Phase 1: Neo4j Driver Integration | Week 1-2 | Updated dependencies, configuration |
| Phase 2: Core Service Migration | Week 2-3 | New GraphStorageService, removed serializers |
| Phase 3: Test Migration | Week 3-4 | Updated test suite, test containers |
| Phase 4: Frontend Migration | Week 4 | Updated Vue.js components |
| Phase 5: CLI and Service Updates | Week 5 | Updated CLI commands, services |
| Phase 6: Documentation Updates | Week 5 | Updated documentation |

**Total Duration:** 5 weeks

## Post-Migration Benefits

1. **Simplified Architecture**: Remove custom serializers and complex connection handling
2. **Better Performance**: Neo4j's optimized query engine and indexing
3. **Improved Developer Experience**: Better tooling, more intuitive Cypher queries
4. **Enhanced Reliability**: Neo4j's mature transaction and consistency guarantees
5. **Better Ecosystem**: Rich plugin ecosystem and community support

## Conclusion

This migration plan provides a comprehensive roadmap for replacing JanusGraph with Neo4j while maintaining full functionality and ensuring all tests pass. The phased approach minimizes risk and allows for thorough validation at each step.