using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// In-memory query cache implementation for graph queries
    /// </summary>
    public class QueryCacheService : IQueryCache, IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheEntry<object>> _cache;
        private readonly ConcurrentDictionary<string, DateTime> _accessTimes;
        private readonly CacheOptions _options;
        private readonly ILogger<QueryCacheService> _logger;
        private readonly Timer _maintenanceTimer;
        private readonly SemaphoreSlim _maintenanceLock;
        private long _hitCount;
        private long _missCount;
        private bool _disposed;

        public QueryCacheService(
            IOptions<CacheOptions> options,
            ILogger<QueryCacheService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _cache = new ConcurrentDictionary<string, CacheEntry<object>>();
            _accessTimes = new ConcurrentDictionary<string, DateTime>();
            _maintenanceLock = new SemaphoreSlim(1, 1);
            
            // Start maintenance timer
            _maintenanceTimer = new Timer(
                MaintenanceCallback,
                null,
                _options.MaintenanceInterval,
                _options.MaintenanceInterval);

            _logger.LogInformation("Query cache initialized with max size {MaxSize}MB, max entries {MaxEntries}", 
                _options.MaxSizeBytes / (1024 * 1024), _options.MaxEntries);
        }

        public async Task<T?> GetAsync<T>(string queryKey) where T : class
        {
            if (_cache.TryGetValue(queryKey, out var entry))
            {
                if (entry.IsExpired)
                {
                    _cache.TryRemove(queryKey, out _);
                    _accessTimes.TryRemove(queryKey, out _);
                    Interlocked.Increment(ref _missCount);
                    _logger.LogDebug("Cache entry expired for query key {QueryKey}", queryKey);
                    return null;
                }

                // Update access time
                entry.LastAccessed = DateTime.UtcNow;
                entry.AccessCount++;
                _accessTimes[queryKey] = entry.LastAccessed;
                
                Interlocked.Increment(ref _hitCount);
                _logger.LogDebug("Cache hit for query key {QueryKey}", queryKey);

                // Deserialize if needed
                if (entry.Data is T directResult)
                {
                    return directResult;
                }

                if (entry.Data is string jsonData)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<T>(jsonData);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize cached query result for key {QueryKey}", queryKey);
                        _cache.TryRemove(queryKey, out _);
                        _accessTimes.TryRemove(queryKey, out _);
                        return null;
                    }
                }
            }

            Interlocked.Increment(ref _missCount);
            _logger.LogDebug("Cache miss for query key {QueryKey}", queryKey);
            return null;
        }

        public async Task SetAsync<T>(string queryKey, T result, TimeSpan? expiration = null) where T : class
        {
            var now = DateTime.UtcNow;
            var expiresAt = expiration.HasValue ? now.Add(expiration.Value) : now.Add(_options.DefaultExpiration);
            
            // Serialize for storage
            object dataToStore = result;
            long sizeEstimate = 0;

            try
            {
                var jsonData = JsonSerializer.Serialize(result);
                dataToStore = jsonData;
                sizeEstimate = EstimateSize(jsonData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize query result for caching, storing as object reference");
                sizeEstimate = EstimateObjectSize(result);
            }

            var entry = new CacheEntry<object>
            {
                Key = queryKey,
                Data = dataToStore,
                CachedAt = now,
                LastAccessed = now,
                ExpiresAt = expiresAt,
                SizeInBytes = sizeEstimate,
                AccessCount = 1
            };

            // Check if we need to evict entries before adding
            await EnsureCapacityAsync();

            _cache[queryKey] = entry;
            _accessTimes[queryKey] = now;
            
            _logger.LogDebug("Cached query result for key {QueryKey}, size {Size} bytes", queryKey, entry.SizeInBytes);
        }

        public async Task RemoveAsync(string queryKey)
        {
            if (_cache.TryRemove(queryKey, out _))
            {
                _accessTimes.TryRemove(queryKey, out _);
                _logger.LogDebug("Removed cache entry for query key {QueryKey}", queryKey);
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var keysToRemove = _cache.Keys.Where(k => regex.IsMatch(k)).ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
                _accessTimes.TryRemove(key, out _);
            }

            _logger.LogDebug("Removed {Count} cache entries matching pattern {Pattern}", keysToRemove.Count, pattern);
        }

        public async Task ClearAsync()
        {
            var count = _cache.Count;
            _cache.Clear();
            _accessTimes.Clear();
            
            _logger.LogInformation("Cleared all {Count} cache entries", count);
        }

        public async Task<CacheStatistics> GetStatisticsAsync()
        {
            var totalSize = _cache.Values.Sum(e => e.SizeInBytes);
            var expiredCount = _cache.Values.Count(e => e.IsExpired);

            return new CacheStatistics
            {
                CacheType = "QueryCache",
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
                _logger.LogError(ex, "Error during query cache maintenance");
            }
        }

        private async Task PerformMaintenanceAsync()
        {
            if (!await _maintenanceLock.WaitAsync(100))
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
                    _logger.LogDebug("Query cache maintenance removed {Count} expired entries", removed);
                }
            }
            finally
            {
                _maintenanceLock.Release();
            }
        }

        private static long EstimateSize(string jsonData)
        {
            return jsonData.Length * 2; // Rough estimation: 2 bytes per character in UTF-16
        }

        private static long EstimateObjectSize(object obj)
        {
            // Very rough estimation for object reference storage
            return 1024; // Default 1KB estimation
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _maintenanceTimer?.Dispose();
                _maintenanceLock?.Dispose();
                _disposed = true;
                _logger.LogInformation("Query cache disposed");
            }
        }
    }
}