using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codivus.Graph.Interfaces
{
    /// <summary>
    /// Interface for managing system resources during graph processing
    /// </summary>
    public interface IResourceManager
    {
        /// <summary>
        /// Acquires memory resources for processing
        /// </summary>
        Task<MemoryAllocation> AcquireMemoryAsync(long requestedBytes, string processName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases previously acquired memory
        /// </summary>
        Task ReleaseMemoryAsync(MemoryAllocation allocation);

        /// <summary>
        /// Acquires a connection to the graph database
        /// </summary>
        Task<GraphConnection> AcquireConnectionAsync(string connectionType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases a graph database connection
        /// </summary>
        Task ReleaseConnectionAsync(GraphConnection connection);

        /// <summary>
        /// Acquires processing slot with throttling
        /// </summary>
        Task<ProcessingSlot> AcquireProcessingSlotAsync(ProcessingType processingType, int priority = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases a processing slot
        /// </summary>
        Task ReleaseProcessingSlotAsync(ProcessingSlot slot);

        /// <summary>
        /// Gets current resource usage statistics
        /// </summary>
        Task<ResourceUsageStatistics> GetUsageStatisticsAsync();

        /// <summary>
        /// Configures resource limits
        /// </summary>
        Task ConfigureLimitsAsync(ResourceLimits limits);

        /// <summary>
        /// Forces garbage collection and cleanup
        /// </summary>
        Task ForceCleanupAsync();

        /// <summary>
        /// Event fired when resource limits are exceeded
        /// </summary>
        event EventHandler<ResourceLimitExceededEventArgs> ResourceLimitExceeded;
    }

    /// <summary>
    /// Represents an allocated memory block
    /// </summary>
    public class MemoryAllocation : IDisposable
    {
        public string AllocationId { get; set; } = string.Empty;
        public long AllocatedBytes { get; set; }
        public DateTime AllocatedAt { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public bool IsReleased { get; set; }
        public TimeSpan MaxDuration { get; set; }

        public void Dispose()
        {
            IsReleased = true;
        }
    }

    /// <summary>
    /// Represents a graph database connection
    /// </summary>
    public class GraphConnection : IDisposable
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = string.Empty;
        public DateTime AcquiredAt { get; set; }
        public bool IsActive { get; set; }
        public int UsageCount { get; set; }
        public object? UnderlyingConnection { get; set; }

        public void Dispose()
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Represents a processing slot for throttling
    /// </summary>
    public class ProcessingSlot : IDisposable
    {
        public string SlotId { get; set; } = string.Empty;
        public ProcessingType Type { get; set; }
        public int Priority { get; set; }
        public DateTime AcquiredAt { get; set; }
        public bool IsActive { get; set; }
        public string ProcessName { get; set; } = string.Empty;

        public void Dispose()
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Resource usage statistics
    /// </summary>
    public class ResourceUsageStatistics
    {
        public MemoryUsageStats Memory { get; set; } = new();
        public ConnectionUsageStats Connections { get; set; } = new();
        public ProcessingUsageStats Processing { get; set; } = new();
        public DateTime CollectedAt { get; set; }
    }

    /// <summary>
    /// Memory usage statistics
    /// </summary>
    public class MemoryUsageStats
    {
        public long TotalAllocatedBytes { get; set; }
        public long AvailableBytes { get; set; }
        public long LimitBytes { get; set; }
        public int ActiveAllocations { get; set; }
        public double UsagePercent => LimitBytes > 0 ? (double)TotalAllocatedBytes / LimitBytes * 100 : 0;
        public long PeakUsageBytes { get; set; }
        public Dictionary<string, long> AllocationsByProcess { get; set; } = new();
    }

    /// <summary>
    /// Connection usage statistics
    /// </summary>
    public class ConnectionUsageStats
    {
        public int ActiveConnections { get; set; }
        public int MaxConnections { get; set; }
        public double UsagePercent => MaxConnections > 0 ? (double)ActiveConnections / MaxConnections * 100 : 0;
        public Dictionary<string, int> ConnectionsByType { get; set; } = new();
        public int TotalConnectionsCreated { get; set; }
        public TimeSpan AverageConnectionAge { get; set; }
    }

    /// <summary>
    /// Processing usage statistics
    /// </summary>
    public class ProcessingUsageStats
    {
        public Dictionary<ProcessingType, int> ActiveSlotsByType { get; set; } = new();
        public Dictionary<ProcessingType, int> MaxSlotsByType { get; set; } = new();
        public int QueuedRequests { get; set; }
        public TimeSpan AverageWaitTime { get; set; }
        public long TotalRequestsProcessed { get; set; }
    }

    /// <summary>
    /// Resource limits configuration
    /// </summary>
    public class ResourceLimits
    {
        public long MaxMemoryBytes { get; set; } = 4L * 1024 * 1024 * 1024; // 4GB
        public int MaxConnections { get; set; } = 50;
        public Dictionary<ProcessingType, int> MaxConcurrentByType { get; set; } = new();
        public TimeSpan MaxAllocationDuration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan MaxConnectionAge { get; set; } = TimeSpan.FromMinutes(30);
        public double MemoryWarningThreshold { get; set; } = 0.8; // 80%
        public double MemoryLimitThreshold { get; set; } = 0.95; // 95%
    }

    /// <summary>
    /// Event arguments for resource limit exceeded
    /// </summary>
    public class ResourceLimitExceededEventArgs : EventArgs
    {
        public ResourceType ResourceType { get; set; }
        public long CurrentUsage { get; set; }
        public long Limit { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? RecommendedAction { get; set; }
    }

    /// <summary>
    /// Types of processing operations
    /// </summary>
    public enum ProcessingType
    {
        FileAnalysis,
        GraphBuilding,
        GraphQuery,
        CacheOperation,
        Maintenance,
        Backup
    }

    /// <summary>
    /// Types of system resources
    /// </summary>
    public enum ResourceType
    {
        Memory,
        Connections,
        Processing,
        Disk,
        Network
    }
}