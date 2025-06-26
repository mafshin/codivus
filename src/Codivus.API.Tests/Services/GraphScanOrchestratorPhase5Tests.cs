using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codivus.API.Services;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Codivus.API.Tests.Services
{
    public class GraphScanOrchestratorPhase5Tests
    {
        private readonly Mock<ITaskQueue<GraphScanTask>> _mockTaskQueue;
        private readonly Mock<IRepositoryService> _mockRepositoryService;
        private readonly Mock<ILogger<GraphScanOrchestrator>> _mockLogger;
        private readonly GraphScanOrchestrator _orchestrator;

        public GraphScanOrchestratorPhase5Tests()
        {
            _mockTaskQueue = new Mock<ITaskQueue<GraphScanTask>>();
            _mockRepositoryService = new Mock<IRepositoryService>();
            _mockLogger = new Mock<ILogger<GraphScanOrchestrator>>();

            _orchestrator = new GraphScanOrchestrator(
                _mockTaskQueue.Object,
                _mockRepositoryService.Object,
                _mockLogger.Object);
        }

        #region StartGraphScanAsync Tests

        [Fact]
        public async Task StartGraphScanAsync_ValidRepository_ShouldCreateScanTasks()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repository = new Repository
            {
                Id = Guid.Parse(repositoryId),
                Name = "test-repo",
                Location = "/test/path"
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = Guid.Parse(repositoryId),
                Name = "src",
                Path = "/test/path/src",
                IsDirectory = true,
                Children = new List<RepositoryFile>
                {
                    new RepositoryFile
                    {
                        Id = Guid.NewGuid(),
                        RepositoryId = Guid.Parse(repositoryId),
                        Name = "Test.cs",
                        Path = "/test/path/src/Test.cs",
                        IsDirectory = false
                    },
                    new RepositoryFile
                    {
                        Id = Guid.NewGuid(),
                        RepositoryId = Guid.Parse(repositoryId),
                        Name = "Another.cs",
                        Path = "/test/path/src/Another.cs",
                        IsDirectory = false
                    }
                }
            };

            var configuration = new GraphScanConfiguration
            {
                Mode = ScanMode.Full,
                Processing = new ProcessingConfiguration { BatchSize = 10 },
                Analysis = new AnalysisConfiguration
                {
                    IncludedExtensions = new List<string> { ".cs" },
                    ExcludedPatterns = new List<string> { "**/bin/**" }
                }
            };

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(repository);

            _mockRepositoryService
                .Setup(x => x.GetRepositoryStructureAsync(It.IsAny<Guid>()))
                .ReturnsAsync(rootStructure);

            _mockTaskQueue
                .Setup(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            // Act
            var scanId = await _orchestrator.StartGraphScanAsync(repositoryId, configuration);

            // Assert
            scanId.Should().NotBeNullOrEmpty();
            _mockTaskQueue.Verify(
                x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartGraphScanAsync_InvalidRepositoryId_ShouldThrowException()
        {
            // Arrange
            var invalidRepositoryId = "invalid-guid";
            var configuration = new GraphScanConfiguration();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _orchestrator.StartGraphScanAsync(invalidRepositoryId, configuration));
        }

        [Fact]
        public async Task StartGraphScanAsync_NonExistentRepository_ShouldThrowException()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var configuration = new GraphScanConfiguration();

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Repository)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _orchestrator.StartGraphScanAsync(repositoryId, configuration));
        }

        #endregion

        #region GetScanProgressAsync Tests

        [Fact]
        public async Task GetScanProgressAsync_ExistingScan_ShouldReturnProgress()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();
            var repositoryId = Guid.NewGuid().ToString();

            // First start a scan to create progress
            var repository = new Repository
            {
                Id = Guid.Parse(repositoryId),
                Name = "test-repo",
                Location = "/test/path"
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = Guid.Parse(repositoryId),
                Name = "src",
                Path = "/test/path/src",
                IsDirectory = true,
                Children = new List<RepositoryFile>
                {
                    new RepositoryFile
                    {
                        Id = Guid.NewGuid(),
                        RepositoryId = Guid.Parse(repositoryId),
                        Name = "Test.cs",
                        Path = "/test/path/src/Test.cs",
                        IsDirectory = false
                    }
                }
            };

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(repository);

            _mockRepositoryService
                .Setup(x => x.GetRepositoryStructureAsync(It.IsAny<Guid>()))
                .ReturnsAsync(rootStructure);

            _mockTaskQueue
                .Setup(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("");

            // Mock task queue to return some tasks
            var tasks = new List<GraphScanTask>
            {
                new GraphScanTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    ScanId = scanId,
                    Status = QueueTaskStatus.Completed,
                    RepositoryId = repositoryId,
                    TargetPath = "/test/path",
                    Checkpoint = new GraphScanCheckpoint { ProcessedFiles = 5 }
                },
                new GraphScanTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    ScanId = scanId,
                    Status = QueueTaskStatus.InProgress,
                    RepositoryId = repositoryId,
                    TargetPath = "/test/path/src"
                }
            };

            _mockTaskQueue
                .Setup(x => x.GetTasksAsync(It.IsAny<QueueTaskStatus?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            var configuration = new GraphScanConfiguration { Mode = ScanMode.Full };

            // Start scan first
            var actualScanId = await _orchestrator.StartGraphScanAsync(repositoryId, configuration);

            // Act
            var progress = await _orchestrator.GetScanProgressAsync(actualScanId);

            // Assert
            progress.Should().NotBeNull();
            progress.ScanId.Should().Be(actualScanId);
            progress.RepositoryId.Should().Be(repositoryId);
        }

        [Fact]
        public async Task GetScanProgressAsync_NonExistentScan_ShouldReturnNull()
        {
            // Arrange
            var nonExistentScanId = Guid.NewGuid().ToString();

            // Act
            var progress = await _orchestrator.GetScanProgressAsync(nonExistentScanId);

            // Assert
            progress.Should().BeNull();
        }

        #endregion

        #region Scan Control Tests

        [Fact]
        public async Task PauseScanAsync_ExistingScan_ShouldPauseQueuedTasks()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();
            var tasks = new List<GraphScanTask>
            {
                new GraphScanTask
                {
                    TaskId = "task1",
                    ScanId = scanId,
                    Status = QueueTaskStatus.Queued
                },
                new GraphScanTask
                {
                    TaskId = "task2",
                    ScanId = scanId,
                    Status = QueueTaskStatus.Queued
                }
            };

            _mockTaskQueue
                .Setup(x => x.GetTasksAsync(It.IsAny<QueueTaskStatus?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            _mockTaskQueue
                .Setup(x => x.UpdateTaskStatusAsync(
                    It.IsAny<string>(),
                    QueueTaskStatus.Paused,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.PauseScanAsync(scanId);

            // Assert
            result.Should().BeTrue();
            _mockTaskQueue.Verify(
                x => x.UpdateTaskStatusAsync(
                    It.IsAny<string>(),
                    QueueTaskStatus.Paused,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task ResumeScanAsync_PausedScan_ShouldRequeueTasks()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();
            var tasks = new List<GraphScanTask>
            {
                new GraphScanTask
                {
                    TaskId = "task1",
                    ScanId = scanId,
                    Status = QueueTaskStatus.Paused
                },
                new GraphScanTask
                {
                    TaskId = "task2",
                    ScanId = scanId,
                    Status = QueueTaskStatus.Paused
                }
            };

            _mockTaskQueue
                .Setup(x => x.GetTasksAsync(It.IsAny<QueueTaskStatus?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            _mockTaskQueue
                .Setup(x => x.UpdateTaskStatusAsync(
                    It.IsAny<string>(),
                    QueueTaskStatus.Queued,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.ResumeScanAsync(scanId);

            // Assert
            result.Should().BeTrue();
            _mockTaskQueue.Verify(
                x => x.UpdateTaskStatusAsync(
                    It.IsAny<string>(),
                    QueueTaskStatus.Queued,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task CancelScanAsync_ActiveScan_ShouldCancelAllTasks()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();
            var tasks = new List<GraphScanTask>
            {
                new GraphScanTask
                {
                    TaskId = "task1",
                    ScanId = scanId,
                    Status = QueueTaskStatus.Queued
                },
                new GraphScanTask
                {
                    TaskId = "task2",
                    ScanId = scanId,
                    Status = QueueTaskStatus.Paused
                }
            };

            _mockTaskQueue
                .Setup(x => x.GetTasksAsync(It.IsAny<QueueTaskStatus?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            _mockTaskQueue
                .Setup(x => x.UpdateTaskStatusAsync(
                    It.IsAny<string>(),
                    QueueTaskStatus.Cancelled,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.CancelScanAsync(scanId);

            // Assert
            result.Should().BeTrue();
            _mockTaskQueue.Verify(
                x => x.UpdateTaskStatusAsync(
                    It.IsAny<string>(),
                    QueueTaskStatus.Cancelled,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        #endregion

        #region Configuration Tests

        [Theory]
        [InlineData(ScanMode.Full)]
        [InlineData(ScanMode.Incremental)]
        [InlineData(ScanMode.Differential)]
        public async Task StartGraphScanAsync_DifferentScanModes_ShouldCreateAppropriateTask(ScanMode scanMode)
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repository = new Repository
            {
                Id = Guid.Parse(repositoryId),
                Name = "test-repo",
                Location = "/test/path"
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = Guid.Parse(repositoryId),
                Name = "test.cs",
                Path = "/test/path/test.cs",
                IsDirectory = false
            };

            var configuration = new GraphScanConfiguration
            {
                Mode = scanMode,
                Processing = new ProcessingConfiguration { BatchSize = 1 },
                Analysis = new AnalysisConfiguration { IncludedExtensions = new List<string> { ".cs" } }
            };

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(repository);

            _mockRepositoryService
                .Setup(x => x.GetRepositoryStructureAsync(It.IsAny<Guid>()))
                .ReturnsAsync(rootStructure);

            GraphScanTask capturedTask = null;
            _mockTaskQueue
                .Setup(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()))
                .Callback<GraphScanTask, CancellationToken>((task, ct) => capturedTask = task)
                .ReturnsAsync("");

            // Act
            var scanId = await _orchestrator.StartGraphScanAsync(repositoryId, configuration);

            // Assert
            scanId.Should().NotBeNullOrEmpty();
            capturedTask.Should().NotBeNull();
            capturedTask.Options.FullScan.Should().Be(scanMode == ScanMode.Full);
        }

        [Fact]
        public async Task StartGraphScanAsync_WithAnalysisConfiguration_ShouldApplySettings()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repository = new Repository
            {
                Id = Guid.Parse(repositoryId),
                Name = "test-repo",
                Location = "/test/path"
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = Guid.Parse(repositoryId),
                Name = "test.cs",
                Path = "/test/path/test.cs",
                IsDirectory = false
            };

            var configuration = new GraphScanConfiguration
            {
                Mode = ScanMode.Full,
                Processing = new ProcessingConfiguration { BatchSize = 50 },
                Analysis = new AnalysisConfiguration
                {
                    AnalyzeTests = true,
                    AnalyzeGeneratedCode = false,
                    MaxFileSizeMB = 5
                },
                Relationships = new RelationshipConfiguration
                {
                    TrackCalls = true,
                    TrackInheritance = false
                },
                Metrics = new MetricsConfiguration
                {
                    CalculateComplexity = true,
                    CalculateCoupling = false
                }
            };

            _mockRepositoryService
                .Setup(x => x.GetRepositoryByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(repository);

            _mockRepositoryService
                .Setup(x => x.GetRepositoryStructureAsync(It.IsAny<Guid>()))
                .ReturnsAsync(rootStructure);

            GraphScanTask capturedTask = null;
            _mockTaskQueue
                .Setup(x => x.EnqueueAsync(It.IsAny<GraphScanTask>(), It.IsAny<CancellationToken>()))
                .Callback<GraphScanTask, CancellationToken>((task, ct) => capturedTask = task)
                .ReturnsAsync("");

            // Act
            var scanId = await _orchestrator.StartGraphScanAsync(repositoryId, configuration);

            // Assert
            capturedTask.Should().NotBeNull();
            capturedTask.Options.IncludeTests.Should().BeTrue();
            capturedTask.Options.AnalyzeGeneratedCode.Should().BeFalse();
            capturedTask.Options.MaxFileSizeBytes.Should().Be(5 * 1024 * 1024);
            capturedTask.Options.BuildRelationships.Should().BeTrue();
            capturedTask.Options.CalculateMetrics.Should().BeTrue();
            capturedTask.Options.BatchSize.Should().Be(50);
        }

        #endregion
    }
}