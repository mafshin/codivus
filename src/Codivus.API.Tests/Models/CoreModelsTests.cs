using Xunit;
using FluentAssertions;
using Codivus.Core.Models;
using Codivus.Core.Interfaces;

namespace Codivus.API.Tests.Models
{
    public class ScanTaskTests
    {
        [Fact]
        public void ScanTask_DefaultConstructor_ShouldInitializeCorrectly()
        {
            // Act
            var task = new ScanTask();

            // Assert
            task.TaskId.Should().NotBeNullOrEmpty();
            task.Status.Should().Be(QueueTaskStatus.Pending);
            task.Priority.Should().Be(TaskPriority.Normal);
            task.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            task.RetryCount.Should().Be(0);
            task.MaxRetries.Should().Be(3);
            task.Metadata.Should().NotBeNull();
            task.Metadata.Should().BeEmpty();
            task.Tags.Should().NotBeNull();
            task.Tags.Should().BeEmpty();
        }

        [Fact]
        public void ScanTask_WithProperties_ShouldSetCorrectly()
        {
            // Arrange
            var taskType = "TestTask";
            var createdBy = "TestUser";
            var estimatedDuration = TimeSpan.FromMinutes(30);

            // Act
            var task = new ScanTask
            {
                TaskType = taskType,
                Status = QueueTaskStatus.InProgress,
                Priority = TaskPriority.High,
                CreatedBy = createdBy,
                EstimatedDuration = estimatedDuration,
                MaxRetries = 5
            };

            // Assert
            task.TaskType.Should().Be(taskType);
            task.Status.Should().Be(QueueTaskStatus.InProgress);
            task.Priority.Should().Be(TaskPriority.High);
            task.CreatedBy.Should().Be(createdBy);
            task.EstimatedDuration.Should().Be(estimatedDuration);
            task.MaxRetries.Should().Be(5);
        }

        [Theory]
        [InlineData(QueueTaskStatus.Pending)]
        [InlineData(QueueTaskStatus.Queued)]
        [InlineData(QueueTaskStatus.InProgress)]
        [InlineData(QueueTaskStatus.Completed)]
        [InlineData(QueueTaskStatus.Failed)]
        [InlineData(QueueTaskStatus.Cancelled)]
        [InlineData(QueueTaskStatus.Paused)]
        public void ScanTask_WithDifferentStatuses_ShouldSetCorrectly(QueueTaskStatus status)
        {
            // Act
            var task = new ScanTask { Status = status };

            // Assert
            task.Status.Should().Be(status);
        }

        [Theory]
        [InlineData(TaskPriority.Low)]
        [InlineData(TaskPriority.Normal)]
        [InlineData(TaskPriority.High)]
        [InlineData(TaskPriority.Critical)]
        public void ScanTask_WithDifferentPriorities_ShouldSetCorrectly(TaskPriority priority)
        {
            // Act
            var task = new ScanTask { Priority = priority };

            // Assert
            task.Priority.Should().Be(priority);
        }

        [Fact]
        public void ScanTask_Metadata_ShouldStoreCustomData()
        {
            // Arrange
            var task = new ScanTask();

            // Act
            task.Metadata["key1"] = "value1";
            task.Metadata["key2"] = 42;
            task.Metadata["key3"] = new { nested = "object" };

            // Assert
            task.Metadata.Should().HaveCount(3);
            task.Metadata["key1"].Should().Be("value1");
            task.Metadata["key2"].Should().Be(42);
            task.Metadata["key3"].Should().NotBeNull();
        }

        [Fact]
        public void ScanTask_Tags_ShouldStoreTags()
        {
            // Arrange
            var task = new ScanTask();

            // Act
            task.Tags.Add("urgent");
            task.Tags.Add("security");
            task.Tags.Add("refactoring");

            // Assert
            task.Tags.Should().HaveCount(3);
            task.Tags.Should().Contain("urgent");
            task.Tags.Should().Contain("security");
            task.Tags.Should().Contain("refactoring");
        }

        [Fact]
        public void ScanTask_ShouldImplementIQueueTask()
        {
            // Act
            var task = new ScanTask();

            // Assert
            task.Should().BeAssignableTo<IQueueTask>();
        }
    }

    public class GraphScanTaskTests
    {
        [Fact]
        public void GraphScanTask_DefaultConstructor_ShouldInitializeCorrectly()
        {
            // Act
            var task = new GraphScanTask();

            // Assert
            task.TaskType.Should().Be("GraphScan");
            task.FileIds.Should().NotBeNull();
            task.FileIds.Should().BeEmpty();
            task.Options.Should().NotBeNull();
            task.Checkpoint.Should().NotBeNull();
        }

        [Fact]
        public void GraphScanTask_WithProperties_ShouldSetCorrectly()
        {
            // Arrange
            var repositoryId = "repo-123";
            var scanId = "scan-456";
            var targetPath = "/test/path";
            var projectId = "project-789";

            // Act
            var task = new GraphScanTask
            {
                RepositoryId = repositoryId,
                ScanId = scanId,
                Scope = ScanScope.Project,
                TargetPath = targetPath,
                ProjectId = projectId
            };

            // Assert
            task.RepositoryId.Should().Be(repositoryId);
            task.ScanId.Should().Be(scanId);
            task.Scope.Should().Be(ScanScope.Project);
            task.TargetPath.Should().Be(targetPath);
            task.ProjectId.Should().Be(projectId);
        }

        [Theory]
        [InlineData(ScanScope.File)]
        [InlineData(ScanScope.Directory)]
        [InlineData(ScanScope.Project)]
        [InlineData(ScanScope.Solution)]
        [InlineData(ScanScope.Repository)]
        public void GraphScanTask_WithDifferentScopes_ShouldSetCorrectly(ScanScope scope)
        {
            // Act
            var task = new GraphScanTask { Scope = scope };

            // Assert
            task.Scope.Should().Be(scope);
        }

        [Fact]
        public void GraphScanTask_FileIds_ShouldStoreFileIds()
        {
            // Arrange
            var task = new GraphScanTask();
            var fileIds = new List<string> { "file1", "file2", "file3" };

            // Act
            task.FileIds.AddRange(fileIds);

            // Assert
            task.FileIds.Should().HaveCount(3);
            task.FileIds.Should().ContainInOrder("file1", "file2", "file3");
        }

        [Fact]
        public void GraphScanTask_ShouldImplementIGraphScanTask()
        {
            // Act
            var task = new GraphScanTask();

            // Assert
            task.Should().BeAssignableTo<IGraphScanTask>();
        }
    }

    public class GraphScanOptionsTests
    {
        [Fact]
        public void GraphScanOptions_DefaultConstructor_ShouldInitializeCorrectly()
        {
            // Act
            var options = new GraphScanOptions();

            // Assert
            options.FullScan.Should().BeFalse();
            options.IncludeTests.Should().BeFalse();
            options.AnalyzeGeneratedCode.Should().BeFalse();
            options.MaxFileSizeBytes.Should().Be(1048576); // 1MB
            options.BuildRelationships.Should().BeTrue();
            options.CalculateMetrics.Should().BeTrue();
            options.BatchSize.Should().Be(100);
            options.IncludePatterns.Should().NotBeNull();
            options.ExcludePatterns.Should().NotBeNull();
            options.IncludePatterns.Should().BeEmpty();
            options.ExcludePatterns.Should().BeEmpty();
        }

        [Fact]
        public void GraphScanOptions_WithProperties_ShouldSetCorrectly()
        {
            // Act
            var options = new GraphScanOptions
            {
                FullScan = true,
                IncludeTests = true,
                AnalyzeGeneratedCode = true,
                MaxFileSizeBytes = 2 * 1024 * 1024, // 2MB
                BuildRelationships = false,
                CalculateMetrics = false,
                BatchSize = 50
            };

            // Assert
            options.FullScan.Should().BeTrue();
            options.IncludeTests.Should().BeTrue();
            options.AnalyzeGeneratedCode.Should().BeTrue();
            options.MaxFileSizeBytes.Should().Be(2 * 1024 * 1024);
            options.BuildRelationships.Should().BeFalse();
            options.CalculateMetrics.Should().BeFalse();
            options.BatchSize.Should().Be(50);
        }

        [Fact]
        public void GraphScanOptions_Patterns_ShouldStorePatterns()
        {
            // Arrange
            var options = new GraphScanOptions();

            // Act
            options.IncludePatterns.Add("*.cs");
            options.IncludePatterns.Add("*.vb");
            options.ExcludePatterns.Add("*.Designer.cs");
            options.ExcludePatterns.Add("*.g.cs");

            // Assert
            options.IncludePatterns.Should().HaveCount(2);
            options.IncludePatterns.Should().Contain("*.cs");
            options.IncludePatterns.Should().Contain("*.vb");
            options.ExcludePatterns.Should().HaveCount(2);
            options.ExcludePatterns.Should().Contain("*.Designer.cs");
            options.ExcludePatterns.Should().Contain("*.g.cs");
        }
    }

    public class GraphScanCheckpointTests
    {
        [Fact]
        public void GraphScanCheckpoint_DefaultConstructor_ShouldInitializeCollections()
        {
            // Act
            var checkpoint = new GraphScanCheckpoint();

            // Assert
            checkpoint.ProcessedFiles.Should().Be(0);
            checkpoint.TotalFiles.Should().Be(0);
            checkpoint.State.Should().NotBeNull();
            checkpoint.ProcessedFileIds.Should().NotBeNull();
            checkpoint.FailedFileIds.Should().NotBeNull();
            checkpoint.State.Should().BeEmpty();
            checkpoint.ProcessedFileIds.Should().BeEmpty();
            checkpoint.FailedFileIds.Should().BeEmpty();
        }

        [Fact]
        public void GraphScanCheckpoint_WithData_ShouldStoreCorrectly()
        {
            // Arrange
            var checkpoint = new GraphScanCheckpoint();

            // Act
            checkpoint.ProcessedFiles = 25;
            checkpoint.TotalFiles = 100;
            checkpoint.LastProcessedFile = "/test/file25.cs";
            checkpoint.ProcessedFileIds.AddRange(new[] { "file1", "file2", "file3" });
            checkpoint.FailedFileIds.Add("failed-file");
            checkpoint.State["currentPhase"] = "analysis";
            checkpoint.State["startTime"] = DateTime.UtcNow;

            // Assert
            checkpoint.ProcessedFiles.Should().Be(25);
            checkpoint.TotalFiles.Should().Be(100);
            checkpoint.LastProcessedFile.Should().Be("/test/file25.cs");
            checkpoint.ProcessedFileIds.Should().HaveCount(3);
            checkpoint.FailedFileIds.Should().HaveCount(1);
            checkpoint.State.Should().HaveCount(2);
            checkpoint.State["currentPhase"].Should().Be("analysis");
        }
    }

    public class GraphScanConfigurationTests
    {
        [Fact]
        public void GraphScanConfiguration_DefaultConstructor_ShouldInitializeCorrectly()
        {
            // Act
            var config = new GraphScanConfiguration();

            // Assert
            config.Enabled.Should().BeTrue();
            config.Mode.Should().Be(ScanMode.Incremental);
            config.Processing.Should().NotBeNull();
            config.Analysis.Should().NotBeNull();
            config.Relationships.Should().NotBeNull();
            config.Metrics.Should().NotBeNull();
        }

        [Theory]
        [InlineData(ScanMode.Full)]
        [InlineData(ScanMode.Incremental)]
        [InlineData(ScanMode.Differential)]
        public void GraphScanConfiguration_WithDifferentModes_ShouldSetCorrectly(ScanMode mode)
        {
            // Act
            var config = new GraphScanConfiguration { Mode = mode };

            // Assert
            config.Mode.Should().Be(mode);
        }
    }

    public class ProcessingConfigurationTests
    {
        [Fact]
        public void ProcessingConfiguration_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var config = new ProcessingConfiguration();

            // Assert
            config.MaxConcurrentTasks.Should().Be(4);
            config.BatchSize.Should().Be(100);
            config.TimeoutMinutes.Should().Be(30);
            config.EnableCheckpoints.Should().BeTrue();
            config.CheckpointIntervalMinutes.Should().Be(5);
            config.ContinueOnError.Should().BeTrue();
            config.MaxErrorsBeforeStop.Should().Be(100);
        }
    }

    public class AnalysisConfigurationTests
    {
        [Fact]
        public void AnalysisConfiguration_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var config = new AnalysisConfiguration();

            // Assert
            config.IncludedExtensions.Should().Contain(".cs");
            config.IncludedExtensions.Should().Contain(".vb");
            config.ExcludedPatterns.Should().Contain("**/bin/**");
            config.ExcludedPatterns.Should().Contain("**/obj/**");
            config.AnalyzeTests.Should().BeFalse();
            config.AnalyzeGeneratedCode.Should().BeFalse();
            config.MaxFileSizeMB.Should().Be(1);
        }
    }

    public class GraphScanProgressTests
    {
        [Fact]
        public void GraphScanProgress_DefaultConstructor_ShouldInitializeCollections()
        {
            // Act
            var progress = new GraphScanProgress();

            // Assert
            progress.RecentErrors.Should().NotBeNull();
            progress.StatsByNodeType.Should().NotBeNull();
            progress.RecentErrors.Should().BeEmpty();
            progress.StatsByNodeType.Should().BeEmpty();
        }

        [Fact]
        public void GraphScanProgress_WithData_ShouldStoreCorrectly()
        {
            // Arrange
            var scanId = "scan-123";
            var repositoryId = "repo-456";

            // Act
            var progress = new GraphScanProgress
            {
                ScanId = scanId,
                RepositoryId = repositoryId,
                Status = ScanStatus.InProgress,
                TotalTasks = 10,
                CompletedTasks = 6,
                FailedTasks = 1,
                TotalFiles = 100,
                ProcessedFiles = 60,
                NodesCreated = 1500,
                RelationshipsCreated = 3000,
                ElapsedMilliseconds = 30000,
                EstimatedTimeRemainingMinutes = 5.5,
                CurrentTask = "Analyzing file.cs"
            };

            // Assert
            progress.ScanId.Should().Be(scanId);
            progress.RepositoryId.Should().Be(repositoryId);
            progress.Status.Should().Be(ScanStatus.InProgress);
            progress.TotalTasks.Should().Be(10);
            progress.CompletedTasks.Should().Be(6);
            progress.FailedTasks.Should().Be(1);
            progress.TotalFiles.Should().Be(100);
            progress.ProcessedFiles.Should().Be(60);
            progress.NodesCreated.Should().Be(1500);
            progress.RelationshipsCreated.Should().Be(3000);
            progress.ElapsedMilliseconds.Should().Be(30000);
            progress.EstimatedTimeRemainingMinutes.Should().Be(5.5);
            progress.CurrentTask.Should().Be("Analyzing file.cs");
        }

        [Theory]
        [InlineData(ScanStatus.Pending)]
        [InlineData(ScanStatus.Initializing)]
        [InlineData(ScanStatus.InProgress)]
        [InlineData(ScanStatus.Paused)]
        [InlineData(ScanStatus.Completed)]
        [InlineData(ScanStatus.Failed)]
        [InlineData(ScanStatus.Canceled)]
        public void GraphScanProgress_WithDifferentStatuses_ShouldSetCorrectly(ScanStatus status)
        {
            // Act
            var progress = new GraphScanProgress { Status = status };

            // Assert
            progress.Status.Should().Be(status);
        }
    }
}