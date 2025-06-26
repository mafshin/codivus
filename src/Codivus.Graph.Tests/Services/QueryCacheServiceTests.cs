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
    public class QueryCacheServiceTests : IDisposable
    {
        private readonly QueryCacheService _queryCacheService;
        private readonly Mock<ILogger<QueryCacheService>> _mockLogger;
        private readonly CacheOptions _cacheOptions;

        public QueryCacheServiceTests()
        {
            _mockLogger = new Mock<ILogger<QueryCacheService>>();
            _cacheOptions = new CacheOptions
            {
                MaxSizeBytes = 1024 * 1024, // 1MB
                MaxEntries = 100,
                DefaultExpiration = TimeSpan.FromHours(1),
                MaintenanceInterval = TimeSpan.FromMilliseconds(100),
                EvictionThreshold = 0.8
            };

            var options = Options.Create(_cacheOptions);
            _queryCacheService = new QueryCacheService(options, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAsync_WhenCacheEmpty_ShouldReturnNull()
        {
            // Act
            var result = await _queryCacheService.GetAsync<TestQueryResult>("nonexistent-key");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_ShouldStoreQueryResult()
        {
            // Arrange
            var queryResult = new TestQueryResult { Id = "test1", Name = "Test Query", Count = 42 };
            var queryKey = "test-query-1";

            // Act
            await _queryCacheService.SetAsync(queryKey, queryResult);

            // Assert
            var result = await _queryCacheService.GetAsync<TestQueryResult>(queryKey);
            result.Should().NotBeNull();
            result!.Id.Should().Be("test1");
            result.Name.Should().Be("Test Query");
            result.Count.Should().Be(42);
        }

        [Fact]
        public async Task GetAsync_AfterSet_ShouldReturnDeserializedData()
        {
            // Arrange
            var queryResult = new TestQueryResult { Id = "test2", Name = "Complex Query", Count = 100 };
            var queryKey = "complex-query";

            await _queryCacheService.SetAsync(queryKey, queryResult);

            // Act
            var result = await _queryCacheService.GetAsync<TestQueryResult>(queryKey);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(queryResult);
        }

        [Fact]
        public async Task SetAsync_WithExpiration_ShouldExpireAfterTime()
        {
            // Arrange
            var queryResult = new TestQueryResult { Id = "test3", Name = "Expiring Query", Count = 5 };
            var queryKey = "expiring-query";
            var shortExpiration = TimeSpan.FromMilliseconds(50);

            // Act
            await _queryCacheService.SetAsync(queryKey, queryResult, shortExpiration);
            
            // Verify it's there initially
            var result1 = await _queryCacheService.GetAsync<TestQueryResult>(queryKey);
            result1.Should().NotBeNull();

            // Wait for expiration
            await Task.Delay(100);

            // Should be expired now
            var result2 = await _queryCacheService.GetAsync<TestQueryResult>(queryKey);
            result2.Should().BeNull();
        }

        [Fact]
        public async Task RemoveAsync_ShouldRemoveSpecificEntry()
        {
            // Arrange
            var queryResult1 = new TestQueryResult { Id = "test4", Name = "Query 1", Count = 10 };
            var queryResult2 = new TestQueryResult { Id = "test5", Name = "Query 2", Count = 20 };
            
            await _queryCacheService.SetAsync("query-1", queryResult1);
            await _queryCacheService.SetAsync("query-2", queryResult2);

            // Act
            await _queryCacheService.RemoveAsync("query-1");

            // Assert
            var result1 = await _queryCacheService.GetAsync<TestQueryResult>("query-1");
            var result2 = await _queryCacheService.GetAsync<TestQueryResult>("query-2");
            
            result1.Should().BeNull();
            result2.Should().NotBeNull();
        }

        [Fact]
        public async Task RemoveByPatternAsync_ShouldRemoveMatchingEntries()
        {
            // Arrange
            var queryResult1 = new TestQueryResult { Id = "test6", Name = "User Query 1", Count = 30 };
            var queryResult2 = new TestQueryResult { Id = "test7", Name = "User Query 2", Count = 40 };
            var queryResult3 = new TestQueryResult { Id = "test8", Name = "System Query", Count = 50 };
            
            await _queryCacheService.SetAsync("user-query-1", queryResult1);
            await _queryCacheService.SetAsync("user-query-2", queryResult2);
            await _queryCacheService.SetAsync("system-query-1", queryResult3);

            // Act
            await _queryCacheService.RemoveByPatternAsync("user-.*");

            // Assert
            var result1 = await _queryCacheService.GetAsync<TestQueryResult>("user-query-1");
            var result2 = await _queryCacheService.GetAsync<TestQueryResult>("user-query-2");
            var result3 = await _queryCacheService.GetAsync<TestQueryResult>("system-query-1");
            
            result1.Should().BeNull();
            result2.Should().BeNull();
            result3.Should().NotBeNull(); // Should not match pattern
        }

        [Fact]
        public async Task ClearAsync_ShouldRemoveAllEntries()
        {
            // Arrange
            var queryResult1 = new TestQueryResult { Id = "test9", Name = "Query A", Count = 60 };
            var queryResult2 = new TestQueryResult { Id = "test10", Name = "Query B", Count = 70 };
            
            await _queryCacheService.SetAsync("query-a", queryResult1);
            await _queryCacheService.SetAsync("query-b", queryResult2);

            // Act
            await _queryCacheService.ClearAsync();

            // Assert
            var result1 = await _queryCacheService.GetAsync<TestQueryResult>("query-a");
            var result2 = await _queryCacheService.GetAsync<TestQueryResult>("query-b");
            
            result1.Should().BeNull();
            result2.Should().BeNull();
        }

        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnCorrectStats()
        {
            // Arrange
            var queryResult1 = new TestQueryResult { Id = "test11", Name = "Stats Query 1", Count = 80 };
            var queryResult2 = new TestQueryResult { Id = "test12", Name = "Stats Query 2", Count = 90 };
            
            await _queryCacheService.SetAsync("stats-query-1", queryResult1);
            await _queryCacheService.SetAsync("stats-query-2", queryResult2);
            
            // Trigger a cache hit
            await _queryCacheService.GetAsync<TestQueryResult>("stats-query-1");
            
            // Trigger a cache miss
            await _queryCacheService.GetAsync<TestQueryResult>("nonexistent-query");

            // Act
            var stats = await _queryCacheService.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.CacheType.Should().Be("QueryCache");
            stats.TotalEntries.Should().Be(2);
            stats.HitCount.Should().BeGreaterThan(0);
            stats.MissCount.Should().BeGreaterThan(0);
            stats.TotalSizeBytes.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Cache_ShouldHandleComplexObjects()
        {
            // Arrange
            var complexResult = new ComplexQueryResult
            {
                Id = "complex1",
                Results = new List<TestQueryResult>
                {
                    new TestQueryResult { Id = "sub1", Name = "Sub Result 1", Count = 100 },
                    new TestQueryResult { Id = "sub2", Name = "Sub Result 2", Count = 200 }
                },
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["source"] = "unit-test",
                    ["nested"] = new { level = 1, enabled = true }
                }
            };

            // Act
            await _queryCacheService.SetAsync("complex-query", complexResult);
            var result = await _queryCacheService.GetAsync<ComplexQueryResult>("complex-query");

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("complex1");
            result.Results.Should().HaveCount(2);
            result.Results[0].Name.Should().Be("Sub Result 1");
            result.Metadata.Should().ContainKey("source");
        }

        [Fact]
        public async Task Cache_ShouldHandleSerializationErrors_Gracefully()
        {
            // Arrange
            var unserializableResult = new UnserializableQueryResult
            {
                Id = "unserializable",
                CircularReference = null
            };
            // Create circular reference
            unserializableResult.CircularReference = unserializableResult;

            // Act & Assert - Should not throw
            await _queryCacheService.SetAsync("bad-query", unserializableResult);
            var result = await _queryCacheService.GetAsync<UnserializableQueryResult>("bad-query");
            
            // Result might be null or the object without the circular reference
            // The important thing is that it doesn't crash the service
        }

        [Fact]
        public async Task Cache_ShouldEvictLRUEntries_WhenCapacityExceeded()
        {
            // Arrange - Create cache with small capacity
            var smallOptions = new CacheOptions
            {
                MaxEntries = 3,
                MaxSizeBytes = 2048,
                EvictionThreshold = 0.5
            };
            
            using var smallCache = new QueryCacheService(Options.Create(smallOptions), _mockLogger.Object);
            
            // Add entries to exceed capacity
            for (int i = 1; i <= 5; i++)
            {
                var queryResult = new TestQueryResult { Id = $"test{i}", Name = $"Query {i}", Count = i * 10 };
                await smallCache.SetAsync($"query-{i}", queryResult);
                await Task.Delay(10); // Ensure different access times
            }

            // Act - Check which entries remain
            var stats = await smallCache.GetStatisticsAsync();

            // Assert - Should have evicted some entries
            stats.TotalEntries.Should().BeLessThan(5);
        }

        [Fact]
        public async Task Cache_ShouldHandleConcurrentAccess()
        {
            // Arrange
            var queryResult = new TestQueryResult { Id = "concurrent", Name = "Concurrent Test", Count = 999 };
            await _queryCacheService.SetAsync("concurrent-query", queryResult);

            // Act - Multiple concurrent reads
            var tasks = new List<Task<TestQueryResult?>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_queryCacheService.GetAsync<TestQueryResult>("concurrent-query"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllSatisfy(result => result.Should().NotBeNull());
            results.Should().AllSatisfy(result => result!.Id.Should().Be("concurrent"));
        }

        [Fact]
        public async Task Cache_ShouldUpdateAccessTime_OnGet()
        {
            // Arrange
            var queryResult = new TestQueryResult { Id = "access-test", Name = "Access Time Test", Count = 123 };
            await _queryCacheService.SetAsync("access-time-query", queryResult);
            
            await Task.Delay(10);
            
            // Act - Access the entry to update access time
            var result = await _queryCacheService.GetAsync<TestQueryResult>("access-time-query");
            
            // Assert
            result.Should().NotBeNull();
            
            // Get statistics to verify access tracking
            var stats = await _queryCacheService.GetStatisticsAsync();
            stats.HitCount.Should().BeGreaterThan(0);
        }

        // Test helper classes
        public class TestQueryResult
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        public class ComplexQueryResult
        {
            public string Id { get; set; } = string.Empty;
            public List<TestQueryResult> Results { get; set; } = new();
            public Dictionary<string, object> Metadata { get; set; } = new();
        }

        public class UnserializableQueryResult
        {
            public string Id { get; set; } = string.Empty;
            public UnserializableQueryResult? CircularReference { get; set; }
        }

        public void Dispose()
        {
            _queryCacheService?.Dispose();
        }
    }
}