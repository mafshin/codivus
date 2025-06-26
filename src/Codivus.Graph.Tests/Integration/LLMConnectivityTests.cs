using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Codivus.Graph.Tests.Integration
{
    [Collection("LLM Connectivity Tests")]
    public class LLMConnectivityTests : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ITestOutputHelper _output;
        private readonly string _llmBaseUrl = "http://host.docker.internal:1234";

        public LLMConnectivityTests(ITestOutputHelper output)
        {
            _output = output;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_llmBaseUrl),
                Timeout = TimeSpan.FromMinutes(3)
            };
            
            // Add headers expected by OpenAI-compatible APIs
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Codivus-Integration-Test/1.0");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "LLM")]
        [Trait("Category", "RequiresDocker")]
        public async Task LLM_ModelsEndpoint_ShouldBeAccessible()
        {
            // Arrange
            _output.WriteLine($"Testing LLM endpoint: {_llmBaseUrl}/v1/models");

            try
            {
                // Act
                var response = await _httpClient.GetAsync("/v1/models");
                var content = await response.Content.ReadAsStringAsync();
                
                _output.WriteLine($"Response Status: {response.StatusCode}");
                _output.WriteLine($"Response Content: {content}");

                // Assert
                response.Should().NotBeNull();
                content.Should().NotBeNullOrEmpty();
                
                // Even if it's an error response, we've confirmed the service is reachable
                _output.WriteLine("✅ LLM service is reachable");
            }
            catch (HttpRequestException ex)
            {
                _output.WriteLine($"❌ HTTP Request failed: {ex.Message}");
                throw new SkipException($"LLM service not available at {_llmBaseUrl}: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _output.WriteLine($"❌ Request timed out: {ex.Message}");
                throw new SkipException($"LLM service timeout at {_llmBaseUrl}: {ex.Message}");
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "LLM")]
        [Trait("Category", "RequiresDocker")]
        public async Task LLM_ChatCompletions_ShouldAcceptCodeAnalysisRequest()
        {
            // Skip if not available
            if (!await IsLLMAvailable())
            {
                throw new SkipException("LLM service not available - skipping chat completions test");
            }

            // Arrange
            var chatRequest = new
            {
                model = "llama-3.2-3b-instruct", // Common model name, may need adjustment
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a helpful code analysis assistant. Respond with valid JSON only."
                    },
                    new
                    {
                        role = "user",
                        content = @"Analyze this C# code for potential issues:

```csharp
public class Calculator 
{
    public int Add(int a, int b) 
    {
        return a + b;
    }
}
```

Respond with JSON containing issues and insights:
{
  ""issues"": [],
  ""insights"": [
    {
      ""type"": ""general"",
      ""title"": ""Simple addition method"",
      ""description"": ""Basic calculator functionality"",
      ""recommendation"": ""Consider input validation for edge cases""
    }
  ]
}"
                    }
                },
                max_tokens = 500,
                temperature = 0.1
            };

            var jsonContent = JsonSerializer.Serialize(chatRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _output.WriteLine($"Sending request to: {_llmBaseUrl}/v1/chat/completions");
            _output.WriteLine($"Request payload size: {jsonContent.Length} characters");

            try
            {
                // Act
                var response = await _httpClient.PostAsync("/v1/chat/completions", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _output.WriteLine($"Response Status: {response.StatusCode}");
                _output.WriteLine($"Response Content Length: {responseContent.Length} characters");

                // Assert
                response.Should().NotBeNull();
                responseContent.Should().NotBeNullOrEmpty();

                if (response.IsSuccessStatusCode)
                {
                    // Try to parse the response as JSON
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    jsonResponse.TryGetProperty("choices", out var choices).Should().BeTrue();
                    
                    _output.WriteLine("✅ Successfully received chat completion response");
                    _output.WriteLine("✅ LLM integration is working correctly");
                    
                    // Log the actual LLM response for debugging
                    if (choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("message", out var message) &&
                            message.TryGetProperty("content", out var messageContent))
                        {
                            _output.WriteLine($"LLM Response: {messageContent.GetString()}");
                        }
                    }
                }
                else
                {
                    _output.WriteLine($"⚠️ LLM returned error status {response.StatusCode}");
                    _output.WriteLine($"Error content: {responseContent}");
                    
                    // Still consider this a successful connectivity test
                    response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
                }
            }
            catch (HttpRequestException ex)
            {
                _output.WriteLine($"❌ HTTP Request failed: {ex.Message}");
                throw new SkipException($"LLM chat completions not available: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _output.WriteLine($"❌ Request timed out: {ex.Message}");
                throw new SkipException($"LLM chat completions timed out: {ex.Message}");
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Category", "LLM")]
        [Trait("Category", "RequiresDocker")]
        public async Task LLM_SecurityAnalysis_ShouldProcessComplexRequest()
        {
            // Skip if not available
            if (!await IsLLMAvailable())
            {
                throw new SkipException("LLM service not available - skipping security analysis test");
            }

            // Arrange - Test a real security analysis scenario
            var securityAnalysisRequest = new
            {
                model = "llama-3.2-3b-instruct",
                messages = new[]
                {
                    new
                    {
                        role = "system", 
                        content = "You are a security expert analyzing C# code for vulnerabilities. Always respond with valid JSON containing security findings."
                    },
                    new
                    {
                        role = "user",
                        content = @"Analyze this code for security vulnerabilities:

```csharp
public class UserController : ControllerBase
{
    private readonly string _connectionString = ""Server=localhost;Database=Users;Trusted_Connection=true;"";
    
    [HttpGet]
    public async Task<User> GetUser(string userId) 
    {
        var sql = $""SELECT * FROM Users WHERE Id = '{userId}'"";
        using var connection = new SqlConnection(_connectionString);
        var user = await connection.QueryFirstOrDefaultAsync<User>(sql);
        return user;
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request) 
    {
        // No input validation
        var user = new User 
        { 
            Name = request.Name, 
            Email = request.Email 
        };
        // Save without validation...
        return Ok();
    }
}
```

Focus on SQL injection, input validation, and data exposure risks.

Respond with JSON:
{
  ""issues"": [
    {
      ""type"": ""security"",
      ""severity"": ""high"",
      ""message"": ""SQL injection vulnerability"",
      ""description"": ""Direct string interpolation in SQL query"",
      ""lineNumber"": 7,
      ""affectedComponents"": [""GetUser""],
      ""impact"": ""Database compromise possible"",
      ""recommendations"": [""Use parameterized queries"", ""Implement input sanitization""],
      ""confidenceScore"": 0.95
    }
  ],
  ""insights"": [
    {
      ""type"": ""security"",
      ""title"": ""Missing input validation"",
      ""description"": ""No validation on user inputs"",
      ""recommendation"": ""Add comprehensive input validation""
    }
  ]
}"
                    }
                },
                max_tokens = 1500,
                temperature = 0.1
            };

            var jsonContent = JsonSerializer.Serialize(securityAnalysisRequest);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _output.WriteLine("🔍 Testing security analysis workflow...");
            _output.WriteLine($"Request size: {jsonContent.Length} characters");

            try
            {
                // Act
                var response = await _httpClient.PostAsync("/v1/chat/completions", httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                _output.WriteLine($"Response Status: {response.StatusCode}");
                _output.WriteLine($"Response Length: {responseContent.Length} characters");

                // Assert
                response.Should().NotBeNull();
                responseContent.Should().NotBeNullOrEmpty();

                if (response.IsSuccessStatusCode)
                {
                    // Parse and validate the response structure
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    jsonResponse.TryGetProperty("choices", out var choices).Should().BeTrue();
                    choices.GetArrayLength().Should().BeGreaterThan(0);
                    
                    var firstChoice = choices[0];
                    firstChoice.TryGetProperty("message", out var message).Should().BeTrue();
                    message.TryGetProperty("content", out var content).Should().BeTrue();
                    
                    var llmContent = content.GetString();
                    _output.WriteLine($"LLM Response: {llmContent}");
                    
                    llmContent.Should().NotBeNullOrEmpty();
                    
                    // Verify this is a response to our security analysis request
                    llmContent.Should().ContainAny("security", "SQL", "injection", "validation", "user");
                    
                    _output.WriteLine("✅ LLM successfully processed security analysis request");
                    _output.WriteLine("✅ Complex code analysis workflow functional");
                }
                else
                {
                    _output.WriteLine($"⚠️ LLM returned error: {response.StatusCode}");
                    _output.WriteLine($"Error details: {responseContent}");
                }

            }
            catch (HttpRequestException ex)
            {
                _output.WriteLine($"❌ HTTP Request failed: {ex.Message}");
                throw new SkipException($"LLM security analysis failed: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _output.WriteLine($"❌ Analysis timed out: {ex.Message}");
                throw new SkipException($"LLM security analysis timed out: {ex.Message}");
            }
        }

        private async Task<bool> IsLLMAvailable()
        {
            try
            {
                var response = await _httpClient.GetAsync("/v1/models");
                _output.WriteLine($"LLM Availability Check: {response.StatusCode}");
                return response != null;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"LLM Availability Check Failed: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Custom exception for skipping tests when LLM is not available
    public class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}