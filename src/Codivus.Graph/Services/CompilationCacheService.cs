using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.CodeAnalysis;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// In-memory compilation cache implementation for Roslyn compilations
    /// </summary>
    public class CompilationCacheService : ICompilationCache, IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheEntry<CachedCompilation>> _cache;
        private readonly ConcurrentDictionary<string, DateTime> _accessTimes;
        private readonly CacheOptions _options;
        private readonly ILogger<CompilationCacheService> _logger;
        private readonly Timer _maintenanceTimer;
        private readonly SemaphoreSlim _maintenanceLock;
        private long _hitCount;
        private long _missCount;
        private bool _disposed;

        public CompilationCacheService(
            IOptions<CacheOptions> options,
            ILogger<CompilationCacheService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _cache = new ConcurrentDictionary<string, CacheEntry<CachedCompilation>>();
            _accessTimes = new ConcurrentDictionary<string, DateTime>();
            _maintenanceLock = new SemaphoreSlim(1, 1);
            
            // Start maintenance timer
            _maintenanceTimer = new Timer(
                MaintenanceCallback,
                null,
                _options.MaintenanceInterval,
                _options.MaintenanceInterval);

            _logger.LogInformation("Compilation cache initialized with max size {MaxSize}MB, max entries {MaxEntries}", 
                _options.MaxSizeBytes / (1024 * 1024), _options.MaxEntries);
        }

        public async Task<CachedCompilation?> GetCompilationAsync(string projectId, string checksum)
        {
            var key = GenerateKey(projectId, checksum);
            
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    _accessTimes.TryRemove(key, out _);
                    Interlocked.Increment(ref _missCount);
                    _logger.LogDebug("Cache entry expired for project {ProjectId}", projectId);
                    return null;
                }

                // Update access time
                entry.LastAccessed = DateTime.UtcNow;
                entry.AccessCount++;
                _accessTimes[key] = entry.LastAccessed;
                
                Interlocked.Increment(ref _hitCount);
                _logger.LogDebug("Cache hit for project {ProjectId}", projectId);
                return entry.Data;
            }

            Interlocked.Increment(ref _missCount);
            _logger.LogDebug("Cache miss for project {ProjectId}", projectId);
            return null;
        }

        public async Task SetCompilationAsync(string projectId, string checksum, CachedCompilation compilation, TimeSpan? expiration = null)
        {
            var key = GenerateKey(projectId, checksum);
            var now = DateTime.UtcNow;
            var expiresAt = expiration.HasValue ? now.Add(expiration.Value) : now.Add(_options.DefaultExpiration);
            
            var entry = new CacheEntry<CachedCompilation>
            {
                Key = key,
                Data = compilation,
                CachedAt = now,
                LastAccessed = now,
                ExpiresAt = expiresAt,
                SizeInBytes = EstimateSize(compilation),
                AccessCount = 1
            };

            // Check if we need to evict entries before adding
            await EnsureCapacityAsync();

            _cache[key] = entry;
            _accessTimes[key] = now;
            
            _logger.LogDebug("Cached compilation for project {ProjectId}, size {Size} bytes", projectId, entry.SizeInBytes);
        }

        public async Task RemoveCompilationAsync(string projectId)
        {
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith($"{projectId}:")).ToList();
            
            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
                _accessTimes.TryRemove(key, out _);
            }

            _logger.LogDebug("Removed {Count} cache entries for project {ProjectId}", keysToRemove.Count, projectId);
        }

        public async Task<CacheStatistics> GetStatisticsAsync()
        {
            var totalSize = _cache.Values.Sum(e => e.SizeInBytes);
            var expiredCount = _cache.Values.Count(e => e.IsExpired);

            return new CacheStatistics
            {
                CacheType = "CompilationCache",
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
                    ["CapacityPercent"] = Math.Round((double)_cache.Count / _options.MaxEntries * 100, 2),
                    ["AverageCompilationSize"] = _cache.Count > 0 ? totalSize / _cache.Count : 0
                }
            };
        }

        public async Task PerformMaintenanceAsync(CancellationToken cancellationToken = default)
        {
            if (!await _maintenanceLock.WaitAsync(100, cancellationToken))
                return;

            try
            {
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
                    _logger.LogDebug("Compilation cache maintenance removed {Count} expired entries", removed);
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
                _logger.LogDebug("Evicted {Count} compilation cache entries to free space", evicted);
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
                _logger.LogError(ex, "Error during compilation cache maintenance");
            }
        }

        private static string GenerateKey(string projectId, string checksum)
        {
            return $"{projectId}:{checksum}";
        }

        private static long EstimateSize(CachedCompilation compilation)
        {
            // Rough estimation of memory usage
            const int baseSize = 500; // Base object overhead
            const int stringOverhead = 50; // Average string overhead
            
            var size = baseSize;
            size += compilation.ProjectId.Length + stringOverhead;
            size += compilation.Checksum.Length + stringOverhead;
            size += compilation.SerializedCompilation.Length; // Actual serialized data size
            size += compilation.Dependencies.Sum(d => d.Length + stringOverhead);
            size += compilation.MetadataReferences.Sum(kvp => kvp.Key.Length + kvp.Value.Length + stringOverhead * 2);

            return size;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _maintenanceTimer?.Dispose();
                _maintenanceLock?.Dispose();
                _disposed = true;
                _logger.LogInformation("Compilation cache disposed");
            }
        }
    }
}