using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Codivus.API.BackgroundServices;

namespace Codivus.API.Tests.BackgroundServices
{
    public class GraphMaintenanceWorkerSimpleTests
    {
        [Fact]
        public void GraphMaintenanceOptions_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var options = new GraphMaintenanceOptions();

            // Assert
            options.MaintenanceIntervalMinutes.Should().Be(60);
            options.StaleTaskTimeoutMinutes.Should().Be(120);
            options.RetryDelayMinutes.Should().Be(30);
            options.ArchiveCompletedTasksAfterDays.Should().Be(7);
            options.OptimizeIndices.Should().BeTrue();
            options.CleanupOrphanedNodes.Should().BeTrue();
            options.DefragmentStorage.Should().BeFalse();
        }

        [Fact]
        public void GraphMaintenanceOptions_SectionName_ShouldBeCorrect()
        {
            // Act & Assert
            GraphMaintenanceOptions.SectionName.Should().Be("GraphMaintenance");
        }

        [Fact]
        public void GraphMaintenanceOptions_WithCustomValues_ShouldSetCorrectly()
        {
            // Act
            var options = new GraphMaintenanceOptions
            {
                MaintenanceIntervalMinutes = 30,
                StaleTaskTimeoutMinutes = 60,
                RetryDelayMinutes = 15,
                ArchiveCompletedTasksAfterDays = 3,
                OptimizeIndices = false,
                CleanupOrphanedNodes = false,
                DefragmentStorage = true
            };

            // Assert
            options.MaintenanceIntervalMinutes.Should().Be(30);
            options.StaleTaskTimeoutMinutes.Should().Be(60);
            options.RetryDelayMinutes.Should().Be(15);
            options.ArchiveCompletedTasksAfterDays.Should().Be(3);
            options.OptimizeIndices.Should().BeFalse();
            options.CleanupOrphanedNodes.Should().BeFalse();
            options.DefragmentStorage.Should().BeTrue();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(120)]
        public void GraphMaintenanceOptions_MaintenanceInterval_ShouldAcceptValidValues(int intervalMinutes)
        {
            // Act
            var options = new GraphMaintenanceOptions { MaintenanceIntervalMinutes = intervalMinutes };

            // Assert
            options.MaintenanceIntervalMinutes.Should().Be(intervalMinutes);
        }

        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(120)]
        [InlineData(300)]
        public void GraphMaintenanceOptions_StaleTaskTimeout_ShouldAcceptValidValues(int timeoutMinutes)
        {
            // Act
            var options = new GraphMaintenanceOptions { StaleTaskTimeoutMinutes = timeoutMinutes };

            // Assert
            options.StaleTaskTimeoutMinutes.Should().Be(timeoutMinutes);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(15)]
        [InlineData(30)]
        [InlineData(60)]
        public void GraphMaintenanceOptions_RetryDelay_ShouldAcceptValidValues(int delayMinutes)
        {
            // Act
            var options = new GraphMaintenanceOptions { RetryDelayMinutes = delayMinutes };

            // Assert
            options.RetryDelayMinutes.Should().Be(delayMinutes);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(14)]
        [InlineData(30)]
        public void GraphMaintenanceOptions_ArchiveDays_ShouldAcceptValidValues(int days)
        {
            // Act
            var options = new GraphMaintenanceOptions { ArchiveCompletedTasksAfterDays = days };

            // Assert
            options.ArchiveCompletedTasksAfterDays.Should().Be(days);
        }

        [Fact]
        public void GraphMaintenanceOptions_BooleanFlags_ShouldToggleCorrectly()
        {
            // Arrange
            var options = new GraphMaintenanceOptions();

            // Act & Assert - Test initial values
            options.OptimizeIndices.Should().BeTrue();
            options.CleanupOrphanedNodes.Should().BeTrue();
            options.DefragmentStorage.Should().BeFalse();

            // Act & Assert - Test toggling
            options.OptimizeIndices = false;
            options.CleanupOrphanedNodes = false;
            options.DefragmentStorage = true;

            options.OptimizeIndices.Should().BeFalse();
            options.CleanupOrphanedNodes.Should().BeFalse();
            options.DefragmentStorage.Should().BeTrue();
        }
    }
}