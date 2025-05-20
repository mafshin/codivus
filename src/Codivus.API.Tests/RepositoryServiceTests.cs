using Xunit;
using Moq;
using Codivus.API.Services;
using Codivus.API.Data;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions; 
using System.IO.Abstractions.TestingHelpers; 
using System;
using System.Threading.Tasks;
using System.Collections.Generic; 
using System.Linq; // Added for Linq assertions

namespace Codivus.API.Tests
{
    public class RepositoryServiceTests
    {
        private readonly Mock<ILogger<RepositoryService>> _mockLogger;
        private readonly Mock<JsonDataStore> _mockDataStore; 
        private readonly MockFileSystem _mockFileSystem; // Reverted to MockFileSystem
        private readonly RepositoryService _service;

        public RepositoryServiceTests()
        {
            _mockLogger = new Mock<ILogger<RepositoryService>>();
            var mockDataStoreLogger = new Mock<ILogger<JsonDataStore>>();
            _mockDataStore = new Mock<JsonDataStore>(mockDataStoreLogger.Object);

            _mockFileSystem = new MockFileSystem(); 
            _service = new RepositoryService(_mockLogger.Object, _mockFileSystem, _mockDataStore.Object);
        }

        [Fact]
        public async Task GetRepositoryByIdAsync_RepositoryFound_ReturnsRepository()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var expectedRepository = new Repository { Id = repoId, Name = "Test Repo", Location = "/test/repo" };
            _mockDataStore.Setup(ds => ds.GetRepositoryAsync(repoId)).ReturnsAsync(expectedRepository);

            // Act
            var result = await _service.GetRepositoryByIdAsync(repoId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(repoId, result.Id);
            _mockDataStore.Verify(ds => ds.GetRepositoryAsync(repoId), Times.Once);
        }

        [Fact]
        public async Task GetRepositoryByIdAsync_RepositoryNotFound_ReturnsNull()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            _mockDataStore.Setup(ds => ds.GetRepositoryAsync(repoId)).ReturnsAsync((Repository)null);

            // Act
            var result = await _service.GetRepositoryByIdAsync(repoId);

            // Assert
            Assert.Null(result);
            _mockDataStore.Verify(ds => ds.GetRepositoryAsync(repoId), Times.Once);
        }

        [Fact]
        public async Task AddRepositoryAsync_ValidRepository_ReturnsAddedRepositoryWithIdAndDate()
        {
            // Arrange
            var newRepo = new Repository { Name = "New Repo", Location = "/new/repo" }; // Id will be set by service
            // Capture the repository passed to AddRepositoryAsync to verify Id and AddedAt
            Repository addedRepoCapture = null;
            _mockDataStore.Setup(ds => ds.AddRepositoryAsync(It.IsAny<Repository>()))
                         .Callback<Repository>(repo => addedRepoCapture = repo)
                         .ReturnsAsync((Repository r) => r); // Return the repo passed to it

            // Act
            var result = await _service.AddRepositoryAsync(newRepo);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.True(result.AddedAt > DateTime.UtcNow.AddMinutes(-1) && result.AddedAt <= DateTime.UtcNow); // Check if AddedAt is recent
            Assert.NotNull(addedRepoCapture);
            Assert.Equal(addedRepoCapture.Id, result.Id); // Ensure the Id was set before calling datastore
            Assert.Equal(addedRepoCapture.AddedAt, result.AddedAt);
            _mockDataStore.Verify(ds => ds.AddRepositoryAsync(It.IsAny<Repository>()), Times.Once);
        }
        
        // Add more tests based on the plan...

        [Fact]
        public async Task DeleteRepositoryAsync_RepositoryExistsAndNoActiveScans_ReturnsTrue()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var repository = new Repository { Id = repoId, Name = "Test Repo", Location = "/test/repo" };
            _mockDataStore.Setup(ds => ds.GetRepositoryAsync(repoId)).ReturnsAsync(repository);
            _mockDataStore.Setup(ds => ds.HasActiveScansAsync(repoId)).ReturnsAsync(false);
            _mockDataStore.Setup(ds => ds.DeleteRepositoryAsync(repoId)).ReturnsAsync(true);
            // Mock other count methods if their logs are critical, otherwise not strictly needed for this test's logic
            _mockDataStore.Setup(ds => ds.GetScanCountByRepositoryAsync(repoId)).ReturnsAsync(0);
            _mockDataStore.Setup(ds => ds.GetIssueCountByRepositoryAsync(repoId)).ReturnsAsync(0);
            _mockDataStore.Setup(ds => ds.GetConfigurationCountByRepositoryAsync(repoId)).ReturnsAsync(0);


            // Act
            var result = await _service.DeleteRepositoryAsync(repoId);

            // Assert
            Assert.True(result);
            _mockDataStore.Verify(ds => ds.GetRepositoryAsync(repoId), Times.Once);
            _mockDataStore.Verify(ds => ds.HasActiveScansAsync(repoId), Times.Once);
            _mockDataStore.Verify(ds => ds.DeleteRepositoryAsync(repoId), Times.Once);
        }

        [Fact]
        public async Task DeleteRepositoryAsync_RepositoryNotFound_ReturnsFalse()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            _mockDataStore.Setup(ds => ds.GetRepositoryAsync(repoId)).ReturnsAsync((Repository)null);

            // Act
            var result = await _service.DeleteRepositoryAsync(repoId);

            // Assert
            Assert.False(result);
            _mockDataStore.Verify(ds => ds.GetRepositoryAsync(repoId), Times.Once);
            _mockDataStore.Verify(ds => ds.HasActiveScansAsync(repoId), Times.Never); // Should not be called if repo not found
            _mockDataStore.Verify(ds => ds.DeleteRepositoryAsync(repoId), Times.Never); // Should not be called
        }

        [Fact]
        public async Task DeleteRepositoryAsync_RepositoryHasActiveScans_ThrowsInvalidOperationException()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var repository = new Repository { Id = repoId, Name = "Test Repo With Scans", Location = "/test/scans" };
            _mockDataStore.Setup(ds => ds.GetRepositoryAsync(repoId)).ReturnsAsync(repository);
            _mockDataStore.Setup(ds => ds.HasActiveScansAsync(repoId)).ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteRepositoryAsync(repoId));
            Assert.Contains("Cannot delete repository", exception.Message); // Check for part of the expected message
            _mockDataStore.Verify(ds => ds.GetRepositoryAsync(repoId), Times.Once);
            _mockDataStore.Verify(ds => ds.HasActiveScansAsync(repoId), Times.Once);
            _mockDataStore.Verify(ds => ds.DeleteRepositoryAsync(repoId), Times.Never); // Delete should not be called
        }

        [Fact]
        public async Task GetRepositoryStructureAsync_LocalRepository_ReturnsStructureSkippingGitAndNodeModules()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var repoLocation = _mockFileSystem.Path.Combine("C:", "testrepo"); // MockFileSystem uses platform-specific separators
            var expectedRepository = new Repository { Id = repoId, Name = "Test Repo", Location = repoLocation, Type = RepositoryType.Local };
            _mockDataStore.Setup(ds => ds.GetRepositoryAsync(repoId)).ReturnsAsync(expectedRepository);

            // Create mock file system structure
            _mockFileSystem.AddDirectory(repoLocation);
            _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(repoLocation, "src"));
            _mockFileSystem.AddFile(_mockFileSystem.Path.Combine(repoLocation, "src", "file1.cs"), new MockFileData("content"));
            _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(repoLocation, ".git")); // Should be skipped
            _mockFileSystem.AddFile(_mockFileSystem.Path.Combine(repoLocation, ".git", "config"), new MockFileData("git stuff"));
            _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(repoLocation, "node_modules")); // Should be skipped
            _mockFileSystem.AddFile(_mockFileSystem.Path.Combine(repoLocation, "node_modules", "somepackage.js"), new MockFileData("js stuff"));
            _mockFileSystem.AddDirectory(_mockFileSystem.Path.Combine(repoLocation, "another_dir"));
            _mockFileSystem.AddFile(_mockFileSystem.Path.Combine(repoLocation, "another_dir", "file2.txt"), new MockFileData("text content"));


            // Act
            var result = await _service.GetRepositoryStructureAsync(repoId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedRepository.Name, result.Name);
            Assert.True(result.IsDirectory);
            Assert.NotNull(result.Children);
            // Should have 'src' and 'another_dir', but not '.git' or 'node_modules' at the root of children
            Assert.Equal(2, result.Children.Count(c => c.IsDirectory));
            Assert.Contains(result.Children, c => c.Name == "src" && c.IsDirectory);
            Assert.Contains(result.Children, c => c.Name == "another_dir" && c.IsDirectory);
            Assert.DoesNotContain(result.Children, c => c.Name == ".git");
            Assert.DoesNotContain(result.Children, c => c.Name == "node_modules");

            var srcDir = result.Children.FirstOrDefault(c => c.Name == "src");
            Assert.NotNull(srcDir);
            Assert.NotNull(srcDir.Children);
            Assert.Single(srcDir.Children); // file1.cs
            Assert.Contains(srcDir.Children, f => f.Name == "file1.cs" && !f.IsDirectory);
        }

        [Fact]
        public async Task GetRepositoryStructureAsync_RepositoryNotFound_ThrowsArgumentException()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            _mockDataStore.Setup(ds => ds.GetRepositoryAsync(repoId)).ReturnsAsync((Repository)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetRepositoryStructureAsync(repoId));
        }
        
        [Fact]
        public async Task GetRepositoryStructureAsync_NonLocalRepository_ReturnsRootOnly()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var repoLocation = "https://github.com/test/repo.git";
            var expectedRepository = new Repository { Id = repoId, Name = "GitHub Repo", Location = repoLocation, Type = RepositoryType.GitHub };
            _mockDataStore.Setup(ds => ds.GetRepositoryAsync(repoId)).ReturnsAsync(expectedRepository);

            // Act
            var result = await _service.GetRepositoryStructureAsync(repoId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedRepository.Name, result.Name);
            Assert.True(result.IsDirectory);
            Assert.NotNull(result.Children); // Children list should be initialized
            Assert.Empty(result.Children); // No scanning for non-local
        }

        [Fact]
        public async Task ValidateRepositoryAsync_LocalRepository_PathExists_ReturnsTrue()
        {
            // Arrange
            var repoLocation = _mockFileSystem.Path.Combine("C:", "validlocalrepo");
            _mockFileSystem.AddDirectory(repoLocation);
            // Add a file to allow GetFiles to succeed for read access check
            _mockFileSystem.AddFile(_mockFileSystem.Path.Combine(repoLocation, "somefile.txt"), new MockFileData("test"));


            // Act
            var result = await _service.ValidateRepositoryAsync(repoLocation, RepositoryType.Local);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateRepositoryAsync_LocalRepository_PathDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var repoLocation = _mockFileSystem.Path.Combine("C:", "invalidlocalrepo");
            // Ensure directory does not exist by not adding it to mockFileSystem

            // Act
            var result = await _service.ValidateRepositoryAsync(repoLocation, RepositoryType.Local);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateRepositoryAsync_GitHubRepository_ValidUrl_ReturnsTrue()
        {
            // Arrange
            var repoLocation = "https://github.com/valid/repo.git";

            // Act
            var result = await _service.ValidateRepositoryAsync(repoLocation, RepositoryType.GitHub);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateRepositoryAsync_GitHubRepository_InvalidUrl_ReturnsFalse()
        {
            // Arrange
            var repoLocation = "ftp://invalid/repo.git";

            // Act
            var result = await _service.ValidateRepositoryAsync(repoLocation, RepositoryType.GitHub);

            // Assert
            Assert.False(result);
        }
    }
}
