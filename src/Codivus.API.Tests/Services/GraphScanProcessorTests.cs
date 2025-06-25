using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Codivus.API.Interfaces;
using Codivus.API.Services;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using System.IO;

namespace Codivus.API.Tests.Services
{
    public class GraphScanProcessorTests : IDisposable
    {
        private readonly Mock<IRoslynAnalysisService> _mockRoslynService;
        private readonly Mock<IGraphStorageService> _mockGraphStorage;
        private readonly Mock<IRepositoryService> _mockRepositoryService;
        private readonly Mock<ILogger<GraphScanProcessor>> _mockLogger;
        private readonly Mock<IGraphTransaction> _mockTransaction;
        private readonly GraphScanProcessor _processor;
        private readonly string _tempDirectory;

        public GraphScanProcessorTests()
        {
            _mockRoslynService = new Mock<IRoslynAnalysisService>();
            _mockGraphStorage = new Mock<IGraphStorageService>();
            _mockRepositoryService = new Mock<IRepositoryService>();
            _mockLogger = new Mock<ILogger<GraphScanProcessor>>();
            _mockTransaction = new Mock<IGraphTransaction>();
            
            _processor = new GraphScanProcessor(
                _mockRoslynService.Object,
                _mockGraphStorage.Object,
                _mockRepositoryService.Object,
                _mockLogger.Object);

            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Act & Assert
            _processor.Should().NotBeNull();
        }

        [Fact]
        public async Task ProcessTaskAsync_WithValidTask_ShouldProcessSuccessfully()
        {
            // Arrange
            var repositoryId = Guid.NewGuid();
            var repository = new Repository
            {
                Id = repositoryId,
                Name = "Test Repository",
                Location = _tempDirectory
            };

            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = repositoryId.ToString(),
                ScanId = "test-scan",
                Scope = ScanScope.Repository,
                TargetPath = _tempDirectory,
                Options = new GraphScanOptions
                {
                    BatchSize = 10,
                    BuildRelationships = true,
                    ContinueOnError = true,
                    IncludeTests = true
                }
            };

            // Create a test file
            var testFilePath = Path.Combine(_tempDirectory, "TestFile.cs");
            await File.WriteAllTextAsync(testFilePath, "public class TestClass { }");

            var testFile = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = "TestFile.cs",
                Path = testFilePath,
                IsDirectory = false
            };

            var analysisResult = new CodeAnalysisResult
            {
                FileId = "file-1",
                FilePath = testFilePath,
                RepositoryId = repositoryId.ToString(),
                Nodes = new List<CodeNode>
                {
                    new CodeNode
                    {
                        Id = "node-1",
                        Name = "TestClass",
                        NodeType = NodeType.Type,
                        RepositoryId = repositoryId.ToString()
                    }
                },
                Relationships = new List<CodeRelationship>()
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = Path.GetFileName(_tempDirectory),
                Path = _tempDirectory,
                IsDirectory = true,
                Children = new List<RepositoryFile> { testFile }
            };

            // Setup mocks
            _mockRepositoryService.Setup(x => x.GetRepositoryByIdAsync(repositoryId))
                .ReturnsAsync(repository);
            
            _mockRepositoryService.Setup(x => x.GetRepositoryStructureAsync(repositoryId))
                .ReturnsAsync(rootStructure);

            _mockGraphStorage.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockGraphStorage.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object);

            _mockTransaction.Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            _mockRoslynService.Setup(x => x.AnalyzeFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(analysisResult);

            _mockGraphStorage.Setup(x => x.CreateNodesAsync(It.IsAny<IEnumerable<CodeNode>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(analysisResult.Nodes);

            // Act
            await _processor.ProcessTaskAsync(task, CancellationToken.None);

            // Assert
            task.Status.Should().Be(QueueTaskStatus.Completed);
            task.StartedAt.Should().NotBeNull();
            task.CompletedAt.Should().NotBeNull();
            task.Checkpoint.TotalFiles.Should().BeGreaterThan(0, "Total files should be set");
            task.Checkpoint.ProcessedFiles.Should().BeGreaterThan(0, "At least one file should be processed");

            // Verify service calls
            _mockGraphStorage.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockRoslynService.Verify(x => x.AnalyzeFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockGraphStorage.Verify(x => x.CreateNodesAsync(It.IsAny<IEnumerable<CodeNode>>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockTransaction.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task ProcessTaskAsync_WithInvalidRepositoryId_ShouldThrowException()
        {
            // Arrange
            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = "invalid-guid",
                ScanId = "test-scan"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _processor.ProcessTaskAsync(task, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessTaskAsync_WithNonExistentRepository_ShouldThrowException()
        {
            // Arrange
            var repositoryId = Guid.NewGuid();
            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = repositoryId.ToString(),
                ScanId = "test-scan"
            };

            _mockRepositoryService.Setup(x => x.GetRepositoryByIdAsync(repositoryId))
                .ReturnsAsync((Repository)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _processor.ProcessTaskAsync(task, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessTaskAsync_WithCancellation_ShouldHandleCorrectly()
        {
            // Arrange
            var repositoryId = Guid.NewGuid();
            var repository = new Repository
            {
                Id = repositoryId,
                Name = "Test Repository",
                Location = _tempDirectory
            };

            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = repositoryId.ToString(),
                ScanId = "test-scan",
                Scope = ScanScope.Repository,
                TargetPath = _tempDirectory,
                Options = new GraphScanOptions
                {
                    IncludeTests = true
                }
            };

            // Create a test file
            var testFilePath = Path.Combine(_tempDirectory, "TestFile.cs");
            await File.WriteAllTextAsync(testFilePath, "public class TestClass { }");

            var testFile = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = "TestFile.cs",
                Path = testFilePath,
                IsDirectory = false
            };

            var rootStructure = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = Path.GetFileName(_tempDirectory),
                Path = _tempDirectory,
                IsDirectory = true,
                Children = new List<RepositoryFile> { testFile }
            };

            var cancellationTokenSource = new CancellationTokenSource();
            
            _mockRepositoryService.Setup(x => x.GetRepositoryByIdAsync(repositoryId))
                .ReturnsAsync(repository);

            _mockRepositoryService.Setup(x => x.GetRepositoryStructureAsync(repositoryId))
                .ReturnsAsync(rootStructure);

            _mockGraphStorage.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
            // Cancel the token before processing starts to ensure cancellation is detected early
            _mockGraphStorage.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Callback(() => cancellationTokenSource.Cancel())
                .ReturnsAsync(_mockTransaction.Object);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _processor.ProcessTaskAsync(task, cancellationTokenSource.Token));

            task.Status.Should().Be(QueueTaskStatus.Cancelled);
        }

        [Fact]
        public async Task ProcessTaskAsync_WithAnalysisError_ShouldContinueOnError()
        {
            // Arrange
            var repositoryId = Guid.NewGuid();
            var repository = new Repository
            {
                Id = repositoryId,
                Name = "Test Repository",
                Location = _tempDirectory
            };

            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = repositoryId.ToString(),
                ScanId = "test-scan",
                Scope = ScanScope.Repository,
                TargetPath = _tempDirectory,
                Options = new GraphScanOptions
                {
                    ContinueOnError = true,
                    BatchSize = 1,
                    IncludeTests = true
                }
            };

            // Create test files
            var testFile1 = Path.Combine(_tempDirectory, "TestFile1.cs");
            var testFile2 = Path.Combine(_tempDirectory, "TestFile2.cs");
            await File.WriteAllTextAsync(testFile1, "public class TestClass1 { }");
            await File.WriteAllTextAsync(testFile2, "public class TestClass2 { }");

            var repositoryFile1 = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = "TestFile1.cs",
                Path = testFile1,
                IsDirectory = false
            };

            var repositoryFile2 = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = "TestFile2.cs",
                Path = testFile2,
                IsDirectory = false
            };

            var rootStructureWithFiles = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = Path.GetFileName(_tempDirectory),
                Path = _tempDirectory,
                IsDirectory = true,
                Children = new List<RepositoryFile> { repositoryFile1, repositoryFile2 }
            };

            // Setup mocks
            _mockRepositoryService.Setup(x => x.GetRepositoryByIdAsync(repositoryId))
                .ReturnsAsync(repository);

            _mockRepositoryService.Setup(x => x.GetRepositoryStructureAsync(repositoryId))
                .ReturnsAsync(rootStructureWithFiles);

            _mockGraphStorage.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockGraphStorage.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object);

            _mockTransaction.Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            // First file succeeds
            _mockRoslynService.Setup(x => x.AnalyzeFileAsync(testFile1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodeAnalysisResult
                {
                    FileId = "file-1",
                    FilePath = testFile1,
                    RepositoryId = repositoryId.ToString(),
                    Nodes = new List<CodeNode>()
                });

            // Second file fails
            _mockRoslynService.Setup(x => x.AnalyzeFileAsync(testFile2, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Analysis failed"));

            _mockGraphStorage.Setup(x => x.CreateNodesAsync(It.IsAny<IEnumerable<CodeNode>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CodeNode>());

            // Act
            await _processor.ProcessTaskAsync(task, CancellationToken.None);

            // Assert
            task.Status.Should().Be(QueueTaskStatus.Completed);
            task.Checkpoint.ProcessedFiles.Should().Be(2); // Both files processed (one successfully, one with error)
            task.Checkpoint.FailedFileIds.Should().NotBeEmpty(); // Failed file recorded
            task.Checkpoint.FailedFileIds.Should().HaveCount(1); // Exactly one failed file
        }

        [Fact]
        public async Task ProcessTaskAsync_WithLargeFiles_ShouldSkipOversizedFiles()
        {
            // Arrange
            var repositoryId = Guid.NewGuid();
            var repository = new Repository
            {
                Id = repositoryId,
                Name = "Test Repository",
                Location = _tempDirectory
            };

            var task = new GraphScanTask
            {
                TaskId = "test-task",
                RepositoryId = repositoryId.ToString(),
                ScanId = "test-scan",
                Scope = ScanScope.Repository,
                TargetPath = _tempDirectory,
                Options = new GraphScanOptions
                {
                    MaxFileSizeBytes = 100, // Very small limit
                    BatchSize = 1,
                    IncludeTests = true
                }
            };

            // Create a large test file
            var largeFilePath = Path.Combine(_tempDirectory, "LargeFile.cs");
            var largeContent = new string('x', 200); // Exceeds the limit
            await File.WriteAllTextAsync(largeFilePath, largeContent);

            var largeFile = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = "LargeFile.cs",
                Path = largeFilePath,
                IsDirectory = false
            };

            var rootStructureWithLargeFile = new RepositoryFile
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Name = Path.GetFileName(_tempDirectory),
                Path = _tempDirectory,
                IsDirectory = true,
                Children = new List<RepositoryFile> { largeFile }
            };

            // Setup mocks
            _mockRepositoryService.Setup(x => x.GetRepositoryByIdAsync(repositoryId))
                .ReturnsAsync(repository);

            _mockRepositoryService.Setup(x => x.GetRepositoryStructureAsync(repositoryId))
                .ReturnsAsync(rootStructureWithLargeFile);

            _mockGraphStorage.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockGraphStorage.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object);

            _mockTransaction.Setup(x => x.CommitAsync())
                .Returns(Task.CompletedTask);

            // Act
            await _processor.ProcessTaskAsync(task, CancellationToken.None);

            // Assert
            task.Status.Should().Be(QueueTaskStatus.Completed);
            task.Checkpoint.ProcessedFiles.Should().Be(1); // File is processed but skipped

            // Verify analysis was not called (file was skipped)
            _mockRoslynService.Verify(x => x.AnalyzeFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
    }
}