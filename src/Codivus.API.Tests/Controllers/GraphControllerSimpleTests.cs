using System;
using System.Collections.Generic;
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
    public class GraphControllerSimpleTests
    {
        private readonly Mock<IGraphQueryService> _mockGraphQueryService;
        private readonly Mock<IGraphStorageService> _mockGraphStorageService;
        private readonly Mock<ILogger<GraphController>> _mockLogger;

        public GraphControllerSimpleTests()
        {
            _mockGraphQueryService = new Mock<IGraphQueryService>();
            _mockGraphStorageService = new Mock<IGraphStorageService>();
            _mockLogger = new Mock<ILogger<GraphController>>();
        }

        [Fact]
        public void GraphController_Constructor_ShouldAcceptAllDependencies()
        {
            // Arrange & Act
            var mockOrchestrator = new Mock<IGraphScanOrchestrator>();

            var controller = new GraphController(
                _mockGraphQueryService.Object,
                _mockGraphStorageService.Object,
                mockOrchestrator.Object,
                _mockLogger.Object);

            // Assert
            controller.Should().NotBeNull();
        }

        [Fact]
        public async Task GetNodes_ReturnsOkResult()
        {
            // Arrange
            var mockOrchestrator = new Mock<IGraphScanOrchestrator>();

            var controller = new GraphController(
                _mockGraphQueryService.Object,
                _mockGraphStorageService.Object,
                mockOrchestrator.Object,
                _mockLogger.Object);

            var repositoryId = Guid.NewGuid().ToString();
            var expectedNodes = new List<CodeNode>
            {
                new CodeNode { Id = "1", Name = "TestClass", NodeType = NodeType.Type }
            };

            _mockGraphQueryService
                .Setup(x => x.FindNodesByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<NodeType?>(),
                    It.IsAny<int>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(expectedNodes);

            // Act
            var result = await controller.GetNodes(repositoryId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ExecuteCustomQuery_WithValidQuery_ReturnsOkResult()
        {
            // Arrange
            var mockOrchestrator = new Mock<IGraphScanOrchestrator>();

            var controller = new GraphController(
                _mockGraphQueryService.Object,
                _mockGraphStorageService.Object,
                mockOrchestrator.Object,
                _mockLogger.Object);

            var request = new GraphQueryRequestDto
            {
                Query = "g.V().hasLabel('type')",
                Parameters = new Dictionary<string, object>()
            };

            var expectedResults = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "id", "1" }, { "name", "TestClass" } }
            };

            _mockGraphQueryService
                .Setup(x => x.ExecuteCustomQueryAsync(
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object>>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(expectedResults);

            // Act
            var result = await controller.ExecuteCustomQuery(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ExecuteCustomQuery_WithEmptyQuery_ReturnsBadRequest()
        {
            // Arrange
            var mockOrchestrator = new Mock<IGraphScanOrchestrator>();

            var controller = new GraphController(
                _mockGraphQueryService.Object,
                _mockGraphStorageService.Object,
                mockOrchestrator.Object,
                _mockLogger.Object);

            var request = new GraphQueryRequestDto { Query = "" };

            // Act
            var result = await controller.ExecuteCustomQuery(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}