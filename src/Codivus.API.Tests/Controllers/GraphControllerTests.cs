using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codivus.API.Controllers;
using Codivus.API.Interfaces;
using Codivus.API.Services;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Codivus.API.Tests.Controllers
{
    public class GraphControllerTests
    {
        private readonly Mock<IGraphQueryService> _mockGraphQueryService;
        private readonly Mock<IGraphStorageService> _mockGraphStorageService;
        private readonly Mock<IGraphScanOrchestrator> _mockGraphScanOrchestrator;
        private readonly Mock<ILogger<GraphController>> _mockLogger;
        private readonly GraphController _controller;

        public GraphControllerTests()
        {
            _mockGraphQueryService = new Mock<IGraphQueryService>();
            _mockGraphStorageService = new Mock<IGraphStorageService>();
            _mockLogger = new Mock<ILogger<GraphController>>();
            
            _mockGraphScanOrchestrator = new Mock<IGraphScanOrchestrator>();

            _controller = new GraphController(
                _mockGraphQueryService.Object,
                _mockGraphStorageService.Object,
                _mockGraphScanOrchestrator.Object,
                _mockLogger.Object);
        }

        #region Graph Scanning Endpoints Tests

        [Fact]
        public async Task StartGraphScan_ValidRequest_ReturnsOkWithScanId()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var expectedScanId = Guid.NewGuid().ToString();
            var request = new GraphScanRequestDto
            {
                Mode = ScanMode.Full,
                Processing = new ProcessingConfiguration(),
                Analysis = new AnalysisConfiguration(),
                Relationships = new RelationshipConfiguration(),
                Metrics = new MetricsConfiguration()
            };

            _mockGraphScanOrchestrator
                .Setup(x => x.StartGraphScanAsync(
                    It.IsAny<string>(),
                    It.IsAny<Core.Models.GraphScanConfiguration>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedScanId);

            // Act
            var result = await _controller.StartGraphScan(repositoryId, request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(new 
            { 
                scanId = expectedScanId, 
                message = "Graph scan started successfully" 
            });
        }

        [Fact]
        public async Task StartGraphScan_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var request = new GraphScanRequestDto { Mode = ScanMode.Full };

            _mockGraphScanOrchestrator
                .Setup(x => x.StartGraphScanAsync(
                    It.IsAny<string>(),
                    It.IsAny<Core.Models.GraphScanConfiguration>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.StartGraphScan(repositoryId, request);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task GetGraphScanStatus_ExistingScan_ReturnsOkWithProgress()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();
            var expectedProgress = new GraphScanProgress
            {
                ScanId = scanId,
                RepositoryId = Guid.NewGuid().ToString(),
                Status = ScanStatus.InProgress,
                TotalTasks = 100,
                CompletedTasks = 50
            };

            _mockGraphScanOrchestrator
                .Setup(x => x.GetScanProgressAsync(scanId))
                .ReturnsAsync(expectedProgress);

            // Act
            var result = await _controller.GetGraphScanStatus(scanId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(expectedProgress);
        }

        [Fact]
        public async Task GetGraphScanStatus_NonExistentScan_ReturnsNotFound()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();

            _mockGraphScanOrchestrator
                .Setup(x => x.GetScanProgressAsync(scanId))
                .ReturnsAsync((GraphScanProgress)null);

            // Act
            var result = await _controller.GetGraphScanStatus(scanId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task PauseGraphScan_SuccessfulPause_ReturnsOk()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();

            _mockGraphScanOrchestrator
                .Setup(x => x.PauseScanAsync(scanId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.PauseGraphScan(scanId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(new { message = "Scan paused successfully" });
        }

        [Fact]
        public async Task PauseGraphScan_UnsuccessfulPause_ReturnsNotFound()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();

            _mockGraphScanOrchestrator
                .Setup(x => x.PauseScanAsync(scanId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.PauseGraphScan(scanId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task ResumeGraphScan_SuccessfulResume_ReturnsOk()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();

            _mockGraphScanOrchestrator
                .Setup(x => x.ResumeScanAsync(scanId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.ResumeGraphScan(scanId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(new { message = "Scan resumed successfully" });
        }

        [Fact]
        public async Task CancelGraphScan_SuccessfulCancel_ReturnsOk()
        {
            // Arrange
            var scanId = Guid.NewGuid().ToString();

            _mockGraphScanOrchestrator
                .Setup(x => x.CancelScanAsync(scanId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.CancelGraphScan(scanId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(new { message = "Scan cancelled successfully" });
        }

        #endregion

        #region Query Endpoints Tests

        [Fact]
        public async Task GetNodes_ValidRequest_ReturnsNodes()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var expectedNodes = new List<CodeNode>
            {
                new CodeNode { Id = "1", Name = "TestClass", NodeType = NodeType.Type },
                new CodeNode { Id = "2", Name = "TestMethod", NodeType = NodeType.Method }
            };

            _mockGraphQueryService
                .Setup(x => x.FindNodesByNameAsync(
                    repositoryId,
                    "*",
                    It.IsAny<NodeType?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedNodes);

            // Act
            var result = await _controller.GetNodes(repositoryId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(expectedNodes);
        }

        [Fact]
        public async Task GetNode_ExistingNode_ReturnsNode()
        {
            // Arrange
            var nodeId = Guid.NewGuid().ToString();
            var expectedNode = new CodeNode 
            { 
                Id = nodeId, 
                Name = "TestClass", 
                NodeType = NodeType.Type 
            };

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(nodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedNode);

            // Act
            var result = await _controller.GetNode(nodeId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(expectedNode);
        }

        [Fact]
        public async Task GetNode_NonExistentNode_ReturnsNotFound()
        {
            // Arrange
            var nodeId = Guid.NewGuid().ToString();

            _mockGraphStorageService
                .Setup(x => x.GetNodeAsync(nodeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CodeNode)null);

            // Act
            var result = await _controller.GetNode(nodeId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task ExecuteCustomQuery_ValidQuery_ReturnsResults()
        {
            // Arrange
            var request = new GraphQueryRequestDto
            {
                Query = "g.V().hasLabel('type').limit(10)",
                Parameters = new Dictionary<string, object> { { "limit", 10 } }
            };

            var expectedResults = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "id", "1" }, { "name", "TestClass" } },
                new Dictionary<string, object> { { "id", "2" }, { "name", "TestMethod" } }
            };

            _mockGraphQueryService
                .Setup(x => x.ExecuteCustomQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults);

            // Act
            var result = await _controller.ExecuteCustomQuery(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(expectedResults);
        }

        [Fact]
        public async Task ExecuteCustomQuery_EmptyQuery_ReturnsBadRequest()
        {
            // Arrange
            var request = new GraphQueryRequestDto { Query = "" };

            // Act
            var result = await _controller.ExecuteCustomQuery(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetCallHierarchy_ValidRequest_ReturnsHierarchy()
        {
            // Arrange
            var nodeId = Guid.NewGuid().ToString();
            var expectedHierarchy = new CallHierarchy
            {
                RootMethod = new CodeNode { Id = nodeId, Name = "TestMethod" },
                Nodes = new List<CallHierarchyNode>
                {
                    new CallHierarchyNode 
                    { 
                        Method = new CodeNode { Id = "2", Name = "CalledMethod" },
                        ParentId = nodeId,
                        Depth = 1
                    }
                }
            };

            _mockGraphQueryService
                .Setup(x => x.GetCallHierarchyAsync(
                    nodeId,
                    It.IsAny<CallHierarchyDirection>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedHierarchy);

            // Act
            var result = await _controller.GetCallHierarchy(nodeId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().BeEquivalentTo(expectedHierarchy);
        }

        [Fact]
        public async Task GetVisualizationData_HierarchyType_ReturnsVisualizationData()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var namespaceNodes = new List<CodeNode>
            {
                new CodeNode { Id = "ns1", Name = "Namespace1", NodeType = NodeType.Namespace }
            };
            var childNodes = new List<CodeNode>
            {
                new CodeNode { Id = "type1", Name = "Type1", NodeType = NodeType.Type }
            };

            _mockGraphQueryService
                .Setup(x => x.FindNodesByNameAsync(
                    repositoryId,
                    "*",
                    It.IsAny<NodeType?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(namespaceNodes);

            _mockGraphQueryService
                .Setup(x => x.GetDependentsAsync(
                    "ns1",
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(childNodes);

            // Act
            var result = await _controller.GetVisualizationData(repositoryId, "hierarchy");

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Value.Should().NotBeNull();
        }

        #endregion
    }
}