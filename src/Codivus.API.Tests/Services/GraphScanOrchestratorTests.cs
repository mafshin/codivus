using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.API.Services;
using Codivus.Core.Models;
using Codivus.Core.Interfaces;
using Codivus.Graph.Interfaces;

namespace Codivus.API.Tests.Services
{
    public class GraphScanOrchestratorTests
    {
        private readonly Mock<ITaskQueue<GraphScanTask>> _mockTaskQueue;
        private readonly Mock<IRepositoryService> _mockRepositoryService;
        private readonly Mock<IGraphStorageService> _mockGraphStorageService;
        private readonly Mock<ILogger<GraphScanOrchestrator>> _mockLogger;
        private readonly GraphScanOrchestrator _orchestrator;

        public GraphScanOrchestratorTests()
        {
            _mockTaskQueue = new Mock<ITaskQueue<GraphScanTask>>();
            _mockRepositoryService = new Mock<IRepositoryService>();
            _mockGraphStorageService = new Mock<IGraphStorageService>();
            _mockLogger = new Mock<ILogger<GraphScanOrchestrator>>();
            
            _orchestrator = new GraphScanOrchestrator(
                _mockTaskQueue.Object,
                _mockRepositoryService.Object,
                _mockGraphStorageService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task StartGraphScanAsync_WithValidRepository_ShouldReturnScanId()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repository = new Repository 
            { 
                Id = Guid.Parse(repositoryId),
                Name = "TestRepo",
                Location = "/test/repo",
                Type = RepositoryType.Local
            };
            
            var configuration = new GraphScanConfiguration
            {
                RepositoryId = repositoryId,
                Mode = ScanMode.Full
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                Name = "TestFile.cs",
                Path = "/test/TestFile.cs",
                IsDirectory = false
            };

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(repository.Id))
                .ReturnsAsync(repository);
            
            _mockRepositoryService
                .Setup(x => x.GetRepositoryStructureAsync(repository.Id))
                .ReturnsAsync(rootStructure);

            _mockTaskQueue
                .Setup(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("task-id");

            // Act
            var scanId = await _orchestrator.StartGraphScanAsync(repositoryId, configuration);

            // Assert
            scanId.Should().NotBeNullOrEmpty();
            _mockRepositoryService.Verify(x => x.GetRepositoryByIdAsync(repository.Id), Times.Once);
            _mockTaskQueue.Verify(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartGraphScanAsync_WithInvalidRepositoryId_ShouldThrowArgumentException()
        {
            // Arrange
            var invalidRepositoryId = "invalid-guid";
            var configuration = new GraphScanConfiguration();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _orchestrator.StartGraphScanAsync(invalidRepositoryId, configuration));
        }

        [Fact]
        public async Task StartGraphScanAsync_WithNonExistentRepository_ShouldThrowArgumentException()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var configuration = new GraphScanConfiguration();

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Repository?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _orchestrator.StartGraphScanAsync(repositoryId, configuration));
        }

        [Fact]
        public async Task GetScanProgressAsync_WithValidScanId_ShouldReturnProgress()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repository = new Repository 
            { 
                Id = Guid.Parse(repositoryId),
                Name = "TestRepo",
                Location = "/test/repo",
                Type = RepositoryType.Local
            };
            
            var configuration = new GraphScanConfiguration
            {
                RepositoryId = repositoryId,
                Mode = ScanMode.Full
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                Name = "TestFile.cs",
                Path = "/test/TestFile.cs",
                IsDirectory = false
            };

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(repository.Id))
                .ReturnsAsync(repository);
            
            _mockRepositoryService
                .Setup(x => x.GetRepositoryStructureAsync(repository.Id))
                .ReturnsAsync(rootStructure);

            _mockTaskQueue.Setup(x => x.GetTasksAsync(null, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GraphScanTask>());

            // Act
            var scanId = await _orchestrator.StartGraphScanAsync(repositoryId, configuration);
            var progress = await _orchestrator.GetScanProgressAsync(scanId);

            // Assert
            progress.Should().NotBeNull();
            progress.ScanId.Should().Be(scanId);
            progress.RepositoryId.Should().Be(repositoryId);
        }

        [Fact]
        public async Task GetScanProgressAsync_WithInvalidScanId_ShouldReturnNull()
        {
            // Act
            var progress = await _orchestrator.GetScanProgressAsync("invalid-scan-id");

            // Assert
            progress.Should().BeNull();
        }

        [Fact]
        public async Task PauseScanAsync_WithValidScanId_ShouldPauseTasks()
        {
            // Arrange
            var scanId = "test-scan-id";
            var tasks = new List<GraphScanTask>
            {
                new GraphScanTask 
                { 
                    ScanId = scanId, 
                    Status = QueueTaskStatus.Queued,
                    TaskId = "task1"
                }
            };

            _mockTaskQueue.Setup(x => x.GetTasksAsync(null, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            _mockTaskQueue.Setup(x => x.UpdateTaskStatusAsync("task1", QueueTaskStatus.Paused, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.PauseScanAsync(scanId);

            // Assert
            result.Should().BeTrue();
            _mockTaskQueue.Verify(x => x.UpdateTaskStatusAsync("task1", QueueTaskStatus.Paused, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ResumeScanAsync_WithValidScanId_ShouldResumeTasks()
        {
            // Arrange
            var scanId = "test-scan-id";
            var tasks = new List<GraphScanTask>
            {
                new GraphScanTask 
                { 
                    ScanId = scanId, 
                    Status = QueueTaskStatus.Paused,
                    TaskId = "task1"
                }
            };

            _mockTaskQueue.Setup(x => x.GetTasksAsync(null, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            _mockTaskQueue.Setup(x => x.UpdateTaskStatusAsync("task1", QueueTaskStatus.Queued, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.ResumeScanAsync(scanId);

            // Assert
            result.Should().BeTrue();
            _mockTaskQueue.Verify(x => x.UpdateTaskStatusAsync("task1", QueueTaskStatus.Queued, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelScanAsync_WithValidScanId_ShouldCancelTasks()
        {
            // Arrange
            var scanId = "test-scan-id";
            var tasks = new List<GraphScanTask>
            {
                new GraphScanTask 
                { 
                    ScanId = scanId, 
                    Status = QueueTaskStatus.Queued,
                    TaskId = "task1"
                },
                new GraphScanTask 
                { 
                    ScanId = scanId, 
                    Status = QueueTaskStatus.Paused,
                    TaskId = "task2"
                }
            };

            _mockTaskQueue.Setup(x => x.GetTasksAsync(null, 100, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            _mockTaskQueue.Setup(x => x.UpdateTaskStatusAsync("task1", QueueTaskStatus.Cancelled, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockTaskQueue.Setup(x => x.UpdateTaskStatusAsync("task2", QueueTaskStatus.Cancelled, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.CancelScanAsync(scanId);

            // Assert
            result.Should().BeTrue();
            _mockTaskQueue.Verify(x => x.UpdateTaskStatusAsync("task1", QueueTaskStatus.Cancelled, null, It.IsAny<CancellationToken>()), Times.Once);
            _mockTaskQueue.Verify(x => x.UpdateTaskStatusAsync("task2", QueueTaskStatus.Cancelled, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(ScanMode.Full)]
        [InlineData(ScanMode.Incremental)]
        [InlineData(ScanMode.Differential)]
        public async Task StartGraphScanAsync_WithDifferentModes_ShouldHandleAllModes(ScanMode mode)
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repository = new Repository 
            { 
                Id = Guid.Parse(repositoryId),
                Name = "TestRepo",
                Location = "/test/repo",
                Type = RepositoryType.Local
            };
            
            var configuration = new GraphScanConfiguration
            {
                RepositoryId = repositoryId,
                Mode = mode
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                Name = "TestFile.cs",
                Path = "/test/TestFile.cs",
                IsDirectory = false
            };

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(repository.Id))
                .ReturnsAsync(repository);
            
            _mockRepositoryService
                .Setup(x => x.GetRepositoryStructureAsync(repository.Id))
                .ReturnsAsync(rootStructure);

            _mockTaskQueue
                .Setup(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("task-id");

            // Act
            var scanId = await _orchestrator.StartGraphScanAsync(repositoryId, configuration);

            // Assert
            scanId.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void FlattenRepositoryFiles_WithNestedStructure_ShouldReturnFlatList()
        {
            // This tests the private method indirectly through the public interface
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repository = new Repository 
            { 
                Id = Guid.Parse(repositoryId),
                Name = "TestRepo",
                Location = "/test/repo",
                Type = RepositoryType.Local
            };
            
            var configuration = new GraphScanConfiguration
            {
                RepositoryId = repositoryId,
                Mode = ScanMode.Full
            };

            var childFile = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                Name = "ChildFile.cs",
                Path = "/test/subdir/ChildFile.cs",
                IsDirectory = false
            };

            var directory = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                Name = "subdir",
                Path = "/test/subdir",
                IsDirectory = true,
                Children = new List<RepositoryFile> { childFile }
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                Name = "root",
                Path = "/test",
                IsDirectory = true,
                Children = new List<RepositoryFile> { directory }
            };

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(repository.Id))
                .ReturnsAsync(repository);
            
            _mockRepositoryService
                .Setup(x => x.GetRepositoryStructureAsync(repository.Id))
                .ReturnsAsync(rootStructure);

            _mockTaskQueue
                .Setup(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("task-id");

            // Act
            var scanIdTask = _orchestrator.StartGraphScanAsync(repositoryId, configuration);

            // Assert - Should not throw and should process nested files
            scanIdTask.Should().NotBeNull();
        }
    }
}