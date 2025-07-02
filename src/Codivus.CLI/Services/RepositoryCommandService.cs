using System.Diagnostics;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class RepositoryCommandService
{
    private readonly ApiClientService _apiClient;
    private readonly IOutputService _outputService;
    private readonly IValidationService _validationService;
    private readonly ILogger<RepositoryCommandService> _logger;

    public RepositoryCommandService(
        ApiClientService apiClient,
        IOutputService outputService,
        IValidationService validationService,
        ILogger<RepositoryCommandService> logger)
    {
        _apiClient = apiClient;
        _outputService = outputService;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<CommandResult<RepositoryResult>> AddRepositoryAsync(RepositoryOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Adding repository from path: {Path}", options.Path);

            // Validate the repository path first
            var validationRequest = new RepositoryValidationRequest
            {
                Location = options.Path,
                Type = ConvertTypeToInt(options.Type)
            };
            
            var validationResponse = await _apiClient.ValidateRepositoryAsync(validationRequest);
            if (!validationResponse.Success || validationResponse.Data?.IsValid != true)
            {
                var errors = validationResponse.Data?.Errors ?? new List<string> { "Invalid repository path or type" };
                return CommandResult<RepositoryResult>.ErrorResult(string.Join(", ", errors));
            }

            // Create the repository request
            var createRequest = new CreateRepositoryRequest
            {
                Name = options.Name,
                Location = options.Path,
                Type = ConvertTypeToInt(options.Type),
                DefaultBranch = options.DefaultBranch ?? "main"
            };

            // Add to the repository service
            var createResponse = await _apiClient.CreateRepositoryAsync(createRequest);
            if (!createResponse.Success || createResponse.Data == null)
            {
                return CommandResult<RepositoryResult>.ErrorResult(createResponse.Message ?? "Failed to create repository");
            }
            
            var addedRepository = createResponse.Data;

            stopwatch.Stop();

            var result = new RepositoryResult
            {
                Repository = MapToRepository(addedRepository),
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<RepositoryResult>.SuccessResult(
                result,
                $"Repository '{addedRepository.Name}' added successfully with ID: {addedRepository.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding repository from path: {Path}", options.Path);
            return CommandResult<RepositoryResult>.ErrorResult($"Failed to add repository: {ex.Message}");
        }
    }

    public async Task<CommandResult<RepositoryListResult>> ListRepositoriesAsync(RepositoryOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Listing all repositories");

            var repositoriesResponse = await _apiClient.GetAllRepositoriesAsync();
            if (!repositoriesResponse.Success || repositoriesResponse.Data == null)
            {
                return CommandResult<RepositoryListResult>.ErrorResult(repositoriesResponse.Message ?? "Failed to get repositories");
            }
            
            var repositoryList = repositoriesResponse.Data;

            // Get additional details for each repository
            var repositoryDetails = new List<RepositoryDetail>();
            
            foreach (var repo in repositoryList)
            {
                // Get repository statistics from details endpoint
                var detailsResponse = await _apiClient.GetRepositoryDetailsAsync(repo.Id);
                
                var detail = new RepositoryDetail
                {
                    Repository = MapToRepository(repo),
                    ScanCount = detailsResponse.Success ? detailsResponse.Data?.Summary.TotalScans ?? 0 : 0,
                    IssueCount = detailsResponse.Success ? detailsResponse.Data?.Summary.TotalIssues ?? 0 : 0,
                    HasActiveScans = detailsResponse.Success ? detailsResponse.Data?.Summary.HasActiveScans ?? false : false
                };
                repositoryDetails.Add(detail);
            }

            stopwatch.Stop();

            var result = new RepositoryListResult
            {
                Repositories = repositoryDetails,
                TotalCount = repositoryList.Count,
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<RepositoryListResult>.SuccessResult(
                result,
                $"Found {repositoryList.Count} registered repositories");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing repositories");
            return CommandResult<RepositoryListResult>.ErrorResult($"Failed to list repositories: {ex.Message}");
        }
    }

    public async Task<CommandResult<RepositoryValidationResult>> ValidateRepositoryAsync(RepositoryOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Validating repository at path: {Path}", options.Path);

            var validationRequest = new RepositoryValidationRequest
            {
                Location = options.Path,
                Type = ConvertTypeToInt(options.Type)
            };
            
            var validationResponse = await _apiClient.ValidateRepositoryAsync(validationRequest);
            if (!validationResponse.Success)
            {
                return CommandResult<RepositoryValidationResult>.ErrorResult(validationResponse.Message ?? "Validation failed");
            }
            
            var validationResult = validationResponse.Data!;
            var isValid = validationResult.IsValid;

            stopwatch.Stop();

            var result = new RepositoryValidationResult
            {
                Path = options.Path,
                Type = options.Type,
                IsValid = isValid,
                ValidationErrors = validationResult.Errors,
                ValidationWarnings = validationResult.Warnings,
                Success = true,
                Duration = stopwatch.Elapsed
            };

            var message = isValid 
                ? $"Repository at '{options.Path}' is valid"
                : $"Repository at '{options.Path}' is invalid or inaccessible";

            return CommandResult<RepositoryValidationResult>.SuccessResult(result, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating repository at path: {Path}", options.Path);
            return CommandResult<RepositoryValidationResult>.ErrorResult($"Validation failed: {ex.Message}");
        }
    }

    public async Task<CommandResult<RepositoryResult>> RemoveRepositoryAsync(RepositoryOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Removing repository: {RepositoryId}", options.RepositoryId);

            // Try to parse as GUID first, then search by name
            RepositoryDto? repository = null;
            Guid repoId;
            
            if (Guid.TryParse(options.RepositoryId, out repoId))
            {
                var repoResponse = await _apiClient.GetRepositoryByIdAsync(repoId);
                repository = repoResponse.Success ? repoResponse.Data : null;
            }
            else
            {
                // Search by name
                var repositoriesResponse = await _apiClient.GetAllRepositoriesAsync();
                if (repositoriesResponse.Success && repositoriesResponse.Data != null)
                {
                    repository = repositoriesResponse.Data.FirstOrDefault(r => 
                        string.Equals(r.Name, options.RepositoryId, StringComparison.OrdinalIgnoreCase));
                    
                    if (repository != null)
                    {
                        repoId = repository.Id;
                    }
                }
            }

            if (repository == null)
            {
                return CommandResult<RepositoryResult>.ErrorResult($"Repository '{options.RepositoryId}' not found");
            }

            // Check for confirmation if not forced
            if (!options.Force)
            {
                // Get repository details for better deletion warning
                var detailsResponse = await _apiClient.GetRepositoryDetailsAsync(repoId);
                if (detailsResponse.Success && detailsResponse.Data != null)
                {
                    var summary = detailsResponse.Data.Summary;
                    _outputService.WriteWarning($"Repository '{repository.Name}' will be permanently deleted.");
                    _outputService.WriteWarning($"This will remove {summary.TotalScans} scans, {summary.TotalIssues} issues, and {summary.TotalConfigurations} configurations.");
                }
                else
                {
                    _outputService.WriteWarning($"Repository '{repository.Name}' will be permanently deleted.");
                }
                _outputService.WriteInfo("Use --force to proceed with deletion.");
                
                return CommandResult<RepositoryResult>.ErrorResult("Repository removal cancelled - use --force to proceed");
            }

            var deleteResponse = await _apiClient.DeleteRepositoryAsync(repoId);
            
            if (!deleteResponse.Success)
            {
                return CommandResult<RepositoryResult>.ErrorResult(deleteResponse.Message ?? $"Failed to remove repository '{repository.Name}'");
            }

            stopwatch.Stop();

            var result = new RepositoryResult
            {
                Repository = MapToRepository(repository),
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<RepositoryResult>.SuccessResult(
                result,
                $"Repository '{repository.Name}' removed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing repository: {RepositoryId}", options.RepositoryId);
            return CommandResult<RepositoryResult>.ErrorResult($"Failed to remove repository: {ex.Message}");
        }
    }

    public async Task<CommandResult<RepositoryInfoResult>> GetRepositoryInfoAsync(RepositoryOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Getting repository info: {RepositoryId}", options.RepositoryId);

            // Try to parse as GUID first, then search by name
            RepositoryDto? repository = null;
            Guid repoId;
            
            if (Guid.TryParse(options.RepositoryId, out repoId))
            {
                var repoResponse = await _apiClient.GetRepositoryByIdAsync(repoId);
                repository = repoResponse.Success ? repoResponse.Data : null;
            }
            else
            {
                // Search by name
                var repositoriesResponse = await _apiClient.GetAllRepositoriesAsync();
                if (repositoriesResponse.Success && repositoriesResponse.Data != null)
                {
                    repository = repositoriesResponse.Data.FirstOrDefault(r => 
                        string.Equals(r.Name, options.RepositoryId, StringComparison.OrdinalIgnoreCase));
                    
                    if (repository != null)
                    {
                        repoId = repository.Id;
                    }
                }
            }

            if (repository == null)
            {
                return CommandResult<RepositoryInfoResult>.ErrorResult($"Repository '{options.RepositoryId}' not found");
            }

            // Get repository statistics from details endpoint
            var detailsResponse = await _apiClient.GetRepositoryDetailsAsync(repoId);
            var scanCount = detailsResponse.Success ? detailsResponse.Data?.Summary.TotalScans ?? 0 : 0;
            var issueCount = detailsResponse.Success ? detailsResponse.Data?.Summary.TotalIssues ?? 0 : 0;
            var hasActiveScans = detailsResponse.Success ? detailsResponse.Data?.Summary.HasActiveScans ?? false : false;

            // Get repository structure if requested
            RepositoryFileDto? structure = null;
            if (options.IncludeStructure)
            {
                // Repository structure endpoint not yet implemented in API
                _logger.LogWarning("Repository structure feature not yet available - API endpoint needed");
                _outputService.WriteWarning("Repository structure display is not yet implemented");
            }

            stopwatch.Stop();

            var result = new RepositoryInfoResult
            {
                Repository = MapToRepository(repository),
                ScanCount = scanCount,
                IssueCount = issueCount,
                HasActiveScans = hasActiveScans,
                Structure = structure != null ? MapToRepositoryFile(structure) : null,
                Success = true,
                Duration = stopwatch.Elapsed
            };

            return CommandResult<RepositoryInfoResult>.SuccessResult(
                result,
                $"Repository '{repository.Name}' information retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repository info: {RepositoryId}", options.RepositoryId);
            return CommandResult<RepositoryInfoResult>.ErrorResult($"Failed to get repository info: {ex.Message}");
        }
    }

    private Repository MapToRepository(RepositoryDto dto)
    {
        return new Repository
        {
            Id = dto.Id,
            Name = dto.Name,
            Location = dto.Location,
            Type = dto.TypeName, // Use TypeName property for string representation
            DefaultBranch = dto.DefaultBranch,
            AddedAt = dto.AddedAt,
            LastScannedAt = dto.LastScannedAt
        };
    }

    private RepositoryFile MapToRepositoryFile(RepositoryFileDto dto)
    {
        return new RepositoryFile
        {
            Id = dto.Id,
            RepositoryId = dto.RepositoryId,
            Name = dto.Name,
            Path = dto.Path,
            Extension = dto.Extension,
            IsDirectory = dto.IsDirectory,
            LastModified = dto.LastModified,
            SizeInBytes = dto.SizeInBytes,
            Children = dto.Children?.Select(MapToRepositoryFile).ToList()
        };
    }

    private int ConvertTypeToInt(string type)
    {
        return type.ToLower() switch
        {
            "local" => 0,
            "github" => 1,
            _ => 0 // Default to Local
        };
    }
}