using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codivus.Graph.Models;
using Codivus.Graph.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Codivus.Graph.Tests.Services
{
    public class CompilationCacheServiceTests : IDisposable
    {
        private readonly CompilationCacheService _compilationCacheService;
        private readonly Mock<ILogger<CompilationCacheService>> _mockLogger;
        private readonly CacheOptions _cacheOptions;

        public CompilationCacheServiceTests()
        {
            _mockLogger = new Mock<ILogger<CompilationCacheService>>();
            _cacheOptions = new CacheOptions
            {
                MaxSizeBytes = 1024 * 1024, // 1MB
                MaxEntries = 100,
                DefaultExpiration = TimeSpan.FromHours(1),
                MaintenanceInterval = TimeSpan.FromMilliseconds(100),
                EvictionThreshold = 0.8
            };

            var options = Options.Create(_cacheOptions);
            _compilationCacheService = new CompilationCacheService(options, _mockLogger.Object);
        }

        [Fact]
        public async Task GetCompilationAsync_WhenCacheEmpty_ShouldReturnNull()
        {
            // Act
            var result = await _compilationCacheService.GetCompilationAsync("project1", "checksum1");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetCompilationAsync_ShouldStoreCachedCompilation()
        {
            // Arrange
            var compilation = CreateTestCompilation("project1", "checksum1");

            // Act
            await _compilationCacheService.SetCompilationAsync("project1", "checksum1", compilation);

            // Assert
            var result = await _compilationCacheService.GetCompilationAsync("project1", "checksum1");
            result.Should().NotBeNull();
            result!.ProjectId.Should().Be("project1");
            result.Checksum.Should().Be("checksum1");
        }

        [Fact]
        public async Task GetCompilationAsync_AfterSet_ShouldReturnStoredData()
        {
            // Arrange
            var compilation = CreateTestCompilation("project2", "checksum2");
            await _compilationCacheService.SetCompilationAsync("project2", "checksum2", compilation);

            // Act
            var result = await _compilationCacheService.GetCompilationAsync("project2", "checksum2");

            // Assert
            result.Should().NotBeNull();
            result!.ProjectId.Should().Be("project2");
            result.Checksum.Should().Be("checksum2");
            System.Text.Encoding.UTF8.GetString(result.SerializedCompilation).Should().Be("serialized_compilation_data");
            result.Dependencies.Should().HaveCount(2);
            result.MetadataReferences.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetCompilationAsync_WithDifferentChecksum_ShouldReturnNull()
        {
            // Arrange
            var compilation = CreateTestCompilation("project3", "checksum3");
            await _compilationCacheService.SetCompilationAsync("project3", "checksum3", compilation);

            // Act
            var result = await _compilationCacheService.GetCompilationAsync("project3", "different-checksum");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetCompilationAsync_WithExpiration_ShouldExpireAfterTime()
        {
            // Arrange
            var compilation = CreateTestCompilation("project4", "checksum4");
            var shortExpiration = TimeSpan.FromMilliseconds(50);

            // Act
            await _compilationCacheService.SetCompilationAsync("project4", "checksum4", compilation, shortExpiration);
            
            // Verify it's there initially
            var result1 = await _compilationCacheService.GetCompilationAsync("project4", "checksum4");
            result1.Should().NotBeNull();

            // Wait for expiration
            await Task.Delay(100);

            // Should be expired now
            var result2 = await _compilationCacheService.GetCompilationAsync("project4", "checksum4");
            result2.Should().BeNull();
        }

        [Fact]
        public async Task RemoveCompilationAsync_ShouldRemoveAllEntriesForProject()
        {
            // Arrange
            var compilation1 = CreateTestCompilation("project5", "checksum5a");
            var compilation2 = CreateTestCompilation("project5", "checksum5b");
            var compilation3 = CreateTestCompilation("project6", "checksum6");
            
            await _compilationCacheService.SetCompilationAsync("project5", "checksum5a", compilation1);
            await _compilationCacheService.SetCompilationAsync("project5", "checksum5b", compilation2);
            await _compilationCacheService.SetCompilationAsync("project6", "checksum6", compilation3);

            // Act
            await _compilationCacheService.RemoveCompilationAsync("project5");

            // Assert
            var result1 = await _compilationCacheService.GetCompilationAsync("project5", "checksum5a");
            var result2 = await _compilationCacheService.GetCompilationAsync("project5", "checksum5b");
            var result3 = await _compilationCacheService.GetCompilationAsync("project6", "checksum6");
            
            result1.Should().BeNull();
            result2.Should().BeNull();
            result3.Should().NotBeNull(); // Should still exist
        }

        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnCorrectStats()
        {
            // Arrange
            var compilation1 = CreateTestCompilation("project7", "checksum7");
            var compilation2 = CreateTestCompilation("project8", "checksum8");
            
            await _compilationCacheService.SetCompilationAsync("project7", "checksum7", compilation1);
            await _compilationCacheService.SetCompilationAsync("project8", "checksum8", compilation2);
            
            // Trigger a cache hit
            await _compilationCacheService.GetCompilationAsync("project7", "checksum7");
            
            // Trigger a cache miss
            await _compilationCacheService.GetCompilationAsync("project9", "checksum9");

            // Act
            var stats = await _compilationCacheService.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.CacheType.Should().Be("CompilationCache");
            stats.TotalEntries.Should().Be(2);
            stats.HitCount.Should().BeGreaterThan(0);
            stats.MissCount.Should().BeGreaterThan(0);
            stats.TotalSizeBytes.Should().BeGreaterThan(0);
            stats.AdditionalMetrics.Should().ContainKey("MaxSizeBytes");
            stats.AdditionalMetrics.Should().ContainKey("MaxEntries");
            stats.AdditionalMetrics.Should().ContainKey("UsagePercent");
            stats.AdditionalMetrics.Should().ContainKey("CapacityPercent");
            stats.AdditionalMetrics.Should().ContainKey("AverageCompilationSize");
        }

        [Fact]
        public async Task PerformMaintenanceAsync_ShouldRemoveExpiredEntries()
        {
            // Arrange
            var compilation = CreateTestCompilation("project10", "checksum10");
            var shortExpiration = TimeSpan.FromMilliseconds(10);
            
            await _compilationCacheService.SetCompilationAsync("project10", "checksum10", compilation, shortExpiration);
            
            // Wait for expiration
            await Task.Delay(50);

            // Act
            await _compilationCacheService.PerformMaintenanceAsync();

            // Assert
            var result = await _compilationCacheService.GetCompilationAsync("project10", "checksum10");
            result.Should().BeNull();
        }

        [Fact]
        public async Task Cache_ShouldEvictLRUEntries_WhenCapacityExceeded()
        {
            // Arrange - Create cache with small capacity
            var smallOptions = new CacheOptions
            {
                MaxEntries = 3,
                MaxSizeBytes = 2048,
                EvictionThreshold = 0.5 // Start eviction at 50%
            };
            
            using var smallCache = new CompilationCacheService(Options.Create(smallOptions), _mockLogger.Object);
            
            // Add entries to exceed capacity
            for (int i = 1; i <= 5; i++)
            {
                var compilation = CreateTestCompilation($"project{i}", $"checksum{i}");
                await smallCache.SetCompilationAsync($"project{i}", $"checksum{i}", compilation);
                
                // Small delay to ensure different access times
                await Task.Delay(10);
            }

            // Act - Check which entries remain
            var stats = await smallCache.GetStatisticsAsync();

            // Assert - Should have evicted some entries
            stats.TotalEntries.Should().BeLessThan(5);
        }

        [Fact]
        public async Task Cache_ShouldUpdateAccessTime_OnGet()
        {
            // Arrange
            var compilation = CreateTestCompilation("project11", "checksum11");
            await _compilationCacheService.SetCompilationAsync("project11", "checksum11", compilation);
            
            // Wait a bit to ensure different timestamps
            await Task.Delay(10);

            // Act - Access the compilation
            var result = await _compilationCacheService.GetCompilationAsync("project11", "checksum11");

            // Assert
            result.Should().NotBeNull();
            
            // Check that hit count increased
            var stats = await _compilationCacheService.GetStatisticsAsync();
            stats.HitCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Cache_ShouldHandleConcurrentAccess()
        {
            // Arrange
            var compilation = CreateTestCompilation("project12", "checksum12");
            await _compilationCacheService.SetCompilationAsync("project12", "checksum12", compilation);

            // Act - Multiple concurrent reads
            var tasks = new List<Task<CachedCompilation?>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_compilationCacheService.GetCompilationAsync("project12", "checksum12"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(result => result.Should().NotBeNull());
            results.Should().AllSatisfy(result => result!.ProjectId.Should().Be("project12"));
        }

        [Fact]
        public async Task Cache_ShouldHandleMultipleChecksumsForSameProject()
        {
            // Arrange
            var compilation1 = CreateTestCompilation("project13", "checksum13a");
            var compilation2 = CreateTestCompilation("project13", "checksum13b");
            
            await _compilationCacheService.SetCompilationAsync("project13", "checksum13a", compilation1);
            await _compilationCacheService.SetCompilationAsync("project13", "checksum13b", compilation2);

            // Act
            var result1 = await _compilationCacheService.GetCompilationAsync("project13", "checksum13a");
            var result2 = await _compilationCacheService.GetCompilationAsync("project13", "checksum13b");

            // Assert
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
            result1!.Checksum.Should().Be("checksum13a");
            result2!.Checksum.Should().Be("checksum13b");
        }

        [Fact]
        public async Task Cache_ShouldHandleLargeCompilations()
        {
            // Arrange
            var largeCompilation = CreateLargeTestCompilation("large-project", "large-checksum");
            
            // Act
            await _compilationCacheService.SetCompilationAsync("large-project", "large-checksum", largeCompilation);
            var result = await _compilationCacheService.GetCompilationAsync("large-project", "large-checksum");

            // Assert
            result.Should().NotBeNull();
            result!.SerializedCompilation.Length.Should().BeGreaterOrEqualTo(10000);
            
            var stats = await _compilationCacheService.GetStatisticsAsync();
            stats.TotalSizeBytes.Should().BeGreaterThan(10000);
        }

        [Fact]
        public async Task Cache_ShouldRespectMaintenanceInterval()
        {
            // Arrange
            var fastMaintenanceOptions = new CacheOptions
            {
                MaxSizeBytes = 1024 * 1024,
                MaxEntries = 100,
                DefaultExpiration = TimeSpan.FromHours(1),
                MaintenanceInterval = TimeSpan.FromMilliseconds(50),
                EvictionThreshold = 0.8
            };
            
            using var fastCache = new CompilationCacheService(Options.Create(fastMaintenanceOptions), _mockLogger.Object);
            
            var compilation = CreateTestCompilation("project14", "checksum14");
            await fastCache.SetCompilationAsync("project14", "checksum14", compilation, TimeSpan.FromMilliseconds(25));
            
            // Act - Wait for maintenance to run
            await Task.Delay(100);
            
            // Assert - Entry should be cleaned up by maintenance
            var result = await fastCache.GetCompilationAsync("project14", "checksum14");
            result.Should().BeNull();
        }

        private static CachedCompilation CreateTestCompilation(string projectId, string checksum)
        {
            return new CachedCompilation
            {
                ProjectId = projectId,
                Checksum = checksum,
                SerializedCompilation = System.Text.Encoding.UTF8.GetBytes("serialized_compilation_data"),
                CachedAt = DateTime.UtcNow,
                Dependencies = new List<string> { "System.dll", "System.Core.dll" },
                MetadataReferences = new Dictionary<string, string>
                {
                    ["System"] = "System.dll",
                    ["System.Core"] = "System.Core.dll"
                }
            };
        }

        private static CachedCompilation CreateLargeTestCompilation(string projectId, string checksum)
        {
            var largeData = new string('X', 10000); // 10KB of data
            
            return new CachedCompilation
            {
                ProjectId = projectId,
                Checksum = checksum,
                SerializedCompilation = System.Text.Encoding.UTF8.GetBytes(largeData),
                CachedAt = DateTime.UtcNow,
                Dependencies = Enumerable.Range(1, 100).Select(i => $"Dependency{i}.dll").ToList(),
                MetadataReferences = Enumerable.Range(1, 50).ToDictionary(
                    i => $"Reference{i}",
                    i => $"Reference{i}.dll"
                )
            };
        }

        public void Dispose()
        {
            _compilationCacheService?.Dispose();
        }
    }
}