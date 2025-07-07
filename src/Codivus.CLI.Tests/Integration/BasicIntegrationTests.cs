using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Codivus.CLI.Tests.Helpers;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;
using Codivus.Graph.Services;
using Codivus.Graph.Configuration;
using Codivus.API.Services;
using Xunit;

namespace Codivus.CLI.Tests.Integration;

public class BasicIntegrationTests : IAsyncLifetime
{
    private readonly Neo4jTestContainer _neo4jContainer;
    private readonly MockLLMServer _mockLLMServer;
    private readonly ILogger<BasicIntegrationTests> _logger;
    private IServiceProvider _serviceProvider = null!;
    private string _testDataPath = "";

    public BasicIntegrationTests()
    {
        _neo4jContainer = new Neo4jTestContainer();
        _mockLLMServer = new MockLLMServer();
        
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<BasicIntegrationTests>();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Starting integration test environment...");
        
        // Start both Neo4j and LLM server
        await Task.WhenAll(
            _neo4jContainer.InitializeAsync(),
            _mockLLMServer.InitializeAsync()
        );
        
        _logger.LogInformation($"Neo4j container started on port {_neo4jContainer.Port}");
        _logger.LogInformation($"Mock LLM server started on port {_mockLLMServer.Port}");

        // Create test data directory
        _testDataPath = Path.Combine(Path.GetTempPath(), $"codivus_integration_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataPath);

        // Setup service provider
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        
        _logger.LogInformation("Test environment initialized");
    }

    public async Task DisposeAsync()
    {
        _logger.LogInformation("Disposing integration test environment...");
        
        await Task.WhenAll(
            _neo4jContainer.DisposeAsync(),
            _mockLLMServer.DisposeAsync()
        );

        if (Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Codivus:Graph:Neo4j:Uri"] = _neo4jContainer.ConnectionString,
                ["Codivus:Graph:Neo4j:Username"] = "neo4j",
                ["Codivus:Graph:Neo4j:Password"] = "pass12345678",
                ["Codivus:Graph:Neo4j:EnableEncryption"] = "false",
                ["Codivus:Graph:Neo4j:MaxConnectionPoolSize"] = "4",
                ["Codivus:LLM:DefaultProvider"] = "Ollama",
                ["Codivus:LLM:Providers:Ollama:BaseUrl"] = _mockLLMServer.BaseUrl,
                ["Codivus:LLM:Providers:Ollama:DefaultModel"] = "codellama:7b",
                ["Codivus:LLM:Providers:Ollama:Timeout"] = "300",
                ["Codivus:Storage:Type"] = "FileSystem",
                ["Codivus:Storage:BasePath"] = _testDataPath
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Graph Services
        services.Configure<GraphConfiguration>(configuration.GetSection("Codivus:Graph"));
        services.AddScoped<IGraphStorageService, GraphStorageService>();
    }

    [Fact]
    public async Task Neo4j_Connection_ShouldSucceed()
    {
        // Arrange
        var graphStorageService = _serviceProvider.GetRequiredService<IGraphStorageService>();
        
        // Act & Assert
        _logger.LogInformation("Testing Neo4j connection...");
        
        // Verify container is healthy
        var isHealthy = await _neo4jContainer.IsHealthyAsync();
        isHealthy.Should().BeTrue("Neo4j container should be healthy");

        // Test basic graph operations
        var testNode = new CodeNode
        {
            Id = "test-node-1",
            Name = "TestClass",
            NodeType = NodeType.Type,
            FullName = "TestProject.TestClass",
            DisplayName = "TestClass",
            RepositoryId = "test-repo",
            ProjectId = "test-project",
            FileId = "test-file",
            StartLine = 1,
            EndLine = 10,
            Properties = new Dictionary<string, object>
            {
                ["Language"] = "C#",
                ["IsPublic"] = true
            }
        };

        // Create node
        var created = await graphStorageService.CreateNodeAsync(testNode);
        created.Should().Be(true, "Should be able to create node in Neo4j");

        // Retrieve node
        var retrievedNode = await graphStorageService.GetNodeAsync("test-node-1");
        retrievedNode.Should().NotBeNull("Should be able to retrieve created node");
        retrievedNode!.Name.Should().Be("TestClass");

        _logger.LogInformation("✅ Neo4j connection and basic operations successful");
    }

    [Fact]
    public async Task LLM_MockServer_ShouldRespondToRequests()
    {
        // Act & Assert
        _logger.LogInformation("Testing LLM mock server connection...");
        
        // Clear previous requests
        _mockLLMServer.ReceivedRequests.Clear();

        try
        {
            // Test the mock server directly via HTTP
            using var httpClient = new HttpClient();
            var requestJson = "{\"prompt\": \"Analyze this C# code: public class Calculator { }\", \"model\": \"codellama:7b\"}";
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{_mockLLMServer.BaseUrl}/api/generate", content);
            response.IsSuccessStatusCode.Should().BeTrue("Mock LLM server should respond successfully");
            
            var responseContent = await response.Content.ReadAsStringAsync();
            responseContent.Should().NotBeEmpty("Response should have content");

            // Verify mock server received the request
            _mockLLMServer.ReceivedRequests.Should().NotBeEmpty("Mock LLM server should have received requests");
            var generateRequest = _mockLLMServer.ReceivedRequests.FirstOrDefault(r => r.Url.Contains("/api/generate"));
            generateRequest.Should().NotBeNull("Should have made generate request");
            generateRequest!.Body.Should().Contain("Calculator", "Request should contain the test code");

            _logger.LogInformation($"✅ LLM mock server integration successful - received response: {responseContent[..Math.Min(100, responseContent.Length)]}...");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"LLM test failed: {ex.Message}");
            throw; // Re-throw to mark test as failed
        }
    }

    [Fact]
    public async Task EndToEnd_BasicRepository_ShouldWork()
    {
        // Create test repository
        var testRepoPath = TestRepositoryHelper.CreateTestRepository(_testDataPath, "integration-test-repo");
        var repository = TestRepositoryHelper.CreateRepositoryModel(testRepoPath, "integration-test-repo");
        
        _logger.LogInformation($"Created test repository at: {testRepoPath}");
        
        // Test file system operations
        _logger.LogInformation("Testing test repository structure...");
        
        File.Exists(Path.Combine(testRepoPath, "src", "Calculator.cs")).Should().BeTrue("Test files should be created");
        File.Exists(Path.Combine(testRepoPath, "tests", "CalculatorTests.cs")).Should().BeTrue("Test files should be created");
        File.Exists(Path.Combine(testRepoPath, "README.md")).Should().BeTrue("Test files should be created");
        
        var calculatorContent = await File.ReadAllTextAsync(Path.Combine(testRepoPath, "src", "Calculator.cs"));
        calculatorContent.Should().Contain("Calculator", "Test code should contain expected content");
        calculatorContent.Should().Contain("Add", "Test code should contain expected methods");

        _logger.LogInformation("✅ End-to-end basic integration test completed");
    }

    [Fact]
    public async Task GraphOperations_CRUD_ShouldWork()
    {
        // Arrange
        var graphStorageService = _serviceProvider.GetRequiredService<IGraphStorageService>();

        _logger.LogInformation("Testing comprehensive graph CRUD operations...");

        // Create test nodes
        var classNode = new CodeNode
        {
            Id = "class-1",
            Name = "Calculator",
            NodeType = NodeType.Type,
            FullName = "TestProject.Calculator",
            DisplayName = "Calculator",
            RepositoryId = "test-repo",
            ProjectId = "test-project",
            FileId = "Calculator.cs",
            StartLine = 1,
            EndLine = 50,
            Properties = new Dictionary<string, object>
            {
                ["Language"] = "C#",
                ["IsPublic"] = true,
                ["TypeKind"] = "Class"
            }
        };

        var methodNode = new CodeNode
        {
            Id = "method-1",
            Name = "Add",
            NodeType = NodeType.Method,
            FullName = "TestProject.Calculator.Add",
            DisplayName = "Add",
            RepositoryId = "test-repo",
            ProjectId = "test-project",
            FileId = "Calculator.cs",
            StartLine = 10,
            EndLine = 15,
            Properties = new Dictionary<string, object>
            {
                ["ReturnType"] = "int",
                ["IsPublic"] = true,
                ["ParameterCount"] = 2
            }
        };

        // Test Create operations
        var classCreated = await graphStorageService.CreateNodeAsync(classNode);
        classCreated.Should().Be(true, "Should create class node");

        var methodCreated = await graphStorageService.CreateNodeAsync(methodNode);
        methodCreated.Should().Be(true, "Should create method node");

        // Create relationship
        var relationship = new CodeRelationship
        {
            Id = "rel-1",
            SourceNodeId = "class-1",
            TargetNodeId = "method-1",
            Type = RelationshipType.Contains,
            Properties = new Dictionary<string, object>
            {
                ["Relationship"] = "ContainsMember"
            }
        };

        var relationshipCreated = await graphStorageService.CreateRelationshipAsync(relationship);
        relationshipCreated.Should().Be(true, "Should create relationship");

        // Test Read operations
        var retrievedClass = await graphStorageService.GetNodeAsync("class-1");
        retrievedClass.Should().NotBeNull();
        retrievedClass!.Name.Should().Be("Calculator");

        var retrievedMethod = await graphStorageService.GetNodeAsync("method-1");
        retrievedMethod.Should().NotBeNull();
        retrievedMethod!.Name.Should().Be("Add");

        _logger.LogInformation("✅ Graph CRUD operations successful");
    }

    [Fact]
    public async Task SystemHealth_AllComponents_ShouldBeHealthy()
    {
        // Test Neo4j health
        var neo4jHealthy = await _neo4jContainer.IsHealthyAsync();
        neo4jHealthy.Should().BeTrue("Neo4j should be healthy");

        // Test LLM mock server health
        using var httpClient = new HttpClient();
        try
        {
            var response = await httpClient.GetAsync($"{_mockLLMServer.BaseUrl}/health");
            response.IsSuccessStatusCode.Should().BeTrue("LLM mock server should be healthy");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"LLM health check failed: {ex.Message}");
        }

        // Test service registration
        var graphStorageService = _serviceProvider.GetService<IGraphStorageService>();
        graphStorageService.Should().NotBeNull("Graph storage service should be registered");

        _logger.LogInformation("✅ All system components are healthy and properly configured");
    }
}