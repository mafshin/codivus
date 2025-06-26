using Xunit;
using FluentAssertions;
using Codivus.Graph.Configuration;

namespace Codivus.Graph.Tests.Configuration
{
    public class GraphConfigurationTests
    {
        [Fact]
        public void GraphConfiguration_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Act
            var config = new GraphConfiguration();

            // Assert
            config.Should().NotBeNull();
            config.JanusGraph.Should().NotBeNull();
            config.Processing.Should().NotBeNull();
            config.Analysis.Should().NotBeNull();
        }

        [Fact]
        public void GraphConfiguration_WithValues_ShouldSetCorrectly()
        {
            // Arrange & Act
            var config = new GraphConfiguration
            {
                Enabled = true,
                JanusGraph = new JanusGraphSettings
                {
                    Host = "test-host",
                    Port = 9999,
                    ConnectionPoolSize = 20,
                    Username = "testuser",
                    Password = "testpass",
                    EnableSsl = true,
                    ConnectionTimeout = 5000
                }
            };

            // Assert
            config.Enabled.Should().BeTrue();
            config.JanusGraph.Host.Should().Be("test-host");
            config.JanusGraph.Port.Should().Be(9999);
            config.JanusGraph.ConnectionPoolSize.Should().Be(20);
            config.JanusGraph.Username.Should().Be("testuser");
            config.JanusGraph.Password.Should().Be("testpass");
            config.JanusGraph.EnableSsl.Should().BeTrue();
            config.JanusGraph.ConnectionTimeout.Should().Be(5000);
        }

        [Fact]
        public void JanusGraphSettings_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Act
            var settings = new JanusGraphSettings();

            // Assert
            settings.Host.Should().Be("localhost");
            settings.Port.Should().Be(8182);
            settings.ConnectionPoolSize.Should().Be(10);
            settings.Username.Should().BeEmpty();
            settings.Password.Should().BeEmpty();
            settings.EnableSsl.Should().BeFalse();
            settings.ConnectionTimeout.Should().Be(30000);
        }

        [Fact]
        public void ProcessingSettings_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Act
            var settings = new ProcessingSettings();

            // Assert
            settings.MaxConcurrentFiles.Should().Be(50);
            settings.BatchSize.Should().Be(1000);
            settings.TimeoutMinutes.Should().Be(30);
            settings.RetryAttempts.Should().Be(3);
        }

        [Fact]
        public void AnalysisSettings_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Act
            var settings = new AnalysisSettings();

            // Assert
            settings.IncludeTests.Should().BeFalse();
            settings.MaxFileSize.Should().Be(1048576); // 1MB
            settings.SupportedExtensions.Should().Contain(".cs");
            settings.SupportedExtensions.Should().Contain(".vb");
            settings.AnalyzeGeneratedCode.Should().BeFalse();
        }
    }
}