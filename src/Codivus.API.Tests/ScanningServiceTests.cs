using Xunit;
using Moq;
using Codivus.API.Services;
using Codivus.API.Data;
using Codivus.Core.Models;
using Codivus.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Codivus.API.Tests
{
    public class ScanningServiceTests
    {
        private readonly Mock<ILogger<ScanningService>> _mockLogger;
        private readonly Mock<IRepositoryService> _mockRepositoryService;
        private readonly Mock<JsonDataStore> _mockDataStore;
        private readonly Mock<ILlmProvider> _mockLlmProvider;
        private readonly ScanningService _service;

        public ScanningServiceTests()
        {
            _mockLogger = new Mock<ILogger<ScanningService>>();
            _mockRepositoryService = new Mock<IRepositoryService>();
            
            // JsonDataStore constructor needs ILogger<JsonDataStore>
            var mockDataStoreLogger = new Mock<ILogger<JsonDataStore>>();
            _mockDataStore = new Mock<JsonDataStore>(mockDataStoreLogger.Object); 
            
            _mockLlmProvider = new Mock<ILlmProvider>();
            
            _service = new ScanningService(
                _mockLogger.Object,
                _mockRepositoryService.Object,
                _mockDataStore.Object,
                _mockLlmProvider.Object
            );
        }

        [Fact]
        public async Task GetScanProgressAsync_ScanFound_ReturnsScanProgress()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            var expectedScanProgress = new ScanProgress { Id = scanId, RepositoryId = Guid.NewGuid(), ConfigurationId = Guid.NewGuid(), Status = ScanStatus.InProgress };
            _mockDataStore.Setup(ds => ds.GetScanAsync(scanId)).ReturnsAsync(expectedScanProgress);

            // Act
            var result = await _service.GetScanProgressAsync(scanId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(scanId, result.Id);
            _mockDataStore.Verify(ds => ds.GetScanAsync(scanId), Times.Once);
        }

        [Fact]
        public async Task GetScanProgressAsync_ScanNotFound_ThrowsArgumentException()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            _mockDataStore.Setup(ds => ds.GetScanAsync(scanId)).ReturnsAsync((ScanProgress)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.GetScanProgressAsync(scanId));
            Assert.Contains($"Scan with ID {scanId} not found", exception.Message);
            _mockDataStore.Verify(ds => ds.GetScanAsync(scanId), Times.Once);
        }

        [Fact]
        public async Task CreateScanConfigurationAsync_ValidConfiguration_ReturnsAddedConfiguration()
        {
            // Arrange
            var repoId = Guid.NewGuid();
            var newConfig = new ScanConfiguration { Name = "Default Config", RepositoryId = repoId };
            // Capture the config passed to AddConfigurationAsync
            ScanConfiguration addedConfigCapture = null;
            _mockDataStore.Setup(ds => ds.AddConfigurationAsync(It.IsAny<ScanConfiguration>()))
                         .Callback<ScanConfiguration>(config => addedConfigCapture = config)
                         .ReturnsAsync((ScanConfiguration sc) => sc); // Return the config passed to it

            // Act
            var result = await _service.CreateScanConfigurationAsync(newConfig);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id); // Id should be set by service if empty
            Assert.True(result.CreatedAt > DateTime.UtcNow.AddMinutes(-1) && result.CreatedAt <= DateTime.UtcNow);
            Assert.True(result.UpdatedAt > DateTime.UtcNow.AddMinutes(-1) && result.UpdatedAt <= DateTime.UtcNow);
            // Assert.Equal(result.CreatedAt, result.UpdatedAt); // This can fail due to minor timing differences
            Assert.True((result.UpdatedAt - result.CreatedAt).TotalMilliseconds < 100, "UpdatedAt should be very close to CreatedAt on creation.");


            Assert.NotNull(addedConfigCapture);
            Assert.Equal(addedConfigCapture.Id, result.Id);
            Assert.Equal(addedConfigCapture.RepositoryId, repoId);
            _mockDataStore.Verify(ds => ds.AddConfigurationAsync(It.IsAny<ScanConfiguration>()), Times.Once);
        }
        
        [Fact]
        public async Task DeleteScanAsync_ScanExistsAndNotActive_ReturnsTrue()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            var scanProgress = new ScanProgress { Id = scanId, RepositoryId = Guid.NewGuid(), ConfigurationId = Guid.NewGuid(), Status = ScanStatus.Completed };
            _mockDataStore.Setup(ds => ds.GetScanAsync(scanId)).ReturnsAsync(scanProgress);
            _mockDataStore.Setup(ds => ds.DeleteScanWithIssuesAsync(scanId)).ReturnsAsync(true);

            // Act
            var result = await _service.DeleteScanAsync(scanId);

            // Assert
            Assert.True(result);
            _mockDataStore.Verify(ds => ds.GetScanAsync(scanId), Times.Once);
            _mockDataStore.Verify(ds => ds.DeleteScanWithIssuesAsync(scanId), Times.Once);
        }

        [Fact]
        public async Task DeleteScanAsync_ScanNotFound_ReturnsFalse()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            _mockDataStore.Setup(ds => ds.GetScanAsync(scanId)).ReturnsAsync((ScanProgress)null);

            // Act
            var result = await _service.DeleteScanAsync(scanId);

            // Assert
            Assert.False(result);
            _mockDataStore.Verify(ds => ds.GetScanAsync(scanId), Times.Once);
            _mockDataStore.Verify(ds => ds.DeleteScanWithIssuesAsync(scanId), Times.Never);
        }

        [Fact]
        public async Task DeleteScanAsync_ScanIsActive_Initializing_ReturnsFalse()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            var scanProgress = new ScanProgress { Id = scanId, RepositoryId = Guid.NewGuid(), ConfigurationId = Guid.NewGuid(), Status = ScanStatus.Initializing };
            _mockDataStore.Setup(ds => ds.GetScanAsync(scanId)).ReturnsAsync(scanProgress);

            // Act
            var result = await _service.DeleteScanAsync(scanId);

            // Assert
            Assert.False(result);
            _mockDataStore.Verify(ds => ds.GetScanAsync(scanId), Times.Once);
            _mockDataStore.Verify(ds => ds.DeleteScanWithIssuesAsync(scanId), Times.Never);
        }

        [Fact]
        public async Task DeleteScanAsync_ScanIsActive_InProgress_ReturnsFalse()
        {
            // Arrange
            var scanId = Guid.NewGuid();
            var scanProgress = new ScanProgress { Id = scanId, RepositoryId = Guid.NewGuid(), ConfigurationId = Guid.NewGuid(), Status = ScanStatus.InProgress };
            _mockDataStore.Setup(ds => ds.GetScanAsync(scanId)).ReturnsAsync(scanProgress);

            // Act
            var result = await _service.DeleteScanAsync(scanId);

            // Assert
            Assert.False(result);
            _mockDataStore.Verify(ds => ds.GetScanAsync(scanId), Times.Once);
            _mockDataStore.Verify(ds => ds.DeleteScanWithIssuesAsync(scanId), Times.Never);
        }
    }
}
