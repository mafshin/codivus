using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Codivus.CLI.Tests.Helpers;

public class MockLLMServer : IAsyncLifetime
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _serverTask;
    
    public int Port { get; private set; }
    public string BaseUrl => $"http://localhost:{Port}";
    public List<LLMRequest> ReceivedRequests { get; } = new();

    public async Task InitializeAsync()
    {
        // Find an available port
        Port = GetAvailablePort();
        
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        _listener.Start();
        
        _cancellationTokenSource = new CancellationTokenSource();
        _serverTask = HandleRequestsAsync(_cancellationTokenSource.Token);
        
        // Verify server is running
        await Task.Delay(100);
    }

    public async Task DisposeAsync()
    {
        _cancellationTokenSource?.Cancel();
        
        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }
        
        _listener?.Stop();
        _listener?.Close();
        _cancellationTokenSource?.Dispose();
    }

    private async Task HandleRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(async () => await ProcessRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException)
            {
                // Listener was stopped
                break;
            }
            catch (ObjectDisposedException)
            {
                // Listener was disposed
                break;
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        
        try
        {
            // Read request body
            string requestBody = "";
            if (request.HasEntityBody)
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                requestBody = await reader.ReadToEndAsync();
            }

            // Log the request
            var llmRequest = new LLMRequest
            {
                Method = request.HttpMethod,
                Url = request.Url?.ToString() ?? "",
                Headers = request.Headers.AllKeys.Where(k => k != null).ToDictionary(k => k!, k => request.Headers[k]),
                Body = requestBody,
                Timestamp = DateTime.UtcNow
            };
            ReceivedRequests.Add(llmRequest);

            // Route the request
            var responseContent = await RouteRequestAsync(request, requestBody);
            
            // Send response
            var buffer = Encoding.UTF8.GetBytes(responseContent);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "application/json";
            response.StatusCode = 200;
            
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            // Send error response
            var errorResponse = JsonSerializer.Serialize(new { error = ex.Message });
            var buffer = Encoding.UTF8.GetBytes(errorResponse);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "application/json";
            response.StatusCode = 500;
            
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        finally
        {
            response.Close();
        }
    }

    private async Task<string> RouteRequestAsync(HttpListenerRequest request, string requestBody)
    {
        var path = request.Url?.AbsolutePath ?? "";
        
        return path switch
        {
            "/api/generate" => await HandleOllamaGenerateAsync(requestBody),
            "/v1/completions" => await HandleLMStudioCompletionsAsync(requestBody),
            "/v1/chat/completions" => await HandleLMStudioChatCompletionsAsync(requestBody),
            "/api/tags" => await HandleOllamaTagsAsync(),
            "/health" => HandleHealthCheck(),
            _ => HandleNotFound(path)
        };
    }

    private async Task<string> HandleOllamaGenerateAsync(string requestBody)
    {
        await Task.Delay(100); // Simulate processing time
        
        var request = JsonSerializer.Deserialize<JsonElement>(requestBody);
        var prompt = request.TryGetProperty("prompt", out var promptProp) ? promptProp.GetString() : "";
        
        var response = new
        {
            model = "codellama:7b",
            created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            response = GenerateMockCodeAnalysis(prompt ?? ""),
            done = true,
            context = new int[] { 1, 2, 3 },
            total_duration = 1000000000, // 1 second in nanoseconds
            load_duration = 100000000,
            prompt_eval_count = 50,
            prompt_eval_duration = 500000000,
            eval_count = 100,
            eval_duration = 400000000
        };
        
        return JsonSerializer.Serialize(response);
    }

    private async Task<string> HandleLMStudioCompletionsAsync(string requestBody)
    {
        await Task.Delay(150); // Simulate processing time
        
        var request = JsonSerializer.Deserialize<JsonElement>(requestBody);
        var prompt = request.TryGetProperty("prompt", out var promptProp) ? promptProp.GetString() : "";
        
        var response = new
        {
            id = $"cmpl-{Guid.NewGuid()}",
            @object = "text_completion",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            model = "codellama-7b-instruct",
            choices = new[]
            {
                new
                {
                    text = GenerateMockCodeAnalysis(prompt ?? ""),
                    index = 0,
                    logprobs = (object?)null,
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 50,
                completion_tokens = 100,
                total_tokens = 150
            }
        };
        
        return JsonSerializer.Serialize(response);
    }

    private async Task<string> HandleLMStudioChatCompletionsAsync(string requestBody)
    {
        await Task.Delay(120); // Simulate processing time
        
        var request = JsonSerializer.Deserialize<JsonElement>(requestBody);
        var messages = request.TryGetProperty("messages", out var messagesProp) ? messagesProp : new JsonElement();
        
        string userMessage = "";
        if (messages.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in messages.EnumerateArray())
            {
                if (message.TryGetProperty("role", out var role) && role.GetString() == "user")
                {
                    if (message.TryGetProperty("content", out var content))
                    {
                        userMessage = content.GetString() ?? "";
                        break;
                    }
                }
            }
        }
        
        var response = new
        {
            id = $"chatcmpl-{Guid.NewGuid()}",
            @object = "chat.completion",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            model = "codellama-7b-instruct",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = GenerateMockCodeAnalysis(userMessage)
                    },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 75,
                completion_tokens = 125,
                total_tokens = 200
            }
        };
        
        return JsonSerializer.Serialize(response);
    }

    private async Task<string> HandleOllamaTagsAsync()
    {
        await Task.Delay(50);
        
        var response = new
        {
            models = new[]
            {
                new
                {
                    name = "codellama:7b",
                    size = 3826793677,
                    digest = "8fdf8f752f6e6d8c34c4c5c5f5c5f5c5f5c5f5c5",
                    modified_at = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            }
        };
        
        return JsonSerializer.Serialize(response);
    }

    private string HandleHealthCheck()
    {
        return JsonSerializer.Serialize(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    private string HandleNotFound(string path)
    {
        return JsonSerializer.Serialize(new { error = $"Not found: {path}" });
    }

    private string GenerateMockCodeAnalysis(string prompt)
    {
        // Generate realistic code analysis responses based on the prompt
        if (prompt.Contains("Calculator", StringComparison.OrdinalIgnoreCase))
        {
            return @"Based on the Calculator class analysis:

**Code Quality Issues:**
1. **Memory Usage**: The _history list grows indefinitely without bounds checking
   - Severity: Medium
   - Recommendation: Implement a maximum history size or provide cleanup methods

2. **Exception Handling**: Division by zero throws ArgumentException instead of more specific DivideByZeroException
   - Severity: Low
   - Recommendation: Use more specific exception types

**Security Analysis:**
- No security vulnerabilities detected in this calculator implementation
- Input validation is present for division operations

**Performance Considerations:**
- List operations are O(1) for append, acceptable for this use case
- Consider using Queue<double> if FIFO behavior is desired for history

**Architectural Recommendations:**
- Consider implementing ICalculator interface for better testability
- Add async variants if calculator operations become more complex";
        }

        if (prompt.Contains("DataService", StringComparison.OrdinalIgnoreCase))
        {
            return @"DataService Analysis:

**Potential Issues:**
1. **Concurrency**: Dictionary is not thread-safe for concurrent access
   - Severity: High
   - Recommendation: Use ConcurrentDictionary or implement locking

2. **Missing ConfigureAwait**: Async methods don't use ConfigureAwait(false)
   - Severity: Medium 
   - Recommendation: Add ConfigureAwait(false) to avoid deadlocks

**Security Review:**
- Data validation needed for id and data parameters
- Consider implementing data sanitization

**Performance:**
- In-memory storage is fast but not persistent
- Consider implementing actual storage backend for production use";
        }

        // Default analysis response
        return @"Code Analysis Complete:

**Summary:**
- Files analyzed successfully
- No critical security vulnerabilities found
- Several code quality improvements recommended

**Key Findings:**
- Memory management could be optimized
- Consider adding more comprehensive error handling
- Code follows general best practices

**Recommendations:**
- Implement proper logging throughout the application
- Add comprehensive unit tests for edge cases
- Consider adding XML documentation for public APIs";
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

public class LLMRequest
{
    public string Method { get; set; } = "";
    public string Url { get; set; } = "";
    public Dictionary<string, string?> Headers { get; set; } = new();
    public string Body { get; set; } = "";
    public DateTime Timestamp { get; set; }
}