using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;

namespace Codivus.Graph.Interfaces
{
    /// <summary>
    /// Interface for caching extracted symbols to improve performance
    /// </summary>
    public interface ISymbolCache
    {
        /// <summary>
        /// Gets cached symbols for a file
        /// </summary>
        Task<CachedSymbolData?> GetSymbolsAsync(string fileId, string checksum);

        /// <summary>
        /// Stores symbols for a file with checksum
        /// </summary>
        Task SetSymbolsAsync(string fileId, string checksum, CachedSymbolData symbolData, TimeSpan? expiration = null);

        /// <summary>
        /// Removes cached symbols for a file
        /// </summary>
        Task RemoveSymbolsAsync(string fileId);

        /// <summary>
        /// Clears all cached symbols for a repository
        /// </summary>
        Task ClearRepositoryAsync(string repositoryId);

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync();

        /// <summary>
        /// Performs cache maintenance (cleanup expired entries)
        /// </summary>
        Task PerformMaintenanceAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for caching graph queries
    /// </summary>
    public interface IQueryCache
    {
        /// <summary>
        /// Gets cached query result
        /// </summary>
        Task<T?> GetAsync<T>(string queryKey) where T : class;

        /// <summary>
        /// Stores query result
        /// </summary>
        Task SetAsync<T>(string queryKey, T result, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Removes cached query result
        /// </summary>
        Task RemoveAsync(string queryKey);

        /// <summary>
        /// Removes cached queries matching pattern
        /// </summary>
        Task RemoveByPatternAsync(string pattern);

        /// <summary>
        /// Clears all cached queries
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// Interface for caching Roslyn compilations
    /// </summary>
    public interface ICompilationCache
    {
        /// <summary>
        /// Gets cached compilation
        /// </summary>
        Task<CachedCompilation?> GetCompilationAsync(string projectId, string checksum);

        /// <summary>
        /// Stores compilation
        /// </summary>
        Task SetCompilationAsync(string projectId, string checksum, CachedCompilation compilation, TimeSpan? expiration = null);

        /// <summary>
        /// Removes cached compilation
        /// </summary>
        Task RemoveCompilationAsync(string projectId);

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync();

        /// <summary>
        /// Performs cache maintenance
        /// </summary>
        Task PerformMaintenanceAsync(CancellationToken cancellationToken = default);
    }
}