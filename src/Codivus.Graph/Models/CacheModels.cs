using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Codivus.Graph.Models
{
    /// <summary>
    /// Cached symbol data for a file
    /// </summary>
    public class CachedSymbolData
    {
        public string FileId { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public string RepositoryId { get; set; } = string.Empty;
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public List<CodeNode> Nodes { get; set; } = new();
        public List<CodeRelationship> Relationships { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public long SizeInBytes { get; set; }
    }

    /// <summary>
    /// Cached Roslyn compilation data
    /// </summary>
    public class CachedCompilation
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public byte[] SerializedCompilation { get; set; } = Array.Empty<byte>();
        public List<string> Dependencies { get; set; } = new();
        public Dictionary<string, string> MetadataReferences { get; set; } = new();
        public long SizeInBytes { get; set; }
    }

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class CacheStatistics
    {
        public string CacheType { get; set; } = string.Empty;
        public long TotalEntries { get; set; }
        public long TotalSizeBytes { get; set; }
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public double HitRatio => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;
        public long TotalRequests => HitCount + MissCount;
        public DateTime LastMaintenance { get; set; }
        public long ExpiredEntries { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    /// <summary>
    /// Cache entry metadata
    /// </summary>
    public class CacheEntry<T>
    {
        public string Key { get; set; } = string.Empty;
        public T Data { get; set; } = default!;
        public DateTime CachedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public long SizeInBytes { get; set; }
        public int AccessCount { get; set; }
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    }

    /// <summary>
    /// Cache configuration options
    /// </summary>
    public class CacheOptions
    {
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(24);
        public long MaxSizeBytes { get; set; } = 1024 * 1024 * 1024; // 1GB
        public int MaxEntries { get; set; } = 10000;
        public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromHours(1);
        public double EvictionThreshold { get; set; } = 0.8; // Start eviction at 80% capacity
        public bool EnableStatistics { get; set; } = true;
        public string PersistencePath { get; set; } = string.Empty; // Empty means in-memory only
    }

    /// <summary>
    /// Cache eviction policy
    /// </summary>
    public enum EvictionPolicy
    {
        LeastRecentlyUsed,
        LeastFrequentlyUsed,
        FirstInFirstOut,
        TimeToLive
    }

    /// <summary>
    /// Version tracking for incremental updates
    /// </summary>
    public class VersionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public string Checksum { get; set; } = string.Empty;
        public Dictionary<string, string> Dependencies { get; set; } = new();
    }
}