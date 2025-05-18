using System.Text;
using System.Text.Json;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Codivus.API.LLM;

/// <summary>
/// Configuration options for the LMStudio provider
/// </summary>
public class LmStudioOptions
{
    /// <summary>
    /// API base URL for LMStudio
    /// </summary>
    public string BaseUrl { get; set; }
    
    /// <summary>
    /// API key for LMStudio (if required)
    /// </summary>
    public string ApiKey { get; set; }
    
    /// <summary>
    /// Maximum tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 2048;
    
    /// <summary>
    /// Temperature parameter for generation
    /// </summary>
    public float Temperature { get; set; } = 0.7f;
    
    /// <summary>
    /// Top-p parameter for generation
    /// </summary>
    public float TopP { get; set; } = 0.9f;
    
    /// <summary>
    /// Maximum number of retries for API calls
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Timeout in seconds for API calls
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Implementation of the LLM provider interface for LMStudio
/// </summary>
public class LmStudioProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LmStudioOptions _options;
    private readonly ILogger<LmStudioProvider> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    /// <summary>
    /// Gets the provider type
    /// </summary>
    public LlmProviderType ProviderType => LlmProviderType.LmStudio;

    /// <summary>
    /// Initializes a new instance of the LmStudioProvider class
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory</param>
    /// <param name="options">LMStudio options</param>
    /// <param name="logger">Logger</param>
    public LmStudioProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<LmStudioOptions> options,
        ILogger<LmStudioProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("LMStudio");
        _options = options.Value;
        _logger = logger;

        // Configure the HTTP client
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        
        // Add API key if provided
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
        }

        // Create a retry policy
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                _options.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Error calling LMStudio API (Attempt {RetryCount} of {MaxRetries}). Retrying in {RetryTimeSpan}...",
                        retryCount,
                        _options.MaxRetries,
                        timeSpan);
                });
    }

    /// <summary>
    /// Gets the available models
    /// </summary>
    /// <returns>Collection of available model names</returns>
    public async Task<IEnumerable<string>> GetAvailableModelsAsync()
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                // Try to get models from the API
                var response = await _httpClient.GetAsync("models");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                var rootElement = jsonDoc.RootElement;
                
                var modelNames = new List<string>();
                try
                {
                    // Try to parse as modern API format
                    if (rootElement.TryGetProperty("data", out var modelsData))
                    {
                        foreach (var model in modelsData.EnumerateArray())
                        {
                            if (model.TryGetProperty("id", out var idProp))
                            {
                                var id = idProp.GetString();
                                if (!string.IsNullOrEmpty(id))
                                {
                                    modelNames.Add(id);
                                }
                            }
                        }
                    }
                    // Check for alternative formats
                    else if (rootElement.ValueKind == JsonValueKind.Array)
                    {
                        // Try to parse as direct array
                        foreach (var model in rootElement.EnumerateArray())
                        {
                            if (model.TryGetProperty("id", out var idProp))
                            {
                                var id = idProp.GetString();
                                if (!string.IsNullOrEmpty(id))
                                {
                                    modelNames.Add(id);
                                }
                            }
                            else if (model.ValueKind == JsonValueKind.String)
                            {
                                var id = model.GetString();
                                if (!string.IsNullOrEmpty(id))
                                {
                                    modelNames.Add(id);
                                }
                            }
                        }
                    }
                    // If we couldn't parse the structure but response was successful, add fallback model
                    if (modelNames.Count == 0)
                    {
                        modelNames.Add("default");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing model list JSON structure, using fallback model name");
                    modelNames.Add("default");
                }

                return modelNames;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models from LMStudio");
            return new List<string> { "default", "codellama:7b-instruct" };
        }
    }

    /// <summary>
    /// Checks if the provider is available
    /// </summary>
    /// <returns>True if available, false otherwise</returns>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/models");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking LMStudio availability");
            return false;
        }
    }

    /// <summary>
    /// Analyzes code using the LLM
    /// </summary>
    /// <param name="code">Code to analyze</param>
    /// <param name="filePath">Path to the file</param>
    /// <param name="configuration">Scan configuration</param>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Collection of detected issues</returns>
    public async Task<IEnumerable<CodeIssue>> AnalyzeCodeAsync(string code, string filePath, ScanConfiguration configuration, Guid scanId)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var prompt = BuildCodeAnalysisPrompt(code, filePath);
                
                var requestBody = new
                {
                    model = "default", // LMStudio server typically uses the currently loaded model
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are a code analysis tool that identifies issues, vulnerabilities, and improvements in code. " +
                                    "You analyze the given code and report issues in JSON format."
                        },
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    temperature = _options.Temperature,
                    top_p = _options.TopP,
                    max_tokens = _options.MaxTokens
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonDocument.Parse(responseContent).RootElement;
                
                var choices = responseJson.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message");
                    var responseText = message.GetProperty("content").GetString() ?? string.Empty;
                    
                    return ParseCodeIssues(responseText, filePath, configuration.RepositoryId, scanId);
                }
                
                return new List<CodeIssue>();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing code with LMStudio");
            return new List<CodeIssue>();
        }
    }

    /// <summary>
    /// Generates a fix suggestion for an issue
    /// </summary>
    /// <param name="issue">Code issue to fix</param>
    /// <param name="code">Original code</param>
    /// <returns>Suggested fix</returns>
    public async Task<string> GenerateFixSuggestionAsync(CodeIssue issue, string code)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var prompt = BuildFixSuggestionPrompt(issue, code);
                
                var requestBody = new
                {
                    model = "default",
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are a code improvement assistant that provides fixes for code issues. " +
                                     "You provide clear, concise fixes with explanations."
                        },
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    temperature = _options.Temperature,
                    top_p = _options.TopP,
                    max_tokens = _options.MaxTokens
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonDocument.Parse(responseContent).RootElement;
                
                var choices = responseJson.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message");
                    var responseText = message.GetProperty("content").GetString() ?? string.Empty;
                    
                    return responseText;
                }
                
                return "Unable to generate fix suggestion.";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating fix suggestion with LMStudio");
            return "Unable to generate fix suggestion.";
        }
    }

    /// <summary>
    /// Builds a prompt for code analysis
    /// </summary>
    /// <param name="code">Code to analyze</param>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Prompt string</returns>
    private string BuildCodeAnalysisPrompt(string code, string filePath)
    {
        var extension = Path.GetExtension(filePath);
        var language = GetLanguageFromExtension(extension);
        
        return $@"Analyze the following {language} code and identify issues, vulnerabilities, or improvements. 
The code is from the file: {filePath}

```{language}
{code}
```

Return a JSON array of found issues in the following format:
```json
[
  {{
    ""title"": ""Issue title"",
    ""description"": ""Detailed description of the issue"",
    ""line_number"": 123,
    ""severity"": ""Critical|High|Medium|Low|Info"",
    ""category"": ""Security|Performance|Quality|Architecture|Dependency|Testing|Documentation|Accessibility|Other"",
    ""confidence"": 0.95,
    ""suggested_fix"": ""Suggested code fix or improvement""
  }}
]
```

If no issues are found, return an empty JSON array: []
Focus on the most important issues first. Only include significant issues with high confidence.";
    }

    /// <summary>
    /// Builds a prompt for fix suggestion
    /// </summary>
    /// <param name="issue">Code issue to fix</param>
    /// <param name="code">Original code</param>
    /// <returns>Prompt string</returns>
    private string BuildFixSuggestionPrompt(CodeIssue issue, string code)
    {
        var extension = Path.GetExtension(issue.FilePath);
        var language = GetLanguageFromExtension(extension);
        
        return $@"I need help fixing the following issue in my {language} code:

Issue: {issue.Title}
Description: {issue.Description}
File: {issue.FilePath}
Line: {issue.LineNumber}
Severity: {issue.Severity}
Category: {issue.Category}

Here's the relevant code:

```{language}
{code}
```

Please provide:
1. A clear explanation of how to fix this issue
2. The corrected code snippet
3. Any additional recommendations to prevent similar issues

Format your response as a detailed explanation followed by the corrected code in a code block.";
    }

    /// <summary>
    /// Gets the programming language from file extension
    /// </summary>
    /// <param name="extension">File extension</param>
    /// <returns>Language name</returns>
    private string GetLanguageFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".vue" => "vue",
            ".py" => "python",
            ".java" => "java",
            ".go" => "go",
            ".rb" => "ruby",
            ".php" => "php",
            ".c" => "c",
            ".cpp" => "cpp",
            ".h" => "c",
            ".hpp" => "cpp",
            ".swift" => "swift",
            ".kt" => "kotlin",
            ".rs" => "rust",
            ".sh" => "bash",
            ".html" => "html",
            ".css" => "css",
            ".scss" => "scss",
            ".sql" => "sql",
            ".md" => "markdown",
            ".json" => "json",
            ".xml" => "xml",
            ".yml" => "yaml",
            ".yaml" => "yaml",
            _ => "text"
        };
    }

    /// <summary>
    /// Parses code issues from LLM response
    /// </summary>
    /// <param name="responseText">Response text from LLM</param>
    /// <param name="filePath">Path to the file</param>
    /// <param name="repositoryId">Repository ID</param>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Collection of code issues</returns>
    private List<CodeIssue> ParseCodeIssues(string responseText, string filePath, Guid repositoryId, Guid scanId)
    {
        var issues = new List<CodeIssue>();
        
        try
        {
            // Extract JSON from the response
            var jsonStartIndex = responseText.IndexOf('[');
            var jsonEndIndex = responseText.LastIndexOf(']');
            
            if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
            {
                var jsonText = responseText.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                var jsonDoc = JsonDocument.Parse(jsonText);
                
                foreach (var issueElement in jsonDoc.RootElement.EnumerateArray())
                {
                    var issue = new CodeIssue
                    {
                        Id = Guid.NewGuid(),
                        RepositoryId = repositoryId,
                        ScanId = scanId,
                        FilePath = filePath,
                        Title = issueElement.GetProperty("title").GetString() ?? "Untitled Issue",
                        Description = issueElement.GetProperty("description").GetString() ?? "No description provided.",
                        DetectionMethod = IssueDetectionMethod.AiAnalysis,
                        DetectedAt = DateTime.UtcNow
                    };
                    
                    // Parse line number
                    if (issueElement.TryGetProperty("line_number", out var lineNumberElement))
                    {
                        issue.LineNumber = lineNumberElement.GetInt32();
                    }
                    else
                    {
                        issue.LineNumber = 1; // Default to line 1 if not specified
                    }
                    
                    // Parse severity
                    if (issueElement.TryGetProperty("severity", out var severityElement))
                    {
                        var severityString = severityElement.GetString();
                        issue.Severity = severityString?.ToLowerInvariant() switch
                        {
                            "critical" => IssueSeverity.Critical,
                            "high" => IssueSeverity.High,
                            "medium" => IssueSeverity.Medium,
                            "low" => IssueSeverity.Low,
                            "info" => IssueSeverity.Info,
                            _ => IssueSeverity.Medium
                        };
                    }
                    
                    // Parse category
                    if (issueElement.TryGetProperty("category", out var categoryElement))
                    {
                        var categoryString = categoryElement.GetString();
                        issue.Category = categoryString?.ToLowerInvariant() switch
                        {
                            "security" => IssueCategory.Security,
                            "performance" => IssueCategory.Performance,
                            "quality" => IssueCategory.Quality,
                            "architecture" => IssueCategory.Architecture,
                            "dependency" => IssueCategory.Dependency,
                            "testing" => IssueCategory.Testing,
                            "documentation" => IssueCategory.Documentation,
                            "accessibility" => IssueCategory.Accessibility,
                            _ => IssueCategory.Other
                        };
                    }
                    
                    // Parse confidence
                    if (issueElement.TryGetProperty("confidence", out var confidenceElement))
                    {
                        issue.Confidence = confidenceElement.GetDouble();
                    }
                    else
                    {
                        issue.Confidence = 0.7; // Default confidence
                    }
                    
                    // Parse suggested fix
                    if (issueElement.TryGetProperty("suggested_fix", out var fixElement))
                    {
                        issue.SuggestedFix = fixElement.GetString();
                    }
                    
                    // Parse code snippet if available
                    if (issueElement.TryGetProperty("code_snippet", out var snippetElement))
                    {
                        issue.CodeSnippet = snippetElement.GetString();
                    }
                    
                    issues.Add(issue);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing code issues from LLM response");
        }
        
        return issues;
    }
}
