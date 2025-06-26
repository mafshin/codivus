using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Codivus.Graph.Interfaces;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// Service for managing connection pools to graph databases
    /// </summary>
    public class ConnectionPoolManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, ConnectionPool> _pools;
        private readonly ConnectionPoolOptions _options;
        private readonly ILogger<ConnectionPoolManager> _logger;
        private readonly Timer _maintenanceTimer;
        private bool _disposed;

        public ConnectionPoolManager(
            IOptions<ConnectionPoolOptions> options,
            ILogger<ConnectionPoolManager> logger)
        {
            _options = options.Value;
            _logger = logger;
            _pools = new ConcurrentDictionary<string, ConnectionPool>();

            // Start maintenance timer
            _maintenanceTimer = new Timer(PerformMaintenance, null,
                _options.MaintenanceInterval, _options.MaintenanceInterval);

            _logger.LogInformation("Connection pool manager initialized with max pools {MaxPools}",
                _options.MaxPoolsPerType);
        }

        public async Task<PooledConnection> AcquireConnectionAsync(string poolKey, string connectionString, CancellationToken cancellationToken = default)
        {
            var pool = _pools.GetOrAdd(poolKey, key => new ConnectionPool(key, connectionString, _options, _logger));
            return await pool.AcquireConnectionAsync(cancellationToken);
        }

        public async Task ReleaseConnectionAsync(PooledConnection connection)
        {
            if (_pools.TryGetValue(connection.PoolKey, out var pool))
            {
                await pool.ReleaseConnectionAsync(connection);
            }
        }

        public async Task<ConnectionPoolStatistics> GetStatisticsAsync()
        {
            var stats = new ConnectionPoolStatistics
            {
                TotalPools = _pools.Count,
                CollectedAt = DateTime.UtcNow
            };

            foreach (var pool in _pools.Values)
            {
                var poolStats = await pool.GetStatisticsAsync();
                stats.PoolStatistics[pool.PoolKey] = poolStats;
                stats.TotalConnections += poolStats.TotalConnections;
                stats.ActiveConnections += poolStats.ActiveConnections;
                stats.IdleConnections += poolStats.IdleConnections;
            }

            return stats;
        }

        public async Task ClosePoolAsync(string poolKey)
        {
            if (_pools.TryRemove(poolKey, out var pool))
            {
                await pool.CloseAsync();
                _logger.LogInformation("Closed connection pool {PoolKey}", poolKey);
            }
        }

        private void PerformMaintenance(object? state)
        {
            try
            {
                _ = Task.Run(async () =>
                {
                    foreach (var pool in _pools.Values.ToList())
                    {
                        await pool.PerformMaintenanceAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection pool maintenance");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _maintenanceTimer?.Dispose();

                var closeTasks = _pools.Values.Select(pool => pool.CloseAsync()).ToArray();
                try
                {
                    Task.WaitAll(closeTasks, TimeSpan.FromSeconds(30));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing connection pools during disposal");
                }

                _pools.Clear();
                _disposed = true;
                _logger.LogInformation("Connection pool manager disposed");
            }
        }
    }

    /// <summary>
    /// Individual connection pool for a specific database/connection type
    /// </summary>
    public class ConnectionPool : IDisposable
    {
        private readonly ConcurrentQueue<PooledConnection> _availableConnections;
        private readonly ConcurrentDictionary<string, PooledConnection> _activeConnections;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly ConnectionPoolOptions _options;
        private readonly ILogger _logger;
        private readonly object _statsLock = new object();
        
        public string PoolKey { get; }
        public string ConnectionString { get; }
        
        private int _totalConnectionsCreated;
        private int _totalConnectionsDestroyed;
        private DateTime _lastMaintenanceTime;
        private bool _disposed;

        public ConnectionPool(string poolKey, string connectionString, ConnectionPoolOptions options, ILogger logger)
        {
            PoolKey = poolKey;
            ConnectionString = connectionString;
            _options = options;
            _logger = logger;
            
            _availableConnections = new ConcurrentQueue<PooledConnection>();
            _activeConnections = new ConcurrentDictionary<string, PooledConnection>();
            _connectionSemaphore = new SemaphoreSlim(_options.MaxConnectionsPerPool, _options.MaxConnectionsPerPool);
            _lastMaintenanceTime = DateTime.UtcNow;

            // Pre-create minimum connections
            _ = Task.Run(async () => await EnsureMinimumConnectionsAsync());
        }

        public async Task<PooledConnection> AcquireConnectionAsync(CancellationToken cancellationToken = default)
        {
            await _connectionSemaphore.WaitAsync(cancellationToken);

            try
            {
                // Try to get an available connection
                if (_availableConnections.TryDequeue(out var connection))
                {
                    // Validate connection
                    if (await ValidateConnectionAsync(connection))
                    {
                        connection.AcquiredAt = DateTime.UtcNow;
                        connection.IsActive = true;
                        _activeConnections[connection.ConnectionId] = connection;
                        
                        _logger.LogDebug("Reused connection {ConnectionId} from pool {PoolKey}", 
                            connection.ConnectionId, PoolKey);
                        return connection;
                    }
                    else
                    {
                        // Connection is invalid, destroy it
                        await DestroyConnectionAsync(connection);
                    }
                }

                // Create new connection
                var newConnection = await CreateConnectionAsync();
                newConnection.AcquiredAt = DateTime.UtcNow;
                newConnection.IsActive = true;
                _activeConnections[newConnection.ConnectionId] = newConnection;

                _logger.LogDebug("Created new connection {ConnectionId} for pool {PoolKey}", 
                    newConnection.ConnectionId, PoolKey);
                return newConnection;
            }
            catch
            {
                _connectionSemaphore.Release();
                throw;
            }
        }

        public async Task ReleaseConnectionAsync(PooledConnection connection)
        {
            if (!connection.IsActive || _disposed)
            {
                return;
            }

            if (_activeConnections.TryRemove(connection.ConnectionId, out var activeConnection))
            {
                connection.IsActive = false;
                connection.ReleasedAt = DateTime.UtcNow;
                connection.UsageCount++;

                // Check if connection should be destroyed
                if (ShouldDestroyConnection(connection))
                {
                    await DestroyConnectionAsync(connection);
                }
                else
                {
                    // Return to pool
                    _availableConnections.Enqueue(connection);
                    _logger.LogDebug("Returned connection {ConnectionId} to pool {PoolKey}", 
                        connection.ConnectionId, PoolKey);
                }

                _connectionSemaphore.Release();
            }
        }

        public async Task<IndividualPoolStatistics> GetStatisticsAsync()
        {
            lock (_statsLock)
            {
                return new IndividualPoolStatistics
                {
                    PoolKey = PoolKey,
                    TotalConnections = _totalConnectionsCreated - _totalConnectionsDestroyed,
                    ActiveConnections = _activeConnections.Count,
                    IdleConnections = _availableConnections.Count,
                    MaxConnections = _options.MaxConnectionsPerPool,
                    MinConnections = _options.MinConnectionsPerPool,
                    TotalConnectionsCreated = _totalConnectionsCreated,
                    TotalConnectionsDestroyed = _totalConnectionsDestroyed,
                    LastMaintenanceTime = _lastMaintenanceTime,
                    AverageConnectionAge = CalculateAverageConnectionAge()
                };
            }
        }

        public async Task PerformMaintenanceAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var connectionsToDestroy = new List<PooledConnection>();

                // Check idle connections for expiration
                var tempConnections = new List<PooledConnection>();
                while (_availableConnections.TryDequeue(out var connection))
                {
                    if (now - connection.ReleasedAt > _options.MaxConnectionAge ||
                        !await ValidateConnectionAsync(connection))
                    {
                        connectionsToDestroy.Add(connection);
                    }
                    else
                    {
                        tempConnections.Add(connection);
                    }
                }

                // Re-queue valid connections
                foreach (var connection in tempConnections)
                {
                    _availableConnections.Enqueue(connection);
                }

                // Destroy expired/invalid connections
                foreach (var connection in connectionsToDestroy)
                {
                    await DestroyConnectionAsync(connection);
                }

                // Ensure minimum connections
                await EnsureMinimumConnectionsAsync();

                _lastMaintenanceTime = now;

                if (connectionsToDestroy.Count > 0)
                {
                    _logger.LogDebug("Pool maintenance for {PoolKey}: destroyed {Count} expired connections", 
                        PoolKey, connectionsToDestroy.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during maintenance for pool {PoolKey}", PoolKey);
            }
        }

        public async Task CloseAsync()
        {
            _disposed = true;

            // Close all active connections
            var activeTasks = _activeConnections.Values.Select(DestroyConnectionAsync).ToArray();
            await Task.WhenAll(activeTasks);

            // Close all idle connections
            var idleTasks = new List<Task>();
            while (_availableConnections.TryDequeue(out var connection))
            {
                idleTasks.Add(DestroyConnectionAsync(connection));
            }
            await Task.WhenAll(idleTasks);

            _connectionSemaphore.Dispose();
            _logger.LogInformation("Closed connection pool {PoolKey}", PoolKey);
        }

        private async Task<PooledConnection> CreateConnectionAsync()
        {
            var connection = new PooledConnection
            {
                ConnectionId = Guid.NewGuid().ToString(),
                PoolKey = PoolKey,
                CreatedAt = DateTime.UtcNow,
                ConnectionString = ConnectionString
            };

            // Create actual database connection
            connection.UnderlyingConnection = await CreateDatabaseConnectionAsync(ConnectionString);

            lock (_statsLock)
            {
                _totalConnectionsCreated++;
            }

            return connection;
        }

        private async Task<object> CreateDatabaseConnectionAsync(string connectionString)
        {
            // This would create the actual database connection
            // For now, return a placeholder object
            return new object();
        }

        private async Task<bool> ValidateConnectionAsync(PooledConnection connection)
        {
            try
            {
                // This would test if the connection is still valid
                // For now, assume valid if not too old
                return DateTime.UtcNow - connection.CreatedAt < _options.MaxConnectionAge;
            }
            catch
            {
                return false;
            }
        }

        private async Task DestroyConnectionAsync(PooledConnection connection)
        {
            try
            {
                // Close underlying connection
                if (connection.UnderlyingConnection is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                lock (_statsLock)
                {
                    _totalConnectionsDestroyed++;
                }

                _logger.LogDebug("Destroyed connection {ConnectionId} from pool {PoolKey}", 
                    connection.ConnectionId, PoolKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error destroying connection {ConnectionId}", connection.ConnectionId);
            }
        }

        private bool ShouldDestroyConnection(PooledConnection connection)
        {
            var now = DateTime.UtcNow;
            
            // Destroy if too old
            if (now - connection.CreatedAt > _options.MaxConnectionAge)
                return true;

            // Destroy if used too many times
            if (connection.UsageCount > _options.MaxUsageCount)
                return true;

            // Destroy if we're over the maximum pool size
            if (_availableConnections.Count + _activeConnections.Count > _options.MaxConnectionsPerPool)
                return true;

            return false;
        }

        private async Task EnsureMinimumConnectionsAsync()
        {
            var currentTotal = _availableConnections.Count + _activeConnections.Count;
            var needed = _options.MinConnectionsPerPool - currentTotal;

            for (int i = 0; i < needed; i++)
            {
                try
                {
                    if (await _connectionSemaphore.WaitAsync(100))
                    {
                        var connection = await CreateConnectionAsync();
                        _availableConnections.Enqueue(connection);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating minimum connection for pool {PoolKey}", PoolKey);
                    break;
                }
            }
        }

        private TimeSpan CalculateAverageConnectionAge()
        {
            var now = DateTime.UtcNow;
            var allConnections = _activeConnections.Values.Concat(_availableConnections.ToArray());
            
            if (!allConnections.Any())
                return TimeSpan.Zero;

            var totalAge = allConnections.Sum(c => (now - c.CreatedAt).TotalSeconds);
            return TimeSpan.FromSeconds(totalAge / allConnections.Count());
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _ = Task.Run(async () => await CloseAsync());
            }
        }
    }

    /// <summary>
    /// Represents a pooled database connection
    /// </summary>
    public class PooledConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string PoolKey { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime AcquiredAt { get; set; }
        public DateTime ReleasedAt { get; set; }
        public bool IsActive { get; set; }
        public int UsageCount { get; set; }
        public object? UnderlyingConnection { get; set; }
    }

    /// <summary>
    /// Configuration options for connection pools
    /// </summary>
    public class ConnectionPoolOptions
    {
        public int MaxConnectionsPerPool { get; set; } = 20;
        public int MinConnectionsPerPool { get; set; } = 2;
        public int MaxPoolsPerType { get; set; } = 10;
        public TimeSpan MaxConnectionAge { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxUsageCount { get; set; } = 1000;
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Statistics for all connection pools
    /// </summary>
    public class ConnectionPoolStatistics
    {
        public int TotalPools { get; set; }
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public DateTime CollectedAt { get; set; }
        public Dictionary<string, IndividualPoolStatistics> PoolStatistics { get; set; } = new();
    }

    /// <summary>
    /// Statistics for an individual connection pool
    /// </summary>
    public class IndividualPoolStatistics
    {
        public string PoolKey { get; set; } = string.Empty;
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int MaxConnections { get; set; }
        public int MinConnections { get; set; }
        public int TotalConnectionsCreated { get; set; }
        public int TotalConnectionsDestroyed { get; set; }
        public DateTime LastMaintenanceTime { get; set; }
        public TimeSpan AverageConnectionAge { get; set; }
        public double UtilizationPercent => MaxConnections > 0 ? (double)TotalConnections / MaxConnections * 100 : 0;
    }
}