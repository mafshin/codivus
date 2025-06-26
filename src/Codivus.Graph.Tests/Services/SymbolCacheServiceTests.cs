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
    public class SymbolCacheServiceTests : IDisposable
    {
        private readonly SymbolCacheService _symbolCacheService;
        private readonly Mock<ILogger<SymbolCacheService>> _mockLogger;
        private readonly CacheOptions _cacheOptions;

        public SymbolCacheServiceTests()
        {
            _mockLogger = new Mock<ILogger<SymbolCacheService>>();
            _cacheOptions = new CacheOptions
            {
                MaxSizeBytes = 1024 * 1024, // 1MB
                MaxEntries = 100,
                DefaultExpiration = TimeSpan.FromHours(1),
                MaintenanceInterval = TimeSpan.FromMilliseconds(100),
                EvictionThreshold = 0.8
            };

            var options = Options.Create(_cacheOptions);
            _symbolCacheService = new SymbolCacheService(options, _mockLogger.Object);
        }

        [Fact]
        public async Task GetSymbolsAsync_WhenCacheEmpty_ShouldReturnNull()
        {
            // Act
            var result = await _symbolCacheService.GetSymbolsAsync("file1", "checksum1");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetSymbolsAsync_ShouldStoreCachedSymbolData()
        {
            // Arrange
            var symbolData = CreateTestSymbolData("file1", "checksum1", "repo1");

            // Act
            await _symbolCacheService.SetSymbolsAsync("file1", "checksum1", symbolData);

            // Assert
            var result = await _symbolCacheService.GetSymbolsAsync("file1", "checksum1");
            result.Should().NotBeNull();
            result!.FileId.Should().Be("file1");
            result.Checksum.Should().Be("checksum1");
        }

        [Fact]
        public async Task GetSymbolsAsync_AfterSet_ShouldReturnStoredData()
        {
            // Arrange
            var symbolData = CreateTestSymbolData("file2", "checksum2", "repo1");
            await _symbolCacheService.SetSymbolsAsync("file2", "checksum2", symbolData);

            // Act
            var result = await _symbolCacheService.GetSymbolsAsync("file2", "checksum2");

            // Assert
            result.Should().NotBeNull();
            result!.FileId.Should().Be("file2");
            result.Nodes.Should().HaveCount(2);
            result.Relationships.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetSymbolsAsync_WithDifferentChecksum_ShouldReturnNull()
        {
            // Arrange
            var symbolData = CreateTestSymbolData("file3", "checksum3", "repo1");
            await _symbolCacheService.SetSymbolsAsync("file3", "checksum3", symbolData);

            // Act
            var result = await _symbolCacheService.GetSymbolsAsync("file3", "different-checksum");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetSymbolsAsync_WithExpiration_ShouldExpireAfterTime()
        {
            // Arrange
            var symbolData = CreateTestSymbolData("file4", "checksum4", "repo1");
            var shortExpiration = TimeSpan.FromMilliseconds(50);

            // Act
            await _symbolCacheService.SetSymbolsAsync("file4", "checksum4", symbolData, shortExpiration);
            
            // Verify it's there initially
            var result1 = await _symbolCacheService.GetSymbolsAsync("file4", "checksum4");
            result1.Should().NotBeNull();

            // Wait for expiration
            await Task.Delay(100);

            // Should be expired now
            var result2 = await _symbolCacheService.GetSymbolsAsync("file4", "checksum4");
            result2.Should().BeNull();
        }

        [Fact]
        public async Task RemoveSymbolsAsync_ShouldRemoveAllEntriesForFile()
        {
            // Arrange
            var symbolData1 = CreateTestSymbolData("file5", "checksum5a", "repo1");
            var symbolData2 = CreateTestSymbolData("file5", "checksum5b", "repo1");
            
            await _symbolCacheService.SetSymbolsAsync("file5", "checksum5a", symbolData1);
            await _symbolCacheService.SetSymbolsAsync("file5", "checksum5b", symbolData2);

            // Act
            await _symbolCacheService.RemoveSymbolsAsync("file5");

            // Assert
            var result1 = await _symbolCacheService.GetSymbolsAsync("file5", "checksum5a");
            var result2 = await _symbolCacheService.GetSymbolsAsync("file5", "checksum5b");
            
            result1.Should().BeNull();
            result2.Should().BeNull();
        }

        [Fact]
        public async Task ClearRepositoryAsync_ShouldRemoveAllEntriesForRepository()
        {
            // Arrange
            var symbolData1 = CreateTestSymbolData("file6", "checksum6", "repo2");
            var symbolData2 = CreateTestSymbolData("file7", "checksum7", "repo2");
            var symbolData3 = CreateTestSymbolData("file8", "checksum8", "repo3");
            
            await _symbolCacheService.SetSymbolsAsync("file6", "checksum6", symbolData1);
            await _symbolCacheService.SetSymbolsAsync("file7", "checksum7", symbolData2);
            await _symbolCacheService.SetSymbolsAsync("file8", "checksum8", symbolData3);

            // Act
            await _symbolCacheService.ClearRepositoryAsync("repo2");

            // Assert
            var result1 = await _symbolCacheService.GetSymbolsAsync("file6", "checksum6");
            var result2 = await _symbolCacheService.GetSymbolsAsync("file7", "checksum7");
            var result3 = await _symbolCacheService.GetSymbolsAsync("file8", "checksum8");
            
            result1.Should().BeNull();
            result2.Should().BeNull();
            result3.Should().NotBeNull(); // Should still exist
        }

        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnCorrectStats()
        {
            // Arrange
            var symbolData1 = CreateTestSymbolData("file9", "checksum9", "repo1");
            var symbolData2 = CreateTestSymbolData("file10", "checksum10", "repo1");
            
            await _symbolCacheService.SetSymbolsAsync("file9", "checksum9", symbolData1);
            await _symbolCacheService.SetSymbolsAsync("file10", "checksum10", symbolData2);
            
            // Trigger a cache hit
            await _symbolCacheService.GetSymbolsAsync("file9", "checksum9");
            
            // Trigger a cache miss
            await _symbolCacheService.GetSymbolsAsync("file11", "checksum11");

            // Act
            var stats = await _symbolCacheService.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.CacheType.Should().Be("SymbolCache");
            stats.TotalEntries.Should().Be(2);
            stats.HitCount.Should().BeGreaterThan(0);
            stats.MissCount.Should().BeGreaterThan(0);
            stats.TotalSizeBytes.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task PerformMaintenanceAsync_ShouldRemoveExpiredEntries()
        {
            // Arrange
            var symbolData = CreateTestSymbolData("file12", "checksum12", "repo1");
            var shortExpiration = TimeSpan.FromMilliseconds(10);
            
            await _symbolCacheService.SetSymbolsAsync("file12", "checksum12", symbolData, shortExpiration);
            
            // Wait for expiration
            await Task.Delay(50);

            // Act
            await _symbolCacheService.PerformMaintenanceAsync();

            // Assert
            var result = await _symbolCacheService.GetSymbolsAsync("file12", "checksum12");
            result.Should().BeNull();
        }

        [Fact]
        public async Task Cache_ShouldEvictLRUEntries_WhenCapacityExceeded()
        {
            // Arrange - Create cache with small capacity
            var smallOptions = new CacheOptions
            {
                MaxEntries = 3,
                MaxSizeBytes = 1024,
                EvictionThreshold = 0.5 // Start eviction at 50%
            };
            
            using var smallCache = new SymbolCacheService(Options.Create(smallOptions), _mockLogger.Object);
            
            // Add entries to exceed capacity
            for (int i = 1; i <= 5; i++)
            {
                var symbolData = CreateTestSymbolData($"file{i}", $"checksum{i}", "repo1");
                await smallCache.SetSymbolsAsync($"file{i}", $"checksum{i}", symbolData);
                
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
            var symbolData1 = CreateTestSymbolData("file13", "checksum13", "repo1");
            var symbolData2 = CreateTestSymbolData("file14", "checksum14", "repo1");
            
            await _symbolCacheService.SetSymbolsAsync("file13", "checksum13", symbolData1);
            await _symbolCacheService.SetSymbolsAsync("file14", "checksum14", symbolData2);
            
            await Task.Delay(10);
            
            // Access first entry to update its access time
            var result1 = await _symbolCacheService.GetSymbolsAsync("file13", "checksum13");
            
            // Act & Assert
            result1.Should().NotBeNull();
            result1!.LastAccessed.Should().BeAfter(result1.CachedAt);
        }

        [Fact]
        public async Task Cache_ShouldHandleConcurrentAccess()
        {
            // Arrange
            var symbolData = CreateTestSymbolData("file15", "checksum15", "repo1");
            await _symbolCacheService.SetSymbolsAsync("file15", "checksum15", symbolData);

            // Act - Multiple concurrent reads
            var tasks = new List<Task<CachedSymbolData?>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_symbolCacheService.GetSymbolsAsync("file15", "checksum15"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(result => result.Should().NotBeNull());
            results.Should().AllSatisfy(result => result!.FileId.Should().Be("file15"));
        }

        private static CachedSymbolData CreateTestSymbolData(string fileId, string checksum, string repositoryId)
        {
            return new CachedSymbolData
            {
                FileId = fileId,
                Checksum = checksum,
                RepositoryId = repositoryId,
                CachedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                Nodes = new List<CodeNode>
                {
                    new CodeNode
                    {
                        Id = $"node1-{fileId}",
                        NodeType = NodeType.Type,
                        Name = "TestClass",
                        FullName = "Test.TestClass"
                    },
                    new CodeNode
                    {
                        Id = $"node2-{fileId}",
                        NodeType = NodeType.Method,
                        Name = "TestMethod",
                        FullName = "Test.TestClass.TestMethod"
                    }
                },
                Relationships = new List<CodeRelationship>
                {
                    new CodeRelationship
                    {
                        Id = $"rel1-{fileId}",
                        SourceNodeId = $"node1-{fileId}",
                        TargetNodeId = $"node2-{fileId}",
                        Type = RelationshipType.Contains
                    }
                },
                Metadata = new Dictionary<string, object>
                {
                    ["language"] = "C#",
                    ["framework"] = "net8.0"
                },
                SizeInBytes = 1024
            };
        }

        public void Dispose()
        {
            _symbolCacheService?.Dispose();
        }
    }
}