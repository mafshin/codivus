using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.API.Services;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Codivus.API.Tests.Services
{
    public class PersistentTaskQueueServiceTests : IDisposable
    {
        private readonly Mock<IDataStore> _mockDataStore;
        private readonly Mock<ILogger<PersistentTaskQueueService<TestTask>>> _mockLogger;
        private readonly PersistentTaskQueueService<TestTask> _service;
        private readonly string _queueName = "TestQueue";

        public PersistentTaskQueueServiceTests()
        {
            _mockDataStore = new Mock<IDataStore>();
            _mockLogger = new Mock<ILogger<PersistentTaskQueueService<TestTask>>>();
            _service = new PersistentTaskQueueService<TestTask>(_mockDataStore.Object, _mockLogger.Object, _queueName);
        }

        [Fact]
        public async Task SaveCheckpointAsync_ShouldSaveToDataStore()
        {
            // Arrange
            var taskId = "test-task-1";
            var checkpointData = new { Progress = 50, CurrentFile = "test.cs" };
            _mockDataStore.Setup(ds => ds.SaveAsync(It.IsAny<string>(), It.IsAny<object>(), default))
                .ReturnsAsync(true);

            // Act
            var result = await _service.SaveCheckpointAsync(taskId, checkpointData);

            // Assert
            result.Should().BeTrue();
            _mockDataStore.Verify(ds => ds.SaveAsync($"{_queueName}:checkpoint:{taskId}", It.IsAny<object>(), default), Times.Once);
        }

        [Fact]
        public async Task SaveCheckpointAsync_WhenDataStoreFails_ShouldReturnFalse()
        {
            // Arrange
            var taskId = "test-task-1";
            var checkpointData = new { Progress = 50 };
            _mockDataStore.Setup(ds => ds.SaveAsync(It.IsAny<string>(), It.IsAny<object>(), default))
                .ReturnsAsync(false);

            // Act
            var result = await _service.SaveCheckpointAsync(taskId, checkpointData);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetCheckpointAsync_ShouldRetrieveFromDataStore()
        {
            // Arrange
            var taskId = "test-task-1";
            var expectedCheckpoint = new { Progress = 75, CurrentFile = "another.cs" };
            _mockDataStore.Setup(ds => ds.GetAsync<object>(It.IsAny<string>(), default))
                .ReturnsAsync(expectedCheckpoint);

            // Act
            var result = await _service.GetCheckpointAsync(taskId);

            // Assert
            result.Should().Be(expectedCheckpoint);
            _mockDataStore.Verify(ds => ds.GetAsync<object>($"{_queueName}:checkpoint:{taskId}", default), Times.Once);
        }

        [Fact]
        public async Task GetStaleTasksAsync_ShouldReturnTasksOlderThanTimeout()
        {
            // Arrange
            var timeout = TimeSpan.FromMinutes(30);
            var oldTask = new TestTask
            {
                TaskId = "old-task",
                Status = QueueTaskStatus.InProgress,
                StartedAt = DateTime.UtcNow.AddHours(-1) // 1 hour ago
            };
            var recentTask = new TestTask
            {
                TaskId = "recent-task",
                Status = QueueTaskStatus.InProgress,
                StartedAt = DateTime.UtcNow.AddMinutes(-10) // 10 minutes ago
            };

            // Setup the base service to return these tasks
            await _service.EnqueueAsync(oldTask);
            await _service.EnqueueAsync(recentTask);
            await _service.UpdateTaskStatusAsync(oldTask.TaskId, QueueTaskStatus.InProgress);
            await _service.UpdateTaskStatusAsync(recentTask.TaskId, QueueTaskStatus.InProgress);

            // Act
            var staleTasks = await _service.GetStaleTasksAsync(timeout);

            // Assert
            staleTasks.Should().ContainSingle();
            staleTasks.First().TaskId.Should().Be("old-task");
        }

        [Fact]
        public async Task ArchiveCompletedTasksAsync_ShouldArchiveOldCompletedTasks()
        {
            // Arrange
            var olderThan = TimeSpan.FromDays(1);
            var oldCompletedTask = new TestTask
            {
                TaskId = "old-completed",
                Status = QueueTaskStatus.Completed,
                CompletedAt = DateTime.UtcNow.AddDays(-2) // 2 days ago
            };
            var recentCompletedTask = new TestTask
            {
                TaskId = "recent-completed",
                Status = QueueTaskStatus.Completed,
                CompletedAt = DateTime.UtcNow.AddHours(-12) // 12 hours ago
            };

            await _service.EnqueueAsync(oldCompletedTask);
            await _service.EnqueueAsync(recentCompletedTask);
            
            // Set the completion times manually after updating status
            await _service.UpdateTaskStatusAsync(oldCompletedTask.TaskId, QueueTaskStatus.Completed);
            await _service.UpdateTaskStatusAsync(recentCompletedTask.TaskId, QueueTaskStatus.Completed);
            
            // Get the tasks back and update their completion times since UpdateTaskStatusAsync might override them
            var oldTask = await _service.GetTaskAsync(oldCompletedTask.TaskId);
            var recentTask = await _service.GetTaskAsync(recentCompletedTask.TaskId);
            oldTask.CompletedAt = DateTime.UtcNow.AddDays(-2);
            recentTask.CompletedAt = DateTime.UtcNow.AddHours(-12);

            _mockDataStore.Setup(ds => ds.SaveAsync(It.IsAny<string>(), It.IsAny<TestTask>(), default))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ArchiveCompletedTasksAsync(olderThan);

            // Assert
            result.Should().BeTrue();
            
            // Verify that the old task was archived
            _mockDataStore.Verify(ds => ds.SaveAsync($"{_queueName}:archive:{oldCompletedTask.TaskId}", It.IsAny<TestTask>(), default), Times.Once);
            
            // Verify that the old task was removed from the queue
            var archivedTask = await _service.GetTaskAsync(oldCompletedTask.TaskId);
            archivedTask.Should().BeNull();
            
            // Verify that the recent task is still in the queue
            var stillRecentTask = await _service.GetTaskAsync(recentCompletedTask.TaskId);
            stillRecentTask.Should().NotBeNull();
        }

        [Fact]
        public async Task ArchiveCompletedTasksAsync_WhenNoOldTasks_ShouldReturnFalse()
        {
            // Arrange
            var olderThan = TimeSpan.FromDays(1);
            var recentTask = new TestTask
            {
                TaskId = "recent-completed",
                Status = QueueTaskStatus.Completed,
                CompletedAt = DateTime.UtcNow.AddHours(-12)
            };

            await _service.EnqueueAsync(recentTask);
            await _service.UpdateTaskStatusAsync(recentTask.TaskId, QueueTaskStatus.Completed);

            // Act
            var result = await _service.ArchiveCompletedTasksAsync(olderThan);

            // Assert
            result.Should().BeFalse();
            _mockDataStore.Verify(ds => ds.SaveAsync(It.IsAny<string>(), It.IsAny<TestTask>(), default), Times.Never);
        }

        [Fact]
        public async Task ArchiveCompletedTasksAsync_ShouldHandleFailedTasks()
        {
            // Arrange
            var olderThan = TimeSpan.FromDays(1);
            var oldFailedTask = new TestTask
            {
                TaskId = "old-failed",
                Status = QueueTaskStatus.Failed,
                CompletedAt = DateTime.UtcNow.AddDays(-2)
            };

            await _service.EnqueueAsync(oldFailedTask);
            await _service.UpdateTaskStatusAsync(oldFailedTask.TaskId, QueueTaskStatus.Failed);
            
            // Update the completion time manually
            var task = await _service.GetTaskAsync(oldFailedTask.TaskId);
            task.CompletedAt = DateTime.UtcNow.AddDays(-2);

            _mockDataStore.Setup(ds => ds.SaveAsync(It.IsAny<string>(), It.IsAny<TestTask>(), default))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ArchiveCompletedTasksAsync(olderThan);

            // Assert
            result.Should().BeTrue();
            _mockDataStore.Verify(ds => ds.SaveAsync($"{_queueName}:archive:{oldFailedTask.TaskId}", It.IsAny<TestTask>(), default), Times.Once);
        }

        [Fact]
        public async Task ArchiveCompletedTasksAsync_ShouldHandleCancelledTasks()
        {
            // Arrange
            var olderThan = TimeSpan.FromDays(1);
            var oldCancelledTask = new TestTask
            {
                TaskId = "old-cancelled",
                Status = QueueTaskStatus.Cancelled,
                CompletedAt = DateTime.UtcNow.AddDays(-2)
            };

            await _service.EnqueueAsync(oldCancelledTask);
            await _service.UpdateTaskStatusAsync(oldCancelledTask.TaskId, QueueTaskStatus.Cancelled);
            
            // Update the completion time manually
            var task = await _service.GetTaskAsync(oldCancelledTask.TaskId);
            task.CompletedAt = DateTime.UtcNow.AddDays(-2);

            _mockDataStore.Setup(ds => ds.SaveAsync(It.IsAny<string>(), It.IsAny<TestTask>(), default))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ArchiveCompletedTasksAsync(olderThan);

            // Assert
            result.Should().BeTrue();
            _mockDataStore.Verify(ds => ds.SaveAsync($"{_queueName}:archive:{oldCancelledTask.TaskId}", It.IsAny<TestTask>(), default), Times.Once);
        }

        [Fact]
        public async Task ArchiveCompletedTasksAsync_WhenArchivingFails_ShouldNotRemoveTask()
        {
            // Arrange
            var olderThan = TimeSpan.FromDays(1);
            var oldTask = new TestTask
            {
                TaskId = "old-completed",
                Status = QueueTaskStatus.Completed,
                CompletedAt = DateTime.UtcNow.AddDays(-2)
            };

            await _service.EnqueueAsync(oldTask);
            await _service.UpdateTaskStatusAsync(oldTask.TaskId, QueueTaskStatus.Completed);

            _mockDataStore.Setup(ds => ds.SaveAsync(It.IsAny<string>(), It.IsAny<TestTask>(), default))
                .ReturnsAsync(false); // Simulate archiving failure

            // Act
            var result = await _service.ArchiveCompletedTasksAsync(olderThan);

            // Assert
            result.Should().BeFalse();
            
            // Task should still be in the queue
            var task = await _service.GetTaskAsync(oldTask.TaskId);
            task.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Act & Assert
            _service.Should().NotBeNull();
            _service.Should().BeAssignableTo<IPersistentTaskQueue<TestTask>>();
            _service.Should().BeAssignableTo<ITaskQueue<TestTask>>();
        }

        [Fact]
        public async Task InheritedMethods_ShouldWorkCorrectly()
        {
            // Arrange
            var task = new TestTask { TaskType = "TestType" };

            // Act & Assert - Test that inherited methods from TaskQueueService work
            var taskId = await _service.EnqueueAsync(task);
            taskId.Should().NotBeNullOrEmpty();

            var queueLength = await _service.GetQueueLengthAsync();
            queueLength.Should().Be(1);

            var retrievedTask = await _service.GetTaskAsync(taskId);
            retrievedTask.Should().NotBeNull();
            retrievedTask.TaskId.Should().Be(taskId);
        }

        public void Dispose()
        {
            // No cleanup needed for this test
        }

        // Test task implementation - made public for mocking
        public class TestTask : IQueueTask
        {
            public string TaskId { get; set; } = Guid.NewGuid().ToString();
            public string TaskType { get; set; } = "";
            public QueueTaskStatus Status { get; set; } = QueueTaskStatus.Pending;
            public TaskPriority Priority { get; set; } = TaskPriority.Normal;
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime? StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public int RetryCount { get; set; } = 0;
            public int MaxRetries { get; set; } = 3;
            public string? ErrorMessage { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = new();
        }
    }
}