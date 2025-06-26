using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// In-memory symbol cache implementation with LRU eviction
    /// </summary>
    public class SymbolCacheService : ISymbolCache, IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheEntry<CachedSymbolData>> _cache;
        private readonly ConcurrentDictionary<string, DateTime> _accessTimes;
        private readonly CacheOptions _options;
        private readonly ILogger<SymbolCacheService> _logger;
        private readonly Timer _maintenanceTimer;
        private readonly SemaphoreSlim _maintenanceLock;
        private long _hitCount;
        private long _missCount;
        private bool _disposed;

        public SymbolCacheService(
            IOptions<CacheOptions> options,
            ILogger<SymbolCacheService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _cache = new ConcurrentDictionary<string, CacheEntry<CachedSymbolData>>();
            _accessTimes = new ConcurrentDictionary<string, DateTime>();
            _maintenanceLock = new SemaphoreSlim(1, 1);
            
            // Start maintenance timer
            _maintenanceTimer = new Timer(
                MaintenanceCallback,
                null,
                _options.MaintenanceInterval,
                _options.MaintenanceInterval);

            _logger.LogInformation("Symbol cache initialized with max size {MaxSize}MB, max entries {MaxEntries}", 
                _options.MaxSizeBytes / (1024 * 1024), _options.MaxEntries);
        }

        public async Task<CachedSymbolData?> GetSymbolsAsync(string fileId, string checksum)
        {
            var key = GenerateKey(fileId, checksum);
            
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    _accessTimes.TryRemove(key, out _);
                    Interlocked.Increment(ref _missCount);
                    _logger.LogDebug("Cache entry expired for file {FileId}", fileId);
                    return null;
                }

                // Update access time
                entry.LastAccessed = DateTime.UtcNow;
                entry.AccessCount++;
                _accessTimes[key] = entry.LastAccessed;
                
                Interlocked.Increment(ref _hitCount);
                _logger.LogDebug("Cache hit for file {FileId}", fileId);
                return entry.Data;
            }

            Interlocked.Increment(ref _missCount);
            _logger.LogDebug("Cache miss for file {FileId}", fileId);
            return null;
        }

        public async Task SetSymbolsAsync(string fileId, string checksum, CachedSymbolData symbolData, TimeSpan? expiration = null)
        {
            var key = GenerateKey(fileId, checksum);
            var now = DateTime.UtcNow;
            var expiresAt = expiration.HasValue ? now.Add(expiration.Value) : now.Add(_options.DefaultExpiration);
            
            var entry = new CacheEntry<CachedSymbolData>
            {
                Key = key,
                Data = symbolData,
                CachedAt = now,
                LastAccessed = now,
                ExpiresAt = expiresAt,
                SizeInBytes = EstimateSize(symbolData),
                AccessCount = 1
            };

            // Check if we need to evict entries before adding
            await EnsureCapacityAsync();

            _cache[key] = entry;
            _accessTimes[key] = now;
            
            _logger.LogDebug("Cached symbols for file {FileId}, size {Size} bytes", fileId, entry.SizeInBytes);
        }

        public async Task RemoveSymbolsAsync(string fileId)
        {
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith($"{fileId}:")).ToList();
            
            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
                _accessTimes.TryRemove(key, out _);
            }

            _logger.LogDebug("Removed {Count} cache entries for file {FileId}", keysToRemove.Count, fileId);
        }

        public async Task ClearRepositoryAsync(string repositoryId)
        {
            var keysToRemove = _cache.Values
                .Where(entry => entry.Data.RepositoryId == repositoryId)
                .Select(entry => entry.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
                _accessTimes.TryRemove(key, out _);
            }

            _logger.LogInformation("Cleared {Count} cache entries for repository {RepositoryId}", keysToRemove.Count, repositoryId);
        }

        public async Task<CacheStatistics> GetStatisticsAsync()
        {
            var totalSize = _cache.Values.Sum(e => e.SizeInBytes);
            var now = DateTime.UtcNow;
            var expiredCount = _cache.Values.Count(e => e.IsExpired);

            return new CacheStatistics
            {
                CacheType = "SymbolCache",
                TotalEntries = _cache.Count,
                TotalSizeBytes = totalSize,
                HitCount = _hitCount,
                MissCount = _missCount,
                ExpiredEntries = expiredCount,
                AdditionalMetrics = new Dictionary<string, object>
                {
                    ["MaxSizeBytes"] = _options.MaxSizeBytes,
                    ["MaxEntries"] = _options.MaxEntries,
                    ["UsagePercent"] = Math.Round((double)totalSize / _options.MaxSizeBytes * 100, 2),
                    ["CapacityPercent"] = Math.Round((double)_cache.Count / _options.MaxEntries * 100, 2)
                }
            };
        }

        public async Task PerformMaintenanceAsync(CancellationToken cancellationToken = default)
        {
            if (!await _maintenanceLock.WaitAsync(100, cancellationToken))
                return;

            try
            {
                var now = DateTime.UtcNow;
                var removed = 0;

                // Remove expired entries
                var expiredKeys = _cache
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    if (_cache.TryRemove(key, out _))
                    {
                        _accessTimes.TryRemove(key, out _);
                        removed++;
                    }
                }

                // Check if we need to evict more entries due to size/count limits
                await EnsureCapacityAsync();

                if (removed > 0)
                {
                    _logger.LogDebug("Cache maintenance removed {Count} expired entries", removed);
                }
            }
            finally
            {
                _maintenanceLock.Release();
            }
        }

        private async Task EnsureCapacityAsync()
        {
            var totalSize = _cache.Values.Sum(e => e.SizeInBytes);
            var totalEntries = _cache.Count;

            // Check if we're over capacity
            if (totalSize > _options.MaxSizeBytes * _options.EvictionThreshold ||
                totalEntries > _options.MaxEntries * _options.EvictionThreshold)
            {
                await EvictEntriesAsync();
            }
        }

        private async Task EvictEntriesAsync()
        {
            var targetSize = (long)(_options.MaxSizeBytes * 0.7); // Evict to 70% capacity
            var targetEntries = (int)(_options.MaxEntries * 0.7);

            // Get entries sorted by last access time (LRU)
            var entriesToEvict = _cache.Values
                .OrderBy(e => e.LastAccessed)
                .TakeWhile(e => 
                {
                    var currentSize = _cache.Values.Sum(x => x.SizeInBytes);
                    var currentCount = _cache.Count;
                    return currentSize > targetSize || currentCount > targetEntries;
                })
                .ToList();

            var evicted = 0;
            foreach (var entry in entriesToEvict)
            {
                if (_cache.TryRemove(entry.Key, out _))
                {
                    _accessTimes.TryRemove(entry.Key, out _);
                    evicted++;
                }
            }

            if (evicted > 0)
            {
                _logger.LogDebug("Evicted {Count} cache entries to free space", evicted);
            }
        }

        private void MaintenanceCallback(object? state)
        {
            try
            {
                _ = Task.Run(async () => await PerformMaintenanceAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache maintenance");
            }
        }

        private static string GenerateKey(string fileId, string checksum)
        {
            return $"{fileId}:{checksum}";
        }

        private static long EstimateSize(CachedSymbolData symbolData)
        {
            // Rough estimation of memory usage
            const int baseSize = 200; // Base object overhead
            const int stringOverhead = 50; // Average string overhead
            const int nodeSize = 300; // Estimated size per node
            const int relationshipSize = 150; // Estimated size per relationship

            var size = baseSize;
            size += symbolData.FileId.Length + stringOverhead;
            size += symbolData.Checksum.Length + stringOverhead;
            size += symbolData.RepositoryId.Length + stringOverhead;
            size += symbolData.Nodes.Count * nodeSize;
            size += symbolData.Relationships.Count * relationshipSize;
            size += symbolData.Metadata.Count * 100; // Rough metadata size

            return size;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _maintenanceTimer?.Dispose();
                _maintenanceLock?.Dispose();
                _disposed = true;
                _logger.LogInformation("Symbol cache disposed");
            }
        }
    }
}