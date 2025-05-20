using Xunit;
using Moq;
using Codivus.API.Controllers;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Codivus.API.Tests
{
    public class RepositoriesControllerTests
    {
        private readonly Mock<IRepositoryService> _mockRepoService;
        private readonly Mock<ILogger<RepositoriesController>> _mockLogger;
        private readonly RepositoriesController _controller;

        public RepositoriesControllerTests()
        {
            _mockRepoService = new Mock<IRepositoryService>();
            _mockLogger = new Mock<ILogger<RepositoriesController>>();
            _controller = new RepositoriesController(_mockRepoService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetRepositoryById_RepositoryExists_ReturnsOkObjectResultWithRepository()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var expectedRepository = new Repository { Id = repoId, Name = "Test Repo", Location = "path/to/repo" };
            _mockRepoService.Setup(service => service.GetRepositoryByIdAsync(repoId))
                            .ReturnsAsync(expectedRepository);

            // Act
            var result = await _controller.GetRepositoryById(repoId);

            // Assert
            var actionResult = Assert.IsType<OkObjectResult>(result);
            var model = Assert.IsAssignableFrom<Repository>(actionResult.Value);
            Assert.Equal(expectedRepository.Id, model.Id);
        }

        [Fact]
        public async Task GetRepositoryById_RepositoryDoesNotExist_ReturnsNotFoundResult()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            _mockRepoService.Setup(service => service.GetRepositoryByIdAsync(repoId))
                            .ReturnsAsync((Repository)null);

            // Act
            var result = await _controller.GetRepositoryById(repoId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateRepository_ValidRepository_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var newRepo = new Repository { Name = "New Repo", Location = "valid/path", Type = RepositoryType.Local };
            var createdRepo = new Repository { Id = Guid.NewGuid(), Name = newRepo.Name, Location = newRepo.Location, Type = newRepo.Type };
            
            _mockRepoService.Setup(service => service.ValidateRepositoryAsync(newRepo.Location, newRepo.Type))
                            .ReturnsAsync(true);
            _mockRepoService.Setup(service => service.AddRepositoryAsync(It.IsAny<Repository>()))
                            .ReturnsAsync(createdRepo);

            // Act
            var result = await _controller.CreateRepository(newRepo);

            // Assert
            var actionResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(RepositoriesController.GetRepositoryById), actionResult.ActionName);
            Assert.Equal(createdRepo.Id, actionResult.RouteValues["id"]);
            var model = Assert.IsAssignableFrom<Repository>(actionResult.Value);
            Assert.Equal(createdRepo.Id, model.Id);
        }

        [Fact]
        public async Task CreateRepository_NullRepository_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.CreateRepository(null);

            // Assert
            var actionResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Repository cannot be null", actionResult.Value);
        }
        
        [Fact]
        public async Task CreateRepository_EmptyLocation_ReturnsBadRequest()
        {
            // Arrange
            var newRepo = new Repository { Name = "Test", Location = "", Type = RepositoryType.Local };

            // Act
            var result = await _controller.CreateRepository(newRepo);

            // Assert
            var actionResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Repository location cannot be empty", actionResult.Value);
        }

        [Fact]
        public async Task CreateRepository_InvalidLocation_ReturnsBadRequest()
        {
            // Arrange
            var newRepo = new Repository { Name = "New Repo", Location = "invalid/path", Type = RepositoryType.Local };
            _mockRepoService.Setup(service => service.ValidateRepositoryAsync(newRepo.Location, newRepo.Type))
                            .ReturnsAsync(false);

            // Act
            var result = await _controller.CreateRepository(newRepo);

            // Assert
            var actionResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.StartsWith("Invalid repository at location:", actionResult.Value as string);
        }
        
        [Fact]
        public async Task DeleteRepository_RepositoryExists_ReturnsNoContentResult()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var existingRepo = new Repository { Id = repoId, Name = "Test Repo", Location = "path/to/repo" };
            _mockRepoService.Setup(service => service.GetRepositoryByIdAsync(repoId)).ReturnsAsync(existingRepo);
            _mockRepoService.Setup(service => service.DeleteRepositoryAsync(repoId)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteRepository(repoId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteRepository_RepositoryNotFound_ReturnsNotFoundResult()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            _mockRepoService.Setup(service => service.GetRepositoryByIdAsync(repoId)).ReturnsAsync((Repository)null);

            // Act
            var result = await _controller.DeleteRepository(repoId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteRepository_ServiceThrowsInvalidOperation_ReturnsConflictObjectResult()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var existingRepo = new Repository { Id = repoId, Name = "Test Repo", Location = "path/to/repo" };
            _mockRepoService.Setup(service => service.GetRepositoryByIdAsync(repoId)).ReturnsAsync(existingRepo);
            _mockRepoService.Setup(service => service.DeleteRepositoryAsync(repoId))
                            .ThrowsAsync(new InvalidOperationException("Cannot delete due to active scans."));

            // Act
            var result = await _controller.DeleteRepository(repoId);

            // Assert
            var actionResult = Assert.IsType<ConflictObjectResult>(result);
            // Optionally, check the structure of the ConflictObjectResult's value
            // For example: var conflictValue = actionResult.Value as { string error, string suggestedAction };
            // Assert.Equal("Cannot delete due to active scans.", conflictValue.error);
        }
    }
}
