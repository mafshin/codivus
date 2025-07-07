using System;
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
            config.Neo4j.Should().NotBeNull();
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
                Neo4j = new Neo4jSettings
                {
                    Uri = "bolt://test-host:7687",
                    MaxConnectionPoolSize = 20,
                    Username = "testuser",
                    Password = "testpass",
                    Database = "testdb",
                    EnableEncryption = true,
                    ConnectionTimeout = TimeSpan.FromSeconds(5),
                    TrustStrategy = "TrustSystemCaSignedCertificates"
                }
            };

            // Assert
            config.Enabled.Should().BeTrue();
            config.Neo4j.Uri.Should().Be("bolt://test-host:7687");
            config.Neo4j.MaxConnectionPoolSize.Should().Be(20);
            config.Neo4j.Username.Should().Be("testuser");
            config.Neo4j.Password.Should().Be("testpass");
            config.Neo4j.Database.Should().Be("testdb");
            config.Neo4j.EnableEncryption.Should().BeTrue();
            config.Neo4j.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(5));
            config.Neo4j.TrustStrategy.Should().Be("TrustSystemCaSignedCertificates");
        }

        [Fact]
        public void Neo4jSettings_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Act
            var settings = new Neo4jSettings();

            // Assert
            settings.Uri.Should().Be("bolt://localhost:7687");
            settings.Username.Should().Be("neo4j");
            settings.Password.Should().Be("pass12345678");
            settings.Database.Should().Be("neo4j");
            settings.MaxConnectionPoolSize.Should().Be(50);
            settings.EnableEncryption.Should().BeFalse();
            settings.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(30));
            settings.TrustStrategy.Should().Be("TrustAllCertificates");
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