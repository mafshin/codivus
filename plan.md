# Comprehensive Implementation Plan for Graph-Based Repository Scanning

## Overview
This plan introduces an optional graph-based scanning feature that creates a graph representation of repository code structure using Neo4j and Roslyn. The graph will capture detailed symbol information and relationships, enabling advanced integration-level issue detection when combined with LLM embeddings.

## Phase 1: Infrastructure Setup

### 1.1 Graph Storage Layer
```
src/Codivus.Graph/
├── Configuration/
│   └── GraphConfiguration.cs          # Neo4j connection settings
├── Interfaces/
│   ├── IGraphStorageService.cs       # Graph CRUD operations
│   ├── ICodeGraphBuilder.cs          # Build graph from code
│   └── IGraphQueryService.cs         # Query graph data
├── Models/
│   ├── CodeNode.cs                   # Represents code elements
│   ├── CodeRelationship.cs           # Represents relationships
│   ├── GraphSchema.cs                # Graph schema definitions
│   └── GraphMetrics.cs               # Performance metrics
├── Services/
│   ├── GraphStorageService.cs        # Neo4j implementation
│   ├── CodeGraphBuilder.cs           # Builds graph from Roslyn
│   └── GraphQueryService.cs          # Query implementations
└── Extensions/
    └── GremlinExtensions.cs          # Gremlin query helpers
```

### 1.2 Task Queue System
```
src/Codivus.Core/
├── Interfaces/
│   ├── ITaskQueue.cs                 # Generic task queue interface
│   └── IGraphScanTask.cs             # Graph scan task definition
└── Models/
    ├── ScanTask.cs                   # Task representation
    ├── TaskStatus.cs                 # Task state tracking
    └── GraphScanConfiguration.cs     # Graph-specific settings

src/Codivus.API/
├── Services/
│   ├── TaskQueueService.cs           # In-memory/persistent queue
│   ├── GraphScanOrchestrator.cs      # Coordinates graph scanning
│   └── RoslynAnalysisService.cs      # Roslyn code analysis
└── BackgroundServices/
    ├── GraphScanWorker.cs            # Processes scan tasks
    └── GraphMaintenanceWorker.cs     # Graph optimization
```

## Phase 2: Roslyn Integration

### 2.1 Code Analysis Components
```
src/Codivus.Analysis/
├── Interfaces/
│   ├── ICodeAnalyzer.cs              # Base analyzer interface
│   ├── ISymbolExtractor.cs           # Extract symbols
│   └── IDependencyAnalyzer.cs        # Analyze dependencies
├── Analyzers/
│   ├── CSharpAnalyzer.cs             # C# specific analysis
│   ├── ProjectAnalyzer.cs            # Project-level analysis
│   └── SolutionAnalyzer.cs           # Solution-level analysis
├── Extractors/
│   ├── ClassExtractor.cs             # Extract classes/interfaces
│   ├── MethodExtractor.cs            # Extract methods/properties
│   ├── DependencyExtractor.cs        # Extract dependencies
│   └── ReferenceExtractor.cs         # Extract references
└── Models/
    ├── CodeSymbol.cs                 # Symbol representation
    ├── DependencyInfo.cs             # Dependency details
    └── AnalysisResult.cs             # Analysis output
```

## Phase 3: Graph Schema Design

### 3.1 Vertex Types
- **Namespace**: Full namespace path, assembly info
- **Type**: Classes, interfaces, structs, enums
- **Method**: Method signatures, parameters, return types
- **Property**: Property types, accessors
- **Field**: Field types, modifiers
- **Parameter**: Parameter types, default values
- **File**: Source file paths, content hashes
- **Project**: Project metadata, framework info
- **Assembly**: Assembly metadata, versions

### 3.2 Edge Types
- **CONTAINS**: Namespace→Type, Type→Method
- **INHERITS**: Type→Type inheritance
- **IMPLEMENTS**: Type→Interface implementation
- **CALLS**: Method→Method invocations
- **USES**: Type→Type dependencies
- **REFERENCES**: Project→Assembly references
- **DECLARES**: File→Type declarations
- **OVERRIDES**: Method→Method overrides

### 3.3 Property Schema
```json
{
  "vertices": {
    "type": {
      "name": "string",
      "fullName": "string",
      "kind": "enum",
      "accessibility": "enum",
      "isAbstract": "boolean",
      "isSealed": "boolean",
      "lineCount": "integer",
      "complexity": "integer"
    },
    "method": {
      "name": "string",
      "signature": "string",
      "returnType": "string",
      "accessibility": "enum",
      "isAsync": "boolean",
      "cyclomaticComplexity": "integer",
      "lineCount": "integer"
    }
  }
}
```

## Phase 4: Scalable Processing Architecture

### 4.1 Task Queue Implementation
```csharp
public class GraphScanTask
{
    public string TaskId { get; set; }
    public string RepositoryId { get; set; }
    public ScanScope Scope { get; set; } // File, Project, Solution
    public string TargetPath { get; set; }
    public TaskPriority Priority { get; set; }
    public TaskStatus Status { get; set; }
    public int RetryCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### 4.2 Processing Pipeline
1. **Discovery Phase**: Enumerate files/projects
2. **Analysis Phase**: Roslyn parsing and extraction
3. **Graph Building Phase**: Create/update graph nodes
4. **Relationship Phase**: Establish edges
5. **Validation Phase**: Verify graph integrity
6. **Indexing Phase**: Update search indices

### 4.3 Concurrency Strategy
- **File-level parallelism**: Process multiple files concurrently
- **Batch operations**: Group graph mutations
- **Connection pooling**: Reuse Gremlin connections
- **Progress tracking**: Real-time status updates
- **Checkpoint system**: Resume from failures

## Phase 5: Integration with Existing System

### 5.1 API Extensions
```csharp
// New endpoints in GraphController
[ApiController]
[Route("api/[controller]")]
public class GraphController : ControllerBase
{
    [HttpPost("scan/{repositoryId}")]
    public async Task<IActionResult> StartGraphScan(string repositoryId, GraphScanRequest request);
    
    [HttpGet("status/{scanId}")]
    public async Task<IActionResult> GetGraphScanStatus(string scanId);
    
    [HttpPost("query")]
    public async Task<IActionResult> QueryGraph(GraphQueryRequest request);
    
    [HttpGet("visualization/{repositoryId}")]
    public async Task<IActionResult> GetGraphVisualization(string repositoryId);
}
```

### 5.2 Configuration Extensions
```json
{
  "GraphScanning": {
    "Enabled": true,
    "Neo4j": {
      "Host": "localhost",
      "Port": 8182,
      "Username": "",
      "Password": "",
      "ConnectionPoolSize": 10
    },
    "Processing": {
      "MaxConcurrentFiles": 50,
      "BatchSize": 1000,
      "TimeoutMinutes": 30,
      "RetryAttempts": 3
    },
    "Analysis": {
      "IncludeTests": false,
      "MaxFileSize": 1048576,
      "SupportedExtensions": [".cs", ".vb"]
    }
  }
}
```

## Phase 6: Performance Optimizations

### 6.1 Caching Strategy
- **Symbol cache**: Cache extracted symbols
- **Graph query cache**: Cache frequent queries
- **Compilation cache**: Reuse Roslyn compilations

### 6.2 Incremental Updates
- **File watchers**: Monitor changes
- **Diff analysis**: Update only changed elements
- **Version tracking**: Track graph versions

### 6.3 Resource Management
- **Memory limits**: Control Roslyn memory usage
- **Connection limits**: Manage graph connections
- **Throttling**: Rate limit processing

## Phase 7: LLM Integration

### 7.1 Graph Embeddings
- **Subgraph extraction**: Extract relevant subgraphs
- **Graph serialization**: Convert to LLM-friendly format
- **Context enrichment**: Add graph context to prompts

### 7.2 Enhanced Scanning
```csharp
public class GraphEnhancedScanningService
{
    public async Task<IEnumerable<CodeIssue>> ScanWithGraphContext(
        string repositoryId, 
        string filePath,
        GraphContext context)
    {
        // Extract relevant subgraph
        var subgraph = await _graphQuery.GetContextualSubgraph(filePath);
        
        // Generate embeddings
        var embeddings = _embeddingService.GenerateGraphEmbeddings(subgraph);
        
        // Enhanced LLM prompt with graph context
        var prompt = _promptBuilder.BuildWithGraphContext(code, embeddings);
        
        // Analyze with LLM
        return await _llmProvider.AnalyzeWithContext(prompt);
    }
}
```

## Implementation Timeline

**Week 1-2**: Infrastructure setup
- Set up Neo4j connection
- Implement basic graph service
- Create task queue system

**Week 3-4**: Roslyn integration
- Implement code analyzers
- Create symbol extractors
- Build dependency analyzers

**Week 5-6**: Graph building
- Implement graph schema
- Create batch processing
- Add progress tracking

**Week 7-8**: Integration
- Connect with existing scanning
- Add API endpoints
- Implement configuration

**Week 9-10**: Optimization
- Add caching layers
- Implement incremental updates
- Performance tuning

**Week 11-12**: LLM enhancement
- Graph embedding generation
- Context-aware scanning
- Testing and refinement

## Key Benefits

This comprehensive plan provides a scalable, efficient solution for graph-based repository scanning that:

1. **Handles large repositories** (5M LOC, 2k-10k projects) through parallel processing and batching
2. **Uses Roslyn** for accurate C# code analysis and symbol extraction
3. **Implements a task queue** for reliable, resumable processing
4. **Integrates seamlessly** with existing plain-text scanning as an optional feature
5. **Provides graph context** to LLMs for advanced integration-level issue detection
6. **Scales horizontally** through connection pooling and concurrent processing

The plan maintains backward compatibility while adding powerful graph-based capabilities for deeper code analysis.