using Xunit;
using FluentAssertions;
using Codivus.Core.Models;
using System;
using System.Linq;

namespace Codivus.API.Tests.Models
{
    public class GraphProcessingConfigurationTests
    {
        [Fact]
        public void GraphScanningOptions_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Act
            var options = new GraphScanningOptions();

            // Assert
            options.Enabled.Should().BeTrue();
            options.Processing.Should().NotBeNull();
            options.Analysis.Should().NotBeNull();
        }

        [Fact]
        public void GraphScanningOptions_SectionName_ShouldBeCorrect()
        {
            // Act & Assert
            GraphScanningOptions.SectionName.Should().Be("GraphScanning");
        }

        [Fact]
        public void ProcessingOptions_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Act
            var options = new ProcessingOptions();

            // Assert
            options.MaxConcurrentFiles.Should().Be(50);
            options.BatchSize.Should().Be(1000);
            options.TimeoutMinutes.Should().Be(30);
            options.RetryAttempts.Should().Be(3);
            options.WorkerCount.Should().Be(4);
        }

        [Fact]
        public void ProcessingOptions_WithCustomValues_ShouldSetCorrectly()
        {
            // Act
            var options = new ProcessingOptions
            {
                MaxConcurrentFiles = 100,
                BatchSize = 500,
                TimeoutMinutes = 60,
                RetryAttempts = 5,
                WorkerCount = 8
            };

            // Assert
            options.MaxConcurrentFiles.Should().Be(100);
            options.BatchSize.Should().Be(500);
            options.TimeoutMinutes.Should().Be(60);
            options.RetryAttempts.Should().Be(5);
            options.WorkerCount.Should().Be(8);
        }

        [Fact]
        public void AnalysisOptions_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Act
            var options = new AnalysisOptions();

            // Assert
            options.IncludeTests.Should().BeFalse();
            options.MaxFileSize.Should().Be(1048576); // 1MB
            options.SupportedExtensions.Should().ContainInOrder(".cs", ".vb");
            options.ExcludedPatterns.Should().NotBeNull();
            options.ExcludedPatterns.Should().HaveCount(7);
        }

        [Fact]
        public void AnalysisOptions_ExcludedPatterns_ShouldIncludeCommonPatterns()
        {
            // Act
            var options = new AnalysisOptions();

            // Assert
            var expectedPatterns = new[]
            {
                "**/bin/**",
                "**/obj/**",
                "**/.git/**",
                "**/packages/**",
                "**/*.Designer.cs",
                "**/*.g.cs",
                "**/*.g.i.cs"
            };

            foreach (var pattern in expectedPatterns)
            {
                options.ExcludedPatterns.Should().Contain(pattern, 
                    $"ExcludedPatterns should contain '{pattern}' by default");
            }
        }

        [Fact]
        public void AnalysisOptions_SupportedExtensions_ShouldIncludeDotNetExtensions()
        {
            // Act
            var options = new AnalysisOptions();

            // Assert
            options.SupportedExtensions.Should().Contain(".cs");
            options.SupportedExtensions.Should().Contain(".vb");
        }

        [Fact]
        public void AnalysisOptions_WithCustomValues_ShouldSetCorrectly()
        {
            // Act
            var options = new AnalysisOptions
            {
                IncludeTests = true,
                MaxFileSize = 2097152, // 2MB
                SupportedExtensions = { ".fs", ".ts" },
                ExcludedPatterns = { "**/temp/**", "**/cache/**" }
            };

            // Assert
            options.IncludeTests.Should().BeTrue();
            options.MaxFileSize.Should().Be(2097152);
            options.SupportedExtensions.Should().Contain(".fs");
            options.SupportedExtensions.Should().Contain(".ts");
            options.ExcludedPatterns.Should().Contain("**/temp/**");
            options.ExcludedPatterns.Should().Contain("**/cache/**");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        public void ProcessingOptions_MaxConcurrentFiles_ShouldAcceptValidValues(int maxFiles)
        {
            // Act
            var options = new ProcessingOptions { MaxConcurrentFiles = maxFiles };

            // Assert
            options.MaxConcurrentFiles.Should().Be(maxFiles);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        [InlineData(5000)]
        public void ProcessingOptions_BatchSize_ShouldAcceptValidValues(int batchSize)
        {
            // Act
            var options = new ProcessingOptions { BatchSize = batchSize };

            // Assert
            options.BatchSize.Should().Be(batchSize);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(15)]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(120)]
        public void ProcessingOptions_TimeoutMinutes_ShouldAcceptValidValues(int timeoutMinutes)
        {
            // Act
            var options = new ProcessingOptions { TimeoutMinutes = timeoutMinutes };

            // Assert
            options.TimeoutMinutes.Should().Be(timeoutMinutes);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(10)]
        public void ProcessingOptions_RetryAttempts_ShouldAcceptValidValues(int retryAttempts)
        {
            // Act
            var options = new ProcessingOptions { RetryAttempts = retryAttempts };

            // Assert
            options.RetryAttempts.Should().Be(retryAttempts);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        public void ProcessingOptions_WorkerCount_ShouldAcceptValidValues(int workerCount)
        {
            // Act
            var options = new ProcessingOptions { WorkerCount = workerCount };

            // Assert
            options.WorkerCount.Should().Be(workerCount);
        }

        [Theory]
        [InlineData(1024)]        // 1KB
        [InlineData(1048576)]     // 1MB
        [InlineData(5242880)]     // 5MB
        [InlineData(10485760)]    // 10MB
        public void AnalysisOptions_MaxFileSize_ShouldAcceptValidValues(int maxFileSize)
        {
            // Act
            var options = new AnalysisOptions { MaxFileSize = maxFileSize };

            // Assert
            options.MaxFileSize.Should().Be(maxFileSize);
        }

        [Fact]
        public void GraphScanningOptions_NestedConfiguration_ShouldWorkCorrectly()
        {
            // Act
            var options = new GraphScanningOptions
            {
                Enabled = false,
                Processing = new ProcessingOptions
                {
                    MaxConcurrentFiles = 25,
                    BatchSize = 200,
                    TimeoutMinutes = 45,
                    RetryAttempts = 2,
                    WorkerCount = 6
                },
                Analysis = new AnalysisOptions
                {
                    IncludeTests = true,
                    MaxFileSize = 512000
                }
            };
            
            // Clear defaults and set our own values to avoid list append behavior
            options.Analysis.SupportedExtensions.Clear();
            options.Analysis.SupportedExtensions.AddRange(new[] { ".cs", ".vb", ".fs" });
            
            options.Analysis.ExcludedPatterns.Clear();
            options.Analysis.ExcludedPatterns.Add("**/test/**");

            // Assert
            options.Enabled.Should().BeFalse();
            
            options.Processing.MaxConcurrentFiles.Should().Be(25);
            options.Processing.BatchSize.Should().Be(200);
            options.Processing.TimeoutMinutes.Should().Be(45);
            options.Processing.RetryAttempts.Should().Be(2);
            options.Processing.WorkerCount.Should().Be(6);
            
            options.Analysis.IncludeTests.Should().BeTrue();
            options.Analysis.MaxFileSize.Should().Be(512000);
            options.Analysis.SupportedExtensions.Should().HaveCount(3);
            options.Analysis.ExcludedPatterns.Should().Contain("**/test/**");
        }

        [Fact]
        public void AnalysisOptions_ExtensionFiltering_ShouldSupportCommonExtensions()
        {
            // Arrange
            var options = new AnalysisOptions();
            var testExtensions = new[] { ".cs", ".vb", ".fs", ".ts", ".js", ".cpp", ".h" };

            // Act & Assert
            foreach (var extension in testExtensions)
            {
                // This simulates how the extensions would be used for filtering
                var isSupported = options.SupportedExtensions.Contains(extension);
                
                if (extension == ".cs" || extension == ".vb")
                {
                    isSupported.Should().BeTrue($"Extension '{extension}' should be supported by default");
                }
                else
                {
                    // For non-default extensions, we can add them
                    options.SupportedExtensions.Add(extension);
                    options.SupportedExtensions.Should().Contain(extension);
                }
            }
        }

        [Fact]
        public void AnalysisOptions_PatternMatching_ShouldSupportGlobPatterns()
        {
            // Arrange
            var options = new AnalysisOptions();
            var testPaths = new[]
            {
                "src/bin/Debug/test.cs",
                "src/obj/Release/test.cs", 
                "src/.git/config",
                "src/packages/package.cs",
                "src/Form1.Designer.cs",
                "src/Resource.g.cs",
                "src/Assembly.g.i.cs",
                "src/ValidFile.cs"
            };

            // Act & Assert
            foreach (var path in testPaths)
            {
                var shouldBeExcluded = options.ExcludedPatterns.Any(pattern => 
                    path.Contains(pattern.Replace("**/", "").Replace("/**", "").Replace("*", "")));
                
                if (path.Contains("ValidFile.cs"))
                {
                    shouldBeExcluded.Should().BeFalse($"Path '{path}' should not be excluded");
                }
                else
                {
                    shouldBeExcluded.Should().BeTrue($"Path '{path}' should be excluded by patterns");
                }
            }
        }
    }
}