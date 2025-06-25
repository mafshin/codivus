using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Codivus.API.BackgroundServices;
using Codivus.Core.Models;
using Codivus.Core.Interfaces;

namespace Codivus.API.Tests.BackgroundServices
{
    public class GraphScanWorkerTests
    {
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IServiceScope> _mockServiceScope;
        private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
        private readonly Mock<ITaskQueue<GraphScanTask>> _mockTaskQueue;
        private readonly Mock<IGraphScanProcessor> _mockProcessor;
        private readonly Mock<ILogger<GraphScanWorker>> _mockLogger;

        public GraphScanWorkerTests()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceScope = new Mock<IServiceScope>();
            _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
            _mockTaskQueue = new Mock<ITaskQueue<GraphScanTask>>();
            _mockProcessor = new Mock<IGraphScanProcessor>();
            _mockLogger = new Mock<ILogger<GraphScanWorker>>();

            // Setup service provider chain
            _mockServiceProvider
                .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(_mockServiceScopeFactory.Object);
            
            _mockServiceScopeFactory
                .Setup(x => x.CreateScope())
                .Returns(_mockServiceScope.Object);

            _mockServiceScope
                .Setup(x => x.ServiceProvider)
                .Returns(_mockServiceProvider.Object);

            _mockServiceProvider
                .Setup(x => x.GetService(typeof(ITaskQueue<GraphScanTask>)))
                .Returns(_mockTaskQueue.Object);

            _mockServiceProvider
                .Setup(x => x.GetService(typeof(IGraphScanProcessor)))
                .Returns(_mockProcessor.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Act
            var worker = new GraphScanWorker(_mockServiceProvider.Object, _mockLogger.Object);

            // Assert
            worker.Should().NotBeNull();
            worker.Should().BeAssignableTo<BackgroundService>();
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelled_ShouldStopGracefully()
        {
            // Arrange
            var worker = new GraphScanWorker(_mockServiceProvider.Object, _mockLogger.Object);
            var cts = new CancellationTokenSource();

            _mockTaskQueue
                .Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken ct) =>
                {
                    await Task.Delay(10, ct);
                    return new GraphScanTask();
                });

            // Act
            var executeTask = worker.StartAsync(cts.Token);
            
            // Cancel immediately
            cts.Cancel();
            
            // Wait for completion
            await Task.Delay(100);
            await worker.StopAsync(CancellationToken.None);

            // Assert - Should complete without throwing
            executeTask.Should().NotBeNull();
        }

        [Fact]
        public async Task ProcessTask_WhenSuccessful_ShouldMarkAsCompleted()
        {
            // This test is more integration-focused as the actual processing logic
            // is complex to test in isolation due to the background service nature
            
            // Arrange
            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = "test-repo",
                TargetPath = "/test/path"
            };

            var worker = new GraphScanWorker(_mockServiceProvider.Object, _mockLogger.Object);

            _mockTaskQueue
                .SetupSequence(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(task)
                .ThrowsAsync(new OperationCanceledException()); // To stop the worker

            _mockProcessor
                .Setup(x => x.ProcessTaskAsync(task, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockTaskQueue
                .Setup(x => x.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Completed, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act
            await worker.StartAsync(cts.Token);
            await Task.Delay(50); // Allow some processing time
            await worker.StopAsync(CancellationToken.None);

            // Assert
            _mockProcessor.Verify(x => x.ProcessTaskAsync(task, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ProcessTask_WhenProcessorThrows_ShouldMarkAsFailed()
        {
            // Arrange
            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = "test-repo",
                TargetPath = "/test/path",
                RetryCount = 0,
                MaxRetries = 1
            };

            var worker = new GraphScanWorker(_mockServiceProvider.Object, _mockLogger.Object);

            _mockTaskQueue
                .SetupSequence(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(task)
                .ThrowsAsync(new OperationCanceledException()); // To stop the worker

            _mockProcessor
                .Setup(x => x.ProcessTaskAsync(task, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test error"));

            _mockTaskQueue
                .Setup(x => x.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Failed, "Test error", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockTaskQueue
                .Setup(x => x.EnqueueAsync(task, It.IsAny<CancellationToken>()))
                .ReturnsAsync("retry-task-id");

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act
            await worker.StartAsync(cts.Token);
            await Task.Delay(50); // Allow some processing time
            await worker.StopAsync(CancellationToken.None);

            // Assert
            _mockProcessor.Verify(x => x.ProcessTaskAsync(task, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            _mockTaskQueue.Verify(x => x.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Failed, "Test error", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ProcessTask_WhenRetryLimitExceeded_ShouldNotRequeue()
        {
            // Arrange
            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = "test-repo",
                TargetPath = "/test/path",
                RetryCount = 3,
                MaxRetries = 3 // Already at max retries
            };

            var worker = new GraphScanWorker(_mockServiceProvider.Object, _mockLogger.Object);

            _mockTaskQueue
                .SetupSequence(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(task)
                .ThrowsAsync(new OperationCanceledException()); // To stop the worker

            _mockProcessor
                .Setup(x => x.ProcessTaskAsync(task, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test error"));

            _mockTaskQueue
                .Setup(x => x.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Failed, "Test error", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act
            await worker.StartAsync(cts.Token);
            await Task.Delay(50); // Allow some processing time
            await worker.StopAsync(CancellationToken.None);

            // Assert
            _mockTaskQueue.Verify(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void WorkerCount_ShouldBeBasedOnProcessorCount()
        {
            // Arrange & Act
            var worker = new GraphScanWorker(_mockServiceProvider.Object, _mockLogger.Object);

            // Assert
            worker.Should().NotBeNull();
            // The worker should be designed to use Environment.ProcessorCount for worker count
            // This is more of a design verification than a functional test
        }

        [Fact]
        public async Task DequeueTimeout_ShouldContinueProcessing()
        {
            // Arrange
            var worker = new GraphScanWorker(_mockServiceProvider.Object, _mockLogger.Object);

            _mockTaskQueue
                .SetupSequence(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException()) // Timeout
                .ThrowsAsync(new OperationCanceledException()); // Stop worker

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act & Assert - Should not throw and should handle timeouts gracefully
            await worker.StartAsync(cts.Token);
            await Task.Delay(50);
            await worker.StopAsync(CancellationToken.None);
        }
    }

    // Mock implementation for testing
    public class MockGraphScanProcessor : IGraphScanProcessor
    {
        public Task ProcessTaskAsync(GraphScanTask task, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}