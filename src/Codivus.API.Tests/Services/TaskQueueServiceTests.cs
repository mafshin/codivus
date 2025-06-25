using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.API.Services;
using Codivus.Core.Models;
using Codivus.Core.Interfaces;

namespace Codivus.API.Tests.Services
{
    public class TaskQueueServiceTests
    {
        private readonly Mock<ILogger<TaskQueueService<TestTask>>> _mockLogger;
        private readonly TaskQueueService<TestTask> _service;

        public TaskQueueServiceTests()
        {
            _mockLogger = new Mock<ILogger<TaskQueueService<TestTask>>>();
            _service = new TaskQueueService<TestTask>(_mockLogger.Object);
        }

        [Fact]
        public async Task EnqueueAsync_WithValidTask_ShouldReturnTaskId()
        {
            // Arrange
            var task = new TestTask
            {
                TaskType = "Test",
                Priority = TaskPriority.Normal
            };

            // Act
            var taskId = await _service.EnqueueAsync(task);

            // Assert
            taskId.Should().NotBeNullOrEmpty();
            task.TaskId.Should().Be(taskId);
            task.Status.Should().Be(QueueTaskStatus.Queued);
            task.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task EnqueueAsync_WithNullTask_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.EnqueueAsync(null!));
        }

        [Fact]
        public async Task EnqueueAsync_WithExistingTaskId_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var task1 = new TestTask { TaskId = "duplicate-id" };
            var task2 = new TestTask { TaskId = "duplicate-id" };

            await _service.EnqueueAsync(task1);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.EnqueueAsync(task2));
        }

        [Fact]
        public async Task DequeueAsync_WithQueuedTask_ShouldReturnTaskInProgress()
        {
            // Arrange
            var task = new TestTask { TaskType = "Test" };
            await _service.EnqueueAsync(task);

            // Act
            var dequeuedTask = await _service.DequeueAsync(CancellationToken.None);

            // Assert
            dequeuedTask.Should().NotBeNull();
            dequeuedTask.TaskId.Should().Be(task.TaskId);
            dequeuedTask.Status.Should().Be(QueueTaskStatus.InProgress);
            dequeuedTask.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task DequeueAsync_WithEmptyQueue_ShouldWaitForTask()
        {
            // Arrange
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                _service.DequeueAsync(cts.Token));
        }

        [Fact]
        public async Task PeekAsync_WithQueuedTask_ShouldReturnTaskWithoutChangingStatus()
        {
            // Arrange
            var task = new TestTask { TaskType = "Test" };
            await _service.EnqueueAsync(task);

            // Act
            var peekedTask = await _service.PeekAsync();

            // Assert
            peekedTask.Should().NotBeNull();
            peekedTask.TaskId.Should().Be(task.TaskId);
            peekedTask.Status.Should().Be(QueueTaskStatus.Queued); // Should remain queued
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithValidTask_ShouldUpdateStatus()
        {
            // Arrange
            var task = new TestTask { TaskType = "Test" };
            var taskId = await _service.EnqueueAsync(task);

            // Act
            var result = await _service.UpdateTaskStatusAsync(taskId, QueueTaskStatus.Completed, "Task completed");

            // Assert
            result.Should().BeTrue();
            
            var updatedTask = await _service.GetTaskAsync(taskId);
            updatedTask.Status.Should().Be(QueueTaskStatus.Completed);
            updatedTask.ErrorMessage.Should().Be("Task completed");
            updatedTask.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_WithNonExistentTask_ShouldReturnFalse()
        {
            // Act
            var result = await _service.UpdateTaskStatusAsync("non-existent", QueueTaskStatus.Completed);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetTaskAsync_WithValidId_ShouldReturnTask()
        {
            // Arrange
            var task = new TestTask { TaskType = "Test" };
            var taskId = await _service.EnqueueAsync(task);

            // Act
            var retrievedTask = await _service.GetTaskAsync(taskId);

            // Assert
            retrievedTask.Should().NotBeNull();
            retrievedTask.TaskId.Should().Be(taskId);
        }

        [Fact]
        public async Task GetTaskAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var result = await _service.GetTaskAsync("invalid-id");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetTasksAsync_WithNoFilter_ShouldReturnAllTasks()
        {
            // Arrange
            var task1 = new TestTask { TaskType = "Test1" };
            var task2 = new TestTask { TaskType = "Test2" };
            await _service.EnqueueAsync(task1);
            await _service.EnqueueAsync(task2);

            // Act
            var tasks = await _service.GetTasksAsync();

            // Assert
            tasks.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetTasksAsync_WithStatusFilter_ShouldReturnFilteredTasks()
        {
            // Arrange
            var task1 = new TestTask { TaskType = "Test1" };
            var task2 = new TestTask { TaskType = "Test2" };
            await _service.EnqueueAsync(task1);
            await _service.EnqueueAsync(task2);
            await _service.UpdateTaskStatusAsync(task1.TaskId, QueueTaskStatus.Completed);

            // Act
            var completedTasks = await _service.GetTasksAsync(QueueTaskStatus.Completed);
            var queuedTasks = await _service.GetTasksAsync(QueueTaskStatus.Queued);

            // Assert
            completedTasks.Should().HaveCount(1);
            queuedTasks.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetQueueLengthAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            await _service.EnqueueAsync(new TestTask { TaskType = "Test1" });
            await _service.EnqueueAsync(new TestTask { TaskType = "Test2" });
            await _service.EnqueueAsync(new TestTask { TaskType = "Test3" });

            // Act
            var totalLength = await _service.GetQueueLengthAsync();
            var queuedLength = await _service.GetQueueLengthAsync(QueueTaskStatus.Queued);

            // Assert
            totalLength.Should().Be(3);
            queuedLength.Should().Be(3); // All should be queued initially
        }

        [Fact]
        public async Task RemoveTaskAsync_WithValidTask_ShouldRemoveTask()
        {
            // Arrange
            var task = new TestTask { TaskType = "Test" };
            var taskId = await _service.EnqueueAsync(task);

            // Act
            var result = await _service.RemoveTaskAsync(taskId);

            // Assert
            result.Should().BeTrue();
            
            var removedTask = await _service.GetTaskAsync(taskId);
            removedTask.Should().BeNull();
        }

        [Fact]
        public async Task PriorityDequeue_ShouldReturnHigherPriorityTasksFirst()
        {
            // Arrange
            var lowTask = new TestTask { TaskType = "Low", Priority = TaskPriority.Low };
            var highTask = new TestTask { TaskType = "High", Priority = TaskPriority.High };
            var normalTask = new TestTask { TaskType = "Normal", Priority = TaskPriority.Normal };
            var criticalTask = new TestTask { TaskType = "Critical", Priority = TaskPriority.Critical };

            // Enqueue in random order
            await _service.EnqueueAsync(lowTask);
            await _service.EnqueueAsync(normalTask);
            await _service.EnqueueAsync(highTask);
            await _service.EnqueueAsync(criticalTask);

            // Act - Dequeue all tasks
            var first = await _service.DequeueAsync();
            var second = await _service.DequeueAsync();
            var third = await _service.DequeueAsync();
            var fourth = await _service.DequeueAsync();

            // Assert - Should be in priority order
            first.Priority.Should().Be(TaskPriority.Critical);
            second.Priority.Should().Be(TaskPriority.High);
            third.Priority.Should().Be(TaskPriority.Normal);
            fourth.Priority.Should().Be(TaskPriority.Low);
        }

        [Fact]
        public async Task RequeueFailedTasksAsync_ShouldRequeueEligibleTasks()
        {
            // Arrange
            var task = new TestTask { TaskType = "Test", MaxRetries = 3 };
            await _service.EnqueueAsync(task);
            await _service.UpdateTaskStatusAsync(task.TaskId, QueueTaskStatus.Failed);
            
            // Make sure the task is old enough
            await Task.Delay(10);

            // Act
            var result = await _service.RequeueFailedTasksAsync(TimeSpan.FromMilliseconds(1));

            // Assert
            result.Should().BeTrue();
            
            var requeuedTask = await _service.GetTaskAsync(task.TaskId);
            requeuedTask.Status.Should().Be(QueueTaskStatus.Queued);
            requeuedTask.RetryCount.Should().Be(1);
        }

        [Fact]
        public async Task ClearQueueAsync_ShouldRemoveAllTasks()
        {
            // Arrange
            await _service.EnqueueAsync(new TestTask { TaskType = "Test1" });
            await _service.EnqueueAsync(new TestTask { TaskType = "Test2" });
            await _service.EnqueueAsync(new TestTask { TaskType = "Test3" });

            // Act
            await _service.ClearQueueAsync();

            // Assert
            var length = await _service.GetQueueLengthAsync();
            length.Should().Be(0);
        }
    }

    // Test implementation of IQueueTask for testing
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
        public string ErrorMessage { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}