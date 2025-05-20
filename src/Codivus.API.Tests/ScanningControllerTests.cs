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
    public class ScanningControllerTests
    {
        private readonly Mock<IScanningService> _mockScanningService;
        private readonly Mock<IRepositoryService> _mockRepositoryService;
        private readonly Mock<ILogger<ScanningController>> _mockLogger;
        private readonly ScanningController _controller;

        public ScanningControllerTests()
        {
            _mockScanningService = new Mock<IScanningService>();
            _mockRepositoryService = new Mock<IRepositoryService>();
            _mockLogger = new Mock<ILogger<ScanningController>>();
            _controller = new ScanningController(
                _mockScanningService.Object,
                _mockRepositoryService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task GetScanById_ScanExists_ReturnsOkObjectResultWithScan()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            var expectedScan = new ScanProgress { Id = scanId, Status = ScanStatus.Completed, RepositoryId = Guid.NewGuid(), ConfigurationId = Guid.NewGuid() };
            _mockScanningService.Setup(service => service.GetScanProgressAsync(scanId))
                                .ReturnsAsync(expectedScan);

            // Act
            var result = await _controller.GetScanById(scanId);

            // Assert
            var actionResult = Assert.IsType<OkObjectResult>(result);
            var model = Assert.IsAssignableFrom<ScanProgress>(actionResult.Value);
            Assert.Equal(expectedScan.Id, model.Id);
        }

        [Fact]
        public async Task GetScanById_ScanDoesNotExist_ReturnsNotFoundResult()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            _mockScanningService.Setup(service => service.GetScanProgressAsync(scanId))
                                .ReturnsAsync((ScanProgress)null);

            // Act
            var result = await _controller.GetScanById(scanId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateScanConfiguration_ValidConfiguration_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var newConfig = new ScanConfiguration { Name = "My Config", RepositoryId = repoId };
            var createdConfig = new ScanConfiguration { Id = Guid.NewGuid(), Name = newConfig.Name, RepositoryId = repoId };
            
            _mockRepositoryService.Setup(service => service.GetRepositoryByIdAsync(repoId))
                                  .ReturnsAsync(new Repository { Id = repoId, Name = "Test Repo", Location = "some/path" });
            _mockScanningService.Setup(service => service.CreateScanConfigurationAsync(It.IsAny<ScanConfiguration>()))
                                .ReturnsAsync(createdConfig);

            // Act
            var result = await _controller.CreateScanConfiguration(newConfig);

            // Assert
            var actionResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(ScanningController.GetScanConfigurationById), actionResult.ActionName);
            Assert.Equal(createdConfig.Id, actionResult.RouteValues["configurationId"]);
            var model = Assert.IsAssignableFrom<ScanConfiguration>(actionResult.Value);
            Assert.Equal(createdConfig.Id, model.Id);
        }

        [Fact]
        public async Task CreateScanConfiguration_NullConfiguration_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.CreateScanConfiguration(null);

            // Assert
            var actionResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Scan configuration cannot be null", actionResult.Value);
        }

        [Fact]
        public async Task CreateScanConfiguration_RepositoryNotFound_ReturnsNotFoundResult()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var newConfig = new ScanConfiguration { Name = "My Config", RepositoryId = repoId };
            _mockRepositoryService.Setup(service => service.GetRepositoryByIdAsync(repoId))
                                  .ReturnsAsync((Repository)null); // Repository not found

            // Act
            var result = await _controller.CreateScanConfiguration(newConfig);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }
        
        [Fact]
        public async Task DeleteScan_ScanExistsAndNotActive_ReturnsNoContentResult()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            var existingScan = new ScanProgress { Id = scanId, Status = ScanStatus.Completed, RepositoryId = Guid.NewGuid(), ConfigurationId = Guid.NewGuid() };
            _mockScanningService.Setup(service => service.GetScanProgressAsync(scanId)).ReturnsAsync(existingScan);
            _mockScanningService.Setup(service => service.DeleteScanAsync(scanId)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteScan(scanId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteScan_ScanNotFound_ReturnsNotFoundResult()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            _mockScanningService.Setup(service => service.GetScanProgressAsync(scanId)).ReturnsAsync((ScanProgress)null);

            // Act
            var result = await _controller.DeleteScan(scanId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteScan_ScanIsActive_ReturnsBadRequestResult()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            // Test with InProgress, Initializing would be similar
            var activeScan = new ScanProgress { Id = scanId, Status = ScanStatus.InProgress, RepositoryId = Guid.NewGuid(), ConfigurationId = Guid.NewGuid() }; 
            _mockScanningService.Setup(service => service.GetScanProgressAsync(scanId)).ReturnsAsync(activeScan);
            // DeleteScanAsync in controller checks status before calling service's DeleteScanAsync for this specific case

            // Act
            var result = await _controller.DeleteScan(scanId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Cannot delete scan with status InProgress", badRequestResult.Value as string);
        }
    }
}
