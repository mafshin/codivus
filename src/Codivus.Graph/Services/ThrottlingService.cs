using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// Service for throttling and rate limiting processing operations
    /// </summary>
    public class ThrottlingService : IDisposable
    {
        private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters;
        private readonly ConcurrentDictionary<string, PriorityQueue<ThrottledRequest, int>> _requestQueues;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _queueSemaphores;
        private readonly ThrottlingOptions _options;
        private readonly ILogger<ThrottlingService> _logger;
        private readonly Timer _maintenanceTimer;
        private bool _disposed;

        public ThrottlingService(
            IOptions<ThrottlingOptions> options,
            ILogger<ThrottlingService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _rateLimiters = new ConcurrentDictionary<string, RateLimiter>();
            _requestQueues = new ConcurrentDictionary<string, PriorityQueue<ThrottledRequest, int>>();
            _queueSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

            // Start maintenance timer
            _maintenanceTimer = new Timer(PerformMaintenance, null,
                _options.MaintenanceInterval, _options.MaintenanceInterval);

            _logger.LogInformation("Throttling service initialized with {LimiterCount} rate limiters", 
                _options.RateLimits.Count);
        }

        public async Task<ThrottleToken> AcquireTokenAsync(string operationType, int priority = 0, CancellationToken cancellationToken = default)
        {
            var rateLimiter = GetOrCreateRateLimiter(operationType);
            
            // Check if we can proceed immediately
            if (await rateLimiter.TryAcquireAsync())
            {
                return new ThrottleToken
                {
                    TokenId = Guid.NewGuid().ToString(),
                    OperationType = operationType,
                    AcquiredAt = DateTime.UtcNow,
                    Priority = priority,
                    IsAcquired = true
                };
            }

            // Need to queue the request
            return await QueueRequestAsync(operationType, priority, cancellationToken);
        }

        public async Task ReleaseTokenAsync(ThrottleToken token)
        {
            if (!token.IsAcquired)
                return;

            var rateLimiter = GetOrCreateRateLimiter(token.OperationType);
            await rateLimiter.ReleaseAsync();

            token.IsAcquired = false;
            token.ReleasedAt = DateTime.UtcNow;

            // Process next queued request
            await ProcessQueuedRequestsAsync(token.OperationType);

            _logger.LogDebug("Released throttle token {TokenId} for operation {OperationType}", 
                token.TokenId, token.OperationType);
        }

        public async Task<ThrottlingStatistics> GetStatisticsAsync()
        {
            var stats = new ThrottlingStatistics
            {
                CollectedAt = DateTime.UtcNow
            };

            foreach (var kvp in _rateLimiters)
            {
                var limiterStats = await kvp.Value.GetStatisticsAsync();
                stats.LimiterStatistics[kvp.Key] = limiterStats;
                stats.TotalRequestsProcessed += limiterStats.TotalRequests;
                stats.TotalRequestsThrottled += limiterStats.ThrottledRequests;
            }

            // Calculate queue statistics
            foreach (var kvp in _requestQueues)
            {
                stats.QueuedRequestsByType[kvp.Key] = kvp.Value.Count;
                stats.TotalQueuedRequests += kvp.Value.Count;
            }

            return stats;
        }

        public async Task UpdateLimitsAsync(string operationType, RateLimit newLimit)
        {
            if (_rateLimiters.TryGetValue(operationType, out var rateLimiter))
            {
                await rateLimiter.UpdateLimitAsync(newLimit);
                _logger.LogInformation("Updated rate limit for {OperationType}: {RequestsPerSecond} requests/sec, burst {BurstSize}", 
                    operationType, newLimit.RequestsPerSecond, newLimit.BurstSize);
            }
        }

        private RateLimiter GetOrCreateRateLimiter(string operationType)
        {
            return _rateLimiters.GetOrAdd(operationType, key =>
            {
                var limit = _options.RateLimits.GetValueOrDefault(key, _options.DefaultRateLimit);
                var limiter = new RateLimiter(key, limit, _logger);
                
                // Also create queue and semaphore
                _requestQueues.TryAdd(key, new PriorityQueue<ThrottledRequest, int>());
                _queueSemaphores.TryAdd(key, new SemaphoreSlim(1, 1));
                
                return limiter;
            });
        }

        private async Task<ThrottleToken> QueueRequestAsync(string operationType, int priority, CancellationToken cancellationToken)
        {
            var request = new ThrottledRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                OperationType = operationType,
                Priority = priority,
                QueuedAt = DateTime.UtcNow,
                CompletionSource = new TaskCompletionSource<ThrottleToken>()
            };

            var queue = _requestQueues.GetOrAdd(operationType, _ => new PriorityQueue<ThrottledRequest, int>());
            var semaphore = _queueSemaphores.GetOrAdd(operationType, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Use negative priority for max-heap behavior (higher priority first)
                queue.Enqueue(request, -priority);
                _logger.LogDebug("Queued request {RequestId} for operation {OperationType} with priority {Priority}", 
                    request.RequestId, operationType, priority);
            }
            finally
            {
                semaphore.Release();
            }

            // Register cancellation
            cancellationToken.Register(() =>
            {
                request.CompletionSource.TrySetCanceled();
            });

            return await request.CompletionSource.Task;
        }

        private async Task ProcessQueuedRequestsAsync(string operationType)
        {
            if (!_requestQueues.TryGetValue(operationType, out var queue) ||
                !_queueSemaphores.TryGetValue(operationType, out var semaphore))
            {
                return;
            }

            await semaphore.WaitAsync();
            try
            {
                if (queue.Count == 0)
                    return;

                var rateLimiter = GetOrCreateRateLimiter(operationType);
                
                // Process as many requests as the rate limiter allows
                while (queue.Count > 0 && await rateLimiter.TryAcquireAsync())
                {
                    var request = queue.Dequeue();
                    
                    var token = new ThrottleToken
                    {
                        TokenId = request.RequestId,
                        OperationType = operationType,
                        AcquiredAt = DateTime.UtcNow,
                        Priority = request.Priority,
                        IsAcquired = true,
                        QueueWaitTime = DateTime.UtcNow - request.QueuedAt
                    };

                    request.CompletionSource.TrySetResult(token);
                    
                    _logger.LogDebug("Processed queued request {RequestId} for operation {OperationType} after {WaitTime}ms", 
                        request.RequestId, operationType, token.QueueWaitTime.TotalMilliseconds);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void PerformMaintenance(object? state)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    // Clean up expired requests from queues
                    var cutoff = DateTime.UtcNow - _options.MaxQueueWaitTime;
                    var expiredCount = 0;

                    foreach (var kvp in _requestQueues)
                    {
                        var queue = kvp.Value;
                        var semaphore = _queueSemaphores[kvp.Key];

                        await semaphore.WaitAsync();
                        try
                        {
                            var tempRequests = new List<(ThrottledRequest request, int priority)>();
                            
                            // Extract all requests
                            while (queue.Count > 0)
                            {
                                var request = queue.Dequeue();
                                if (request.QueuedAt > cutoff)
                                {
                                    tempRequests.Add((request, -request.Priority));
                                }
                                else
                                {
                                    // Cancel expired request
                                    request.CompletionSource.TrySetException(
                                        new TimeoutException("Request expired in queue"));
                                    expiredCount++;
                                }
                            }

                            // Re-queue non-expired requests
                            foreach (var (request, priority) in tempRequests)
                            {
                                queue.Enqueue(request, priority);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }

                    if (expiredCount > 0)
                    {
                        _logger.LogInformation("Throttling maintenance: removed {ExpiredCount} expired requests from queues", 
                            expiredCount);
                    }

                    // Perform maintenance on rate limiters
                    foreach (var rateLimiter in _rateLimiters.Values)
                    {
                        await rateLimiter.PerformMaintenanceAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during throttling service maintenance");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _maintenanceTimer?.Dispose();

                // Cancel all queued requests
                foreach (var queue in _requestQueues.Values)
                {
                    while (queue.Count > 0)
                    {
                        var request = queue.Dequeue();
                        request.CompletionSource.TrySetCanceled();
                    }
                }

                // Dispose rate limiters
                foreach (var rateLimiter in _rateLimiters.Values)
                {
                    rateLimiter.Dispose();
                }

                // Dispose semaphores
                foreach (var semaphore in _queueSemaphores.Values)
                {
                    semaphore.Dispose();
                }

                _disposed = true;
                _logger.LogInformation("Throttling service disposed");
            }
        }
    }

    /// <summary>
    /// Token representing throttling permission
    /// </summary>
    public class ThrottleToken
    {
        public string TokenId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public DateTime AcquiredAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public int Priority { get; set; }
        public bool IsAcquired { get; set; }
        public TimeSpan QueueWaitTime { get; set; }
    }

    /// <summary>
    /// Queued throttling request
    /// </summary>
    internal class ThrottledRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public int Priority { get; set; }
        public DateTime QueuedAt { get; set; }
        public TaskCompletionSource<ThrottleToken> CompletionSource { get; set; } = null!;
    }

    /// <summary>
    /// Rate limiter for a specific operation type
    /// </summary>
    public class RateLimiter : IDisposable
    {
        private readonly string _operationType;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _refillTimer;
        private readonly object _statsLock = new object();
        
        private RateLimit _limit;
        private int _availableTokens;
        private long _totalRequests;
        private long _throttledRequests;
        private DateTime _lastRefill;
        private bool _disposed;

        public RateLimiter(string operationType, RateLimit limit, ILogger logger)
        {
            _operationType = operationType;
            _limit = limit;
            _logger = logger;
            _availableTokens = limit.BurstSize;
            _lastRefill = DateTime.UtcNow;
            
            _semaphore = new SemaphoreSlim(1, 1);
            
            // Calculate refill interval
            var refillInterval = TimeSpan.FromMilliseconds(1000.0 / limit.RequestsPerSecond);
            _refillTimer = new Timer(RefillTokens, null, refillInterval, refillInterval);
        }

        public async Task<bool> TryAcquireAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                lock (_statsLock)
                {
                    _totalRequests++;
                    
                    if (_availableTokens > 0)
                    {
                        _availableTokens--;
                        return true;
                    }
                    else
                    {
                        _throttledRequests++;
                        return false;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ReleaseAsync()
        {
            // For token bucket, release doesn't add tokens back
            // Tokens are added by the timer-based refill
        }

        public async Task UpdateLimitAsync(RateLimit newLimit)
        {
            await _semaphore.WaitAsync();
            try
            {
                _limit = newLimit;
                
                // Adjust available tokens if burst size changed
                if (_availableTokens > newLimit.BurstSize)
                {
                    _availableTokens = newLimit.BurstSize;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<RateLimiterStatistics> GetStatisticsAsync()
        {
            lock (_statsLock)
            {
                return new RateLimiterStatistics
                {
                    OperationType = _operationType,
                    TotalRequests = _totalRequests,
                    ThrottledRequests = _throttledRequests,
                    AvailableTokens = _availableTokens,
                    MaxTokens = _limit.BurstSize,
                    RequestsPerSecond = _limit.RequestsPerSecond,
                    ThrottleRate = _totalRequests > 0 ? (double)_throttledRequests / _totalRequests : 0,
                    LastRefillTime = _lastRefill
                };
            }
        }

        public async Task PerformMaintenanceAsync()
        {
            // Maintenance tasks for rate limiter
            // For now, just log statistics
            var stats = await GetStatisticsAsync();
            
            if (stats.TotalRequests > 0)
            {
                _logger.LogDebug("Rate limiter {OperationType}: {TotalRequests} requests, {ThrottleRate:P} throttled, {AvailableTokens}/{MaxTokens} tokens", 
                    _operationType, stats.TotalRequests, stats.ThrottleRate, stats.AvailableTokens, stats.MaxTokens);
            }
        }

        private void RefillTokens(object? state)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    await _semaphore.WaitAsync();
                    try
                    {
                        var now = DateTime.UtcNow;
                        var timeSinceLastRefill = now - _lastRefill;
                        
                        // Calculate tokens to add based on time elapsed
                        var tokensToAdd = (int)(timeSinceLastRefill.TotalSeconds * _limit.RequestsPerSecond);
                        
                        if (tokensToAdd > 0)
                        {
                            _availableTokens = Math.Min(_availableTokens + tokensToAdd, _limit.BurstSize);
                            _lastRefill = now;
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refill for {OperationType}", _operationType);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _refillTimer?.Dispose();
                _semaphore?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Rate limit configuration
    /// </summary>
    public class RateLimit
    {
        public double RequestsPerSecond { get; set; } = 10;
        public int BurstSize { get; set; } = 20;
    }

    /// <summary>
    /// Throttling service configuration
    /// </summary>
    public class ThrottlingOptions
    {
        public Dictionary<string, RateLimit> RateLimits { get; set; } = new();
        public RateLimit DefaultRateLimit { get; set; } = new() { RequestsPerSecond = 10, BurstSize = 20 };
        public TimeSpan MaxQueueWaitTime { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Throttling service statistics
    /// </summary>
    public class ThrottlingStatistics
    {
        public DateTime CollectedAt { get; set; }
        public long TotalRequestsProcessed { get; set; }
        public long TotalRequestsThrottled { get; set; }
        public int TotalQueuedRequests { get; set; }
        public Dictionary<string, RateLimiterStatistics> LimiterStatistics { get; set; } = new();
        public Dictionary<string, int> QueuedRequestsByType { get; set; } = new();
        public double OverallThrottleRate => TotalRequestsProcessed > 0 ? (double)TotalRequestsThrottled / TotalRequestsProcessed : 0;
    }

    /// <summary>
    /// Rate limiter statistics
    /// </summary>
    public class RateLimiterStatistics
    {
        public string OperationType { get; set; } = string.Empty;
        public long TotalRequests { get; set; }
        public long ThrottledRequests { get; set; }
        public int AvailableTokens { get; set; }
        public int MaxTokens { get; set; }
        public double RequestsPerSecond { get; set; }
        public double ThrottleRate { get; set; }
        public DateTime LastRefillTime { get; set; }
    }
}