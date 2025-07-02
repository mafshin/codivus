using System.Text;
using System.Text.Json;
using Codivus.CLI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class ApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClientService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClientService(HttpClient httpClient, IConfiguration configuration, ILogger<ApiClientService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configure base URL from configuration
        var baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
        _httpClient.BaseAddress = new Uri(baseUrl);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    // Repository APIs
    public async Task<ApiResponse<List<RepositoryDto>>> GetAllRepositoriesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/repositories");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var repositories = JsonSerializer.Deserialize<List<RepositoryDto>>(content, _jsonOptions);
                return new ApiResponse<List<RepositoryDto>>
                {
                    Success = true,
                    Data = repositories ?? new List<RepositoryDto>()
                };
            }
            
            return new ApiResponse<List<RepositoryDto>>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repositories");
            return new ApiResponse<List<RepositoryDto>>
            {
                Success = false,
                Message = "Failed to get repositories",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<RepositoryDto>> GetRepositoryByIdAsync(Guid repositoryId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/repositories/{repositoryId}");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var repository = JsonSerializer.Deserialize<RepositoryDto>(content, _jsonOptions);
                return new ApiResponse<RepositoryDto>
                {
                    Success = true,
                    Data = repository
                };
            }
            
            return new ApiResponse<RepositoryDto>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repository {RepositoryId}", repositoryId);
            return new ApiResponse<RepositoryDto>
            {
                Success = false,
                Message = "Failed to get repository",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<RepositoryDto>> CreateRepositoryAsync(CreateRepositoryRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/repositories", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var repository = JsonSerializer.Deserialize<RepositoryDto>(responseContent, _jsonOptions);
                return new ApiResponse<RepositoryDto>
                {
                    Success = true,
                    Data = repository
                };
            }
            
            return new ApiResponse<RepositoryDto>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { responseContent }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating repository");
            return new ApiResponse<RepositoryDto>
            {
                Success = false,
                Message = "Failed to create repository",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<RepositoryValidationResponse>> ValidateRepositoryAsync(RepositoryValidationRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/repositories/validate", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var validation = JsonSerializer.Deserialize<RepositoryValidationResponse>(responseContent, _jsonOptions);
                return new ApiResponse<RepositoryValidationResponse>
                {
                    Success = true,
                    Data = validation
                };
            }
            
            return new ApiResponse<RepositoryValidationResponse>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { responseContent }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating repository");
            return new ApiResponse<RepositoryValidationResponse>
            {
                Success = false,
                Message = "Failed to validate repository",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> DeleteRepositoryAsync(Guid repositoryId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/repositories/{repositoryId}");
            
            if (response.IsSuccessStatusCode)
            {
                return new ApiResponse<bool>
                {
                    Success = true,
                    Data = true
                };
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting repository {RepositoryId}", repositoryId);
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Failed to delete repository",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // Scan APIs
    public async Task<ApiResponse<ScanProgressDto>> StartScanAsync(StartScanRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request.Configuration, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"/api/scanning/start?repositoryId={request.RepositoryId}", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var scanProgress = JsonSerializer.Deserialize<ScanProgressDto>(responseContent, _jsonOptions);
                return new ApiResponse<ScanProgressDto>
                {
                    Success = true,
                    Data = scanProgress
                };
            }
            
            return new ApiResponse<ScanProgressDto>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { responseContent }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting scan");
            return new ApiResponse<ScanProgressDto>
            {
                Success = false,
                Message = "Failed to start scan",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<ScanProgressDto>> GetScanProgressAsync(Guid scanId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/scanning/{scanId}/progress");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var scanProgress = JsonSerializer.Deserialize<ScanProgressDto>(content, _jsonOptions);
                return new ApiResponse<ScanProgressDto>
                {
                    Success = true,
                    Data = scanProgress
                };
            }
            
            return new ApiResponse<ScanProgressDto>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan progress {ScanId}", scanId);
            return new ApiResponse<ScanProgressDto>
            {
                Success = false,
                Message = "Failed to get scan progress",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<CodeIssueDto>>> GetScanIssuesAsync(Guid scanId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/scanning/{scanId}/issues");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var issues = JsonSerializer.Deserialize<List<CodeIssueDto>>(content, _jsonOptions);
                return new ApiResponse<List<CodeIssueDto>>
                {
                    Success = true,
                    Data = issues ?? new List<CodeIssueDto>()
                };
            }
            
            return new ApiResponse<List<CodeIssueDto>>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan issues {ScanId}", scanId);
            return new ApiResponse<List<CodeIssueDto>>
            {
                Success = false,
                Message = "Failed to get scan issues",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> PauseScanAsync(Guid scanId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/scanning/{scanId}/pause", null);
            
            if (response.IsSuccessStatusCode)
            {
                return new ApiResponse<bool>
                {
                    Success = true,
                    Data = true
                };
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing scan {ScanId}", scanId);
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Failed to pause scan",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> ResumeScanAsync(Guid scanId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/scanning/{scanId}/resume", null);
            
            if (response.IsSuccessStatusCode)
            {
                return new ApiResponse<bool>
                {
                    Success = true,
                    Data = true
                };
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming scan {ScanId}", scanId);
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Failed to resume scan",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> CancelScanAsync(Guid scanId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/scanning/{scanId}/cancel", null);
            
            if (response.IsSuccessStatusCode)
            {
                return new ApiResponse<bool>
                {
                    Success = true,
                    Data = true
                };
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling scan {ScanId}", scanId);
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Failed to cancel scan",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // Issues APIs
    public async Task<ApiResponse<List<CodeIssueDto>>> GetAllIssuesAsync(string? repositoryId = null)
    {
        try
        {
            // Since issues are tied to scans, we need to get scans first, then get issues for each scan
            var allIssues = new List<CodeIssueDto>();
            
            if (!string.IsNullOrEmpty(repositoryId))
            {
                // Get scans for specific repository
                var scansResponse = await GetScansForRepositoryAsync(repositoryId);
                if (scansResponse.Success && scansResponse.Data != null)
                {
                    foreach (var scan in scansResponse.Data)
                    {
                        var issuesResponse = await GetIssuesForScanAsync(scan.Id.ToString());
                        if (issuesResponse.Success && issuesResponse.Data != null)
                        {
                            allIssues.AddRange(issuesResponse.Data);
                        }
                    }
                }
            }
            else
            {
                // Get all repositories, then all scans, then all issues
                var reposResponse = await GetAllRepositoriesAsync();
                if (reposResponse.Success && reposResponse.Data != null)
                {
                    foreach (var repo in reposResponse.Data)
                    {
                        var scansResponse = await GetScansForRepositoryAsync(repo.Id.ToString());
                        if (scansResponse.Success && scansResponse.Data != null)
                        {
                            foreach (var scan in scansResponse.Data)
                            {
                                var issuesResponse = await GetIssuesForScanAsync(scan.Id.ToString());
                                if (issuesResponse.Success && issuesResponse.Data != null)
                                {
                                    allIssues.AddRange(issuesResponse.Data);
                                }
                            }
                        }
                    }
                }
            }
            
            return new ApiResponse<List<CodeIssueDto>>
            {
                Success = true,
                Data = allIssues
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all issues");
            return new ApiResponse<List<CodeIssueDto>>
            {
                Success = false,
                Message = "Failed to get issues",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<CodeIssueDto>> GetIssueByIdAsync(Guid issueId)
    {
        try
        {
            // Since there's no direct issue-by-id endpoint, we need to search through all issues
            var allIssuesResponse = await GetAllIssuesAsync();
            if (allIssuesResponse.Success && allIssuesResponse.Data != null)
            {
                var issue = allIssuesResponse.Data.FirstOrDefault(i => i.Id == issueId);
                if (issue != null)
                {
                    return new ApiResponse<CodeIssueDto>
                    {
                        Success = true,
                        Data = issue
                    };
                }
                else
                {
                    return new ApiResponse<CodeIssueDto>
                    {
                        Success = false,
                        Message = $"Issue with ID {issueId} not found"
                    };
                }
            }
            
            return new ApiResponse<CodeIssueDto>
            {
                Success = false,
                Message = allIssuesResponse.Message ?? "Failed to get issues"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issue {IssueId}", issueId);
            return new ApiResponse<CodeIssueDto>
            {
                Success = false,
                Message = "Failed to get issue",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> UpdateIssueStatusAsync(Guid issueId, string status)
    {
        // Note: The backend API doesn't currently support updating issue status
        // Issues are read-only and tied to scans
        await Task.Delay(1); // Make method async
        return new ApiResponse<bool>
        {
            Success = false,
            Message = "Issue status updates are not supported by the API. Issues are read-only and tied to scans."
        };
    }

    // Graph APIs
    public async Task<ApiResponse<GraphScanProgressDto>> StartGraphScanAsync(StartGraphScanRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/graph/scan", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var graphProgress = JsonSerializer.Deserialize<GraphScanProgressDto>(responseContent, _jsonOptions);
                return new ApiResponse<GraphScanProgressDto>
                {
                    Success = true,
                    Data = graphProgress
                };
            }
            
            return new ApiResponse<GraphScanProgressDto>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { responseContent }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting graph scan");
            return new ApiResponse<GraphScanProgressDto>
            {
                Success = false,
                Message = "Failed to start graph scan",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<GraphScanProgressDto>> GetGraphScanProgressAsync(Guid scanId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/graph/scan/{scanId}/progress");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var graphProgress = JsonSerializer.Deserialize<GraphScanProgressDto>(content, _jsonOptions);
                return new ApiResponse<GraphScanProgressDto>
                {
                    Success = true,
                    Data = graphProgress
                };
            }
            
            return new ApiResponse<GraphScanProgressDto>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting graph scan progress {ScanId}", scanId);
            return new ApiResponse<GraphScanProgressDto>
            {
                Success = false,
                Message = "Failed to get graph scan progress",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<GraphMetricsDto>> GetGraphMetricsAsync(string repositoryId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/graph/repositories/{repositoryId}/metrics");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var metrics = JsonSerializer.Deserialize<GraphMetricsDto>(content, _jsonOptions);
                return new ApiResponse<GraphMetricsDto>
                {
                    Success = true,
                    Data = metrics
                };
            }
            
            return new ApiResponse<GraphMetricsDto>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting graph metrics for repository {RepositoryId}", repositoryId);
            return new ApiResponse<GraphMetricsDto>
            {
                Success = false,
                Message = "Failed to get graph metrics",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // Helper methods for issues (which are tied to scans)
    public async Task<ApiResponse<List<ScanProgressDto>>> GetAllScansAsync()
    {
        try
        {
            // Since there's no single endpoint for all scans, we need to:
            // 1. Get all repositories
            // 2. Get scans for each repository
            // 3. Aggregate the results
            
            var allScans = new List<ScanProgressDto>();
            
            // Get all repositories first
            var repositoriesResponse = await GetAllRepositoriesAsync();
            if (!repositoriesResponse.Success || repositoriesResponse.Data == null)
            {
                return new ApiResponse<List<ScanProgressDto>>
                {
                    Success = false,
                    Message = "Failed to get repositories to fetch scans",
                    Errors = repositoriesResponse.Errors
                };
            }
            
            // Get scans for each repository
            foreach (var repo in repositoriesResponse.Data)
            {
                try
                {
                    var scansResponse = await GetScansForRepositoryAsync(repo.Id.ToString());
                    if (scansResponse.Success && scansResponse.Data != null)
                    {
                        allScans.AddRange(scansResponse.Data);
                    }
                    // Continue even if one repository fails
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get scans for repository {RepositoryId}", repo.Id);
                    // Continue with other repositories
                }
            }
            
            return new ApiResponse<List<ScanProgressDto>>
            {
                Success = true,
                Data = allScans
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all scans");
            return new ApiResponse<List<ScanProgressDto>>
            {
                Success = false,
                Message = "Failed to get all scans",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<ScanProgressDto>>> GetScansForRepositoryAsync(string repositoryId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/scanning/repository/{repositoryId}");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var scans = JsonSerializer.Deserialize<List<ScanProgressDto>>(content, _jsonOptions);
                return new ApiResponse<List<ScanProgressDto>>
                {
                    Success = true,
                    Data = scans ?? new List<ScanProgressDto>()
                };
            }
            
            return new ApiResponse<List<ScanProgressDto>>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scans for repository {RepositoryId}", repositoryId);
            return new ApiResponse<List<ScanProgressDto>>
            {
                Success = false,
                Message = "Failed to get scans",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<CodeIssueDto>>> GetIssuesForScanAsync(string scanId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/scanning/{scanId}/issues");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var issues = JsonSerializer.Deserialize<List<CodeIssueDto>>(content, _jsonOptions);
                return new ApiResponse<List<CodeIssueDto>>
                {
                    Success = true,
                    Data = issues ?? new List<CodeIssueDto>()
                };
            }
            
            return new ApiResponse<List<CodeIssueDto>>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issues for scan {ScanId}", scanId);
            return new ApiResponse<List<CodeIssueDto>>
            {
                Success = false,
                Message = "Failed to get issues for scan",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // Repository details and statistics
    public async Task<ApiResponse<RepositoryDetailsDto>> GetRepositoryDetailsAsync(Guid repositoryId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/repositories/{repositoryId}/details");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var details = JsonSerializer.Deserialize<RepositoryDetailsDto>(content, _jsonOptions);
                return new ApiResponse<RepositoryDetailsDto>
                {
                    Success = true,
                    Data = details
                };
            }
            
            return new ApiResponse<RepositoryDetailsDto>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repository details {RepositoryId}", repositoryId);
            return new ApiResponse<RepositoryDetailsDto>
            {
                Success = false,
                Message = "Failed to get repository details",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // LLM Provider APIs
    public async Task<ApiResponse<List<string>>> GetAvailableModelsAsync(string providerType)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/llmprovider/models?providerType={providerType}");
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var models = JsonSerializer.Deserialize<List<string>>(content, _jsonOptions);
                return new ApiResponse<List<string>>
                {
                    Success = true,
                    Data = models ?? new List<string>()
                };
            }
            
            return new ApiResponse<List<string>>
            {
                Success = false,
                Message = $"API call failed: {response.StatusCode}",
                Errors = new List<string> { content }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models for provider {ProviderType}", providerType);
            return new ApiResponse<List<string>>
            {
                Success = false,
                Message = "Failed to get available models",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}