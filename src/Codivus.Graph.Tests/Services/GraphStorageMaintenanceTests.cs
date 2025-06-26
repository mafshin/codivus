using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Codivus.Graph.Configuration;
using Codivus.Graph.Services;
using Codivus.Graph.Models;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Codivus.Graph.Tests.Services
{
    public class GraphStorageMaintenanceTests : IDisposable
    {
        private readonly Mock<ILogger<GraphStorageService>> _mockLogger;
        private readonly Mock<IOptions<GraphConfiguration>> _mockOptions;
        private readonly GraphConfiguration _configuration;
        private readonly GraphStorageService _service;

        public GraphStorageMaintenanceTests()
        {
            _mockLogger = new Mock<ILogger<GraphStorageService>>();
            _mockOptions = new Mock<IOptions<GraphConfiguration>>();
            
            _configuration = new GraphConfiguration
            {
                Enabled = false, // Disable actual connections for testing
                JanusGraph = new JanusGraphSettings
                {
                    Host = "localhost",
                    Port = 8182,
                    ConnectionPoolSize = 5
                }
            };
            
            _mockOptions.Setup(x => x.Value).Returns(_configuration);
            _service = new GraphStorageService(_mockOptions.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task OptimizeIndicesAsync_ShouldCompleteSuccessfully()
        {
            // Act
            var exception = await Record.ExceptionAsync(async () => 
                await _service.OptimizeIndicesAsync(CancellationToken.None));

            // Assert
            exception.Should().BeNull();
            
            // Verify logging was called
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Optimizing graph indices")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
                
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Graph indices optimization completed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task CleanupOrphanedNodesAsync_ShouldReturnZeroCount()
        {
            // Act
            var result = await _service.CleanupOrphanedNodesAsync(CancellationToken.None);

            // Assert
            result.Should().Be(0); // In the stub implementation, this returns 0
            
            // Verify logging was called
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting cleanup of orphaned nodes")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task DefragmentStorageAsync_ShouldCompleteSuccessfully()
        {
            // Act
            var exception = await Record.ExceptionAsync(async () => 
                await _service.DefragmentStorageAsync(CancellationToken.None));

            // Assert
            exception.Should().BeNull();
            
            // Verify logging was called
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting graph storage defragmentation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateStatisticsAsync_ShouldCompleteSuccessfully()
        {
            // Act
            var exception = await Record.ExceptionAsync(async () => 
                await _service.UpdateStatisticsAsync(CancellationToken.None));

            // Assert
            exception.Should().BeNull();
            
            // Verify logging was called
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Updating graph statistics")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task OptimizeIndicesAsync_WithCancellation_ShouldHandleCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _service.OptimizeIndicesAsync(cts.Token));
        }

        [Fact]
        public async Task CleanupOrphanedNodesAsync_WithCancellation_ShouldHandleCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _service.CleanupOrphanedNodesAsync(cts.Token));
        }

        [Fact]
        public async Task DefragmentStorageAsync_WithCancellation_ShouldHandleCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _service.DefragmentStorageAsync(cts.Token));
        }

        [Fact]
        public async Task UpdateStatisticsAsync_WithCancellation_ShouldHandleCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _service.UpdateStatisticsAsync(cts.Token));
        }

        [Fact]
        public async Task MaintenanceOperations_ShouldBeIdempotent()
        {
            // Act - Call each operation multiple times
            await _service.OptimizeIndicesAsync();
            await _service.OptimizeIndicesAsync();
            
            var result1 = await _service.CleanupOrphanedNodesAsync();
            var result2 = await _service.CleanupOrphanedNodesAsync();
            
            await _service.DefragmentStorageAsync();
            await _service.DefragmentStorageAsync();
            
            await _service.UpdateStatisticsAsync();
            await _service.UpdateStatisticsAsync();

            // Assert - Operations should be idempotent
            result1.Should().Be(result2);
        }

        [Fact]
        public async Task MaintenanceOperations_ShouldHandleSimultaneousCalls()
        {
            // Act - Call operations simultaneously
            var tasks = new Task[]
            {
                _service.OptimizeIndicesAsync(),
                _service.CleanupOrphanedNodesAsync(),
                _service.DefragmentStorageAsync(),
                _service.UpdateStatisticsAsync()
            };

            // Assert - All operations should complete without exceptions
            var exception = await Record.ExceptionAsync(async () => await Task.WhenAll(tasks));
            exception.Should().BeNull();
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}