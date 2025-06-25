using System;
using System.Collections.Generic;

namespace Codivus.Graph.Models
{
    public class GraphMetrics
    {
        public string RepositoryId { get; set; }
        public DateTime Timestamp { get; set; }
        public long VertexCount { get; set; }
        public long EdgeCount { get; set; }
        public Dictionary<string, long> VertexCountByType { get; set; } = new();
        public Dictionary<string, long> EdgeCountByType { get; set; } = new();
        public long TotalProjects { get; set; }
        public long TotalFiles { get; set; }
        public long TotalTypes { get; set; }
        public long TotalMethods { get; set; }
        public double AverageComplexity { get; set; }
        public double AverageCoupling { get; set; }
        public long ProcessingTimeMs { get; set; }
        public long MemoryUsageBytes { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
    }

    public class GraphQueryMetrics
    {
        public string QueryId { get; set; }
        public DateTime Timestamp { get; set; }
        public string QueryType { get; set; }
        public long ExecutionTimeMs { get; set; }
        public long ResultCount { get; set; }
        public long TraversedVertices { get; set; }
        public long TraversedEdges { get; set; }
        public bool FromCache { get; set; }
        public Dictionary<string, object> QueryParameters { get; set; } = new();
    }
}