using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Codivus.Graph.Interfaces;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// Service for managing system resources during graph processing
    /// </summary>
    public class ResourceManager : IResourceManager, IDisposable
    {
        private readonly ConcurrentDictionary<string, MemoryAllocation> _memoryAllocations;
        private readonly ConcurrentDictionary<string, GraphConnection> _activeConnections;
        private readonly ConcurrentDictionary<ProcessingType, SemaphoreSlim> _processingSemaphores;
        private readonly ConcurrentDictionary<string, ProcessingSlot> _activeSlots;
        private readonly ConcurrentQueue<(TaskCompletionSource<ProcessingSlot>, ProcessingType, int, string)> _processingQueue;
        private readonly Timer _cleanupTimer;
        private readonly Timer _monitoringTimer;
        private readonly ILogger<ResourceManager> _logger;
        
        private ResourceLimits _limits;
        private long _totalAllocatedMemory;
        private long _peakMemoryUsage;
        private long _totalConnectionsCreated;
        private long _totalRequestsProcessed;
        private readonly object _statsLock = new object();
        private bool _disposed;

        public event EventHandler<ResourceLimitExceededEventArgs>? ResourceLimitExceeded;

        public ResourceManager(
            IOptions<ResourceLimits> options,
            ILogger<ResourceManager> logger)
        {
            _limits = options.Value;
            _logger = logger;
            
            _memoryAllocations = new ConcurrentDictionary<string, MemoryAllocation>();
            _activeConnections = new ConcurrentDictionary<string, GraphConnection>();
            _processingSemaphores = new ConcurrentDictionary<ProcessingType, SemaphoreSlim>();
            _activeSlots = new ConcurrentDictionary<string, ProcessingSlot>();
            _processingQueue = new ConcurrentQueue<(TaskCompletionSource<ProcessingSlot>, ProcessingType, int, string)>();

            // Initialize processing semaphores
            foreach (ProcessingType type in Enum.GetValues<ProcessingType>())
            {
                var maxConcurrent = _limits.MaxConcurrentByType.GetValueOrDefault(type, 10);
                _processingSemaphores[type] = new SemaphoreSlim(maxConcurrent, maxConcurrent);
            }

            // Start cleanup timer (every 5 minutes)
            _cleanupTimer = new Timer(CleanupExpiredResources, null, 
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Start monitoring timer (every minute)
            _monitoringTimer = new Timer(MonitorResourceUsage, null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger.LogInformation("Resource manager initialized with memory limit {MemoryLimitMB}MB, connection limit {ConnectionLimit}",
                _limits.MaxMemoryBytes / (1024 * 1024), _limits.MaxConnections);
        }

        public async Task<MemoryAllocation> AcquireMemoryAsync(long requestedBytes, string processName, CancellationToken cancellationToken = default)
        {
            // Check if request exceeds available memory
            var currentUsage = Interlocked.Read(ref _totalAllocatedMemory);
            if (currentUsage + requestedBytes > _limits.MaxMemoryBytes)
            {
                // Try cleanup first
                await ForceCleanupAsync();
                currentUsage = Interlocked.Read(ref _totalAllocatedMemory);
                
                if (currentUsage + requestedBytes > _limits.MaxMemoryBytes)
                {
                    var eventArgs = new ResourceLimitExceededEventArgs
                    {
                        ResourceType = ResourceType.Memory,
                        CurrentUsage = currentUsage + requestedBytes,
                        Limit = _limits.MaxMemoryBytes,
                        ProcessName = processName,
                        Timestamp = DateTime.UtcNow,
                        RecommendedAction = "Release unused memory allocations or increase memory limit"
                    };
                    
                    ResourceLimitExceeded?.Invoke(this, eventArgs);
                    throw new InsufficientMemoryException($"Memory limit exceeded. Requested: {requestedBytes}, Available: {_limits.MaxMemoryBytes - currentUsage}");
                }
            }

            var allocation = new MemoryAllocation
            {
                AllocationId = Guid.NewGuid().ToString(),
                AllocatedBytes = requestedBytes,
                AllocatedAt = DateTime.UtcNow,
                ProcessName = processName,
                MaxDuration = _limits.MaxAllocationDuration
            };

            _memoryAllocations[allocation.AllocationId] = allocation;
            Interlocked.Add(ref _totalAllocatedMemory, requestedBytes);

            // Update peak usage
            var newTotal = Interlocked.Read(ref _totalAllocatedMemory);
            lock (_statsLock)
            {
                if (newTotal > _peakMemoryUsage)
                {
                    _peakMemoryUsage = newTotal;
                }
            }

            _logger.LogDebug("Allocated {MemoryMB}MB for process {ProcessName} (total: {TotalMB}MB)",
                requestedBytes / (1024 * 1024), processName, newTotal / (1024 * 1024));

            return allocation;
        }

        public async Task ReleaseMemoryAsync(MemoryAllocation allocation)
        {
            if (allocation.IsReleased)
                return;

            if (_memoryAllocations.TryRemove(allocation.AllocationId, out var removed))
            {
                Interlocked.Add(ref _totalAllocatedMemory, -removed.AllocatedBytes);
                allocation.IsReleased = true;

                _logger.LogDebug("Released {MemoryMB}MB for process {ProcessName} (total: {TotalMB}MB)",
                    removed.AllocatedBytes / (1024 * 1024), removed.ProcessName, 
                    Interlocked.Read(ref _totalAllocatedMemory) / (1024 * 1024));
            }
        }

        public async Task<GraphConnection> AcquireConnectionAsync(string connectionType, CancellationToken cancellationToken = default)
        {
            // Check connection limit
            if (_activeConnections.Count >= _limits.MaxConnections)
            {
                // Try cleanup first
                await CleanupExpiredConnections();
                
                if (_activeConnections.Count >= _limits.MaxConnections)
                {
                    var eventArgs = new ResourceLimitExceededEventArgs
                    {
                        ResourceType = ResourceType.Connections,
                        CurrentUsage = _activeConnections.Count + 1,
                        Limit = _limits.MaxConnections,
                        Timestamp = DateTime.UtcNow,
                        RecommendedAction = "Release unused connections or increase connection limit"
                    };
                    
                    ResourceLimitExceeded?.Invoke(this, eventArgs);
                    throw new InvalidOperationException($"Connection limit exceeded. Active: {_activeConnections.Count}, Limit: {_limits.MaxConnections}");
                }
            }

            var connection = new GraphConnection
            {
                ConnectionId = Guid.NewGuid().ToString(),
                ConnectionType = connectionType,
                AcquiredAt = DateTime.UtcNow,
                IsActive = true
            };

            _activeConnections[connection.ConnectionId] = connection;
            Interlocked.Increment(ref _totalConnectionsCreated);

            _logger.LogDebug("Acquired {ConnectionType} connection {ConnectionId} (active: {ActiveCount})",
                connectionType, connection.ConnectionId, _activeConnections.Count);

            return connection;
        }

        public async Task ReleaseConnectionAsync(GraphConnection connection)
        {
            if (!connection.IsActive)
                return;

            if (_activeConnections.TryRemove(connection.ConnectionId, out var removed))
            {
                connection.IsActive = false;
                removed.UnderlyingConnection = null;

                _logger.LogDebug("Released {ConnectionType} connection {ConnectionId} (active: {ActiveCount})",
                    removed.ConnectionType, removed.ConnectionId, _activeConnections.Count);
            }
        }

        public async Task<ProcessingSlot> AcquireProcessingSlotAsync(ProcessingType processingType, int priority = 0, CancellationToken cancellationToken = default)
        {
            var semaphore = _processingSemaphores[processingType];
            var processName = $"{processingType}_{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                await semaphore.WaitAsync(cancellationToken);
                
                var slot = new ProcessingSlot
                {
                    SlotId = Guid.NewGuid().ToString(),
                    Type = processingType,
                    Priority = priority,
                    AcquiredAt = DateTime.UtcNow,
                    IsActive = true,
                    ProcessName = processName
                };

                _activeSlots[slot.SlotId] = slot;
                Interlocked.Increment(ref _totalRequestsProcessed);

                _logger.LogDebug("Acquired {ProcessingType} slot {SlotId} (active: {ActiveCount})",
                    processingType, slot.SlotId, _activeSlots.Values.Count(s => s.Type == processingType && s.IsActive));

                return slot;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Processing slot acquisition cancelled for {ProcessingType}", processingType);
                throw;
            }
        }

        public async Task ReleaseProcessingSlotAsync(ProcessingSlot slot)
        {
            if (!slot.IsActive)
                return;

            if (_activeSlots.TryRemove(slot.SlotId, out var removed))
            {
                slot.IsActive = false;
                var semaphore = _processingSemaphores[slot.Type];
                semaphore.Release();

                _logger.LogDebug("Released {ProcessingType} slot {SlotId} (active: {ActiveCount})",
                    slot.Type, slot.SlotId, _activeSlots.Values.Count(s => s.Type == slot.Type && s.IsActive));
            }
        }

        public async Task<ResourceUsageStatistics> GetUsageStatisticsAsync()
        {
            var stats = new ResourceUsageStatistics
            {
                CollectedAt = DateTime.UtcNow
            };

            // Memory statistics
            var memoryStats = new MemoryUsageStats
            {
                TotalAllocatedBytes = Interlocked.Read(ref _totalAllocatedMemory),
                LimitBytes = _limits.MaxMemoryBytes,
                ActiveAllocations = _memoryAllocations.Count,
                AvailableBytes = _limits.MaxMemoryBytes - Interlocked.Read(ref _totalAllocatedMemory)
            };

            lock (_statsLock)
            {
                memoryStats.PeakUsageBytes = _peakMemoryUsage;
            }

            // Group allocations by process
            foreach (var allocation in _memoryAllocations.Values)
            {
                memoryStats.AllocationsByProcess[allocation.ProcessName] = 
                    memoryStats.AllocationsByProcess.GetValueOrDefault(allocation.ProcessName, 0) + allocation.AllocatedBytes;
            }

            stats.Memory = memoryStats;

            // Connection statistics
            var connectionStats = new ConnectionUsageStats
            {
                ActiveConnections = _activeConnections.Count,
                MaxConnections = _limits.MaxConnections,
                TotalConnectionsCreated = (int)Interlocked.Read(ref _totalConnectionsCreated)
            };

            // Group connections by type
            foreach (var connection in _activeConnections.Values)
            {
                connectionStats.ConnectionsByType[connection.ConnectionType] = 
                    connectionStats.ConnectionsByType.GetValueOrDefault(connection.ConnectionType, 0) + 1;
            }

            // Calculate average connection age
            if (_activeConnections.Any())
            {
                var totalAge = _activeConnections.Values.Sum(c => (DateTime.UtcNow - c.AcquiredAt).TotalSeconds);
                connectionStats.AverageConnectionAge = TimeSpan.FromSeconds(totalAge / _activeConnections.Count);
            }

            stats.Connections = connectionStats;

            // Processing statistics
            var processingStats = new ProcessingUsageStats
            {
                TotalRequestsProcessed = Interlocked.Read(ref _totalRequestsProcessed)
            };

            // Count active slots by type
            foreach (ProcessingType type in Enum.GetValues<ProcessingType>())
            {
                var activeCount = _activeSlots.Values.Count(s => s.Type == type && s.IsActive);
                var maxCount = _limits.MaxConcurrentByType.GetValueOrDefault(type, 10);
                
                processingStats.ActiveSlotsByType[type] = activeCount;
                processingStats.MaxSlotsByType[type] = maxCount;
            }

            stats.Processing = processingStats;

            return stats;
        }

        public async Task ConfigureLimitsAsync(ResourceLimits limits)
        {
            var oldLimits = _limits;
            _limits = limits;

            // Update processing semaphores if limits changed
            foreach (ProcessingType type in Enum.GetValues<ProcessingType>())
            {
                var newLimit = limits.MaxConcurrentByType.GetValueOrDefault(type, 10);
                var oldLimit = oldLimits.MaxConcurrentByType.GetValueOrDefault(type, 10);
                
                if (newLimit != oldLimit)
                {
                    // Dispose old semaphore and create new one
                    if (_processingSemaphores.TryRemove(type, out var oldSemaphore))
                    {
                        oldSemaphore.Dispose();
                    }
                    _processingSemaphores[type] = new SemaphoreSlim(newLimit, newLimit);
                }
            }

            _logger.LogInformation("Resource limits updated: Memory {MemoryMB}MB, Connections {Connections}",
                limits.MaxMemoryBytes / (1024 * 1024), limits.MaxConnections);
        }

        public async Task ForceCleanupAsync()
        {
            var cleaned = 0;

            // Clean up expired memory allocations
            var expiredAllocations = _memoryAllocations.Values
                .Where(a => DateTime.UtcNow - a.AllocatedAt > a.MaxDuration)
                .ToList();

            foreach (var allocation in expiredAllocations)
            {
                await ReleaseMemoryAsync(allocation);
                cleaned++;
            }

            // Clean up expired connections
            await CleanupExpiredConnections();

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (cleaned > 0)
            {
                _logger.LogInformation("Forced cleanup completed: {CleanedCount} resources cleaned", cleaned);
            }
        }

        private void CleanupExpiredResources(object? state)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    await ForceCleanupAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled resource cleanup");
            }
        }

        private async Task CleanupExpiredConnections()
        {
            var expiredConnections = _activeConnections.Values
                .Where(c => DateTime.UtcNow - c.AcquiredAt > _limits.MaxConnectionAge)
                .ToList();

            foreach (var connection in expiredConnections)
            {
                await ReleaseConnectionAsync(connection);
            }
        }

        private void MonitorResourceUsage(object? state)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    var stats = await GetUsageStatisticsAsync();
                    
                    // Check memory usage
                    if (stats.Memory.UsagePercent > _limits.MemoryWarningThreshold * 100)
                    {
                        var eventArgs = new ResourceLimitExceededEventArgs
                        {
                            ResourceType = ResourceType.Memory,
                            CurrentUsage = stats.Memory.TotalAllocatedBytes,
                            Limit = stats.Memory.LimitBytes,
                            Timestamp = DateTime.UtcNow,
                            RecommendedAction = stats.Memory.UsagePercent > _limits.MemoryLimitThreshold * 100 
                                ? "Critical: Force cleanup required" 
                                : "Warning: Consider releasing unused allocations"
                        };
                        
                        ResourceLimitExceeded?.Invoke(this, eventArgs);

                        if (stats.Memory.UsagePercent > _limits.MemoryLimitThreshold * 100)
                        {
                            await ForceCleanupAsync();
                        }
                    }

                    // Check connection usage
                    if (stats.Connections.UsagePercent > 80)
                    {
                        await CleanupExpiredConnections();
                    }

                    _logger.LogDebug("Resource monitoring: Memory {MemoryPercent:F1}%, Connections {ConnectionPercent:F1}%, Active slots {ActiveSlots}",
                        stats.Memory.UsagePercent, stats.Connections.UsagePercent, 
                        stats.Processing.ActiveSlotsByType.Values.Sum());
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resource monitoring");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _monitoringTimer?.Dispose();

                // Release all active resources
                foreach (var allocation in _memoryAllocations.Values.ToList())
                {
                    allocation.Dispose();
                }

                foreach (var connection in _activeConnections.Values.ToList())
                {
                    connection.Dispose();
                }

                foreach (var slot in _activeSlots.Values.ToList())
                {
                    slot.Dispose();
                }

                foreach (var semaphore in _processingSemaphores.Values)
                {
                    semaphore.Dispose();
                }

                _disposed = true;
                _logger.LogInformation("Resource manager disposed");
            }
        }
    }
}