using System.Diagnostics;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class RepositoryCommandService
{
    private readonly IRepositoryService _repositoryService;
    private readonly IOutputService _outputService;
    private readonly IValidationService _validationService;
    private readonly ILogger<RepositoryCommandService> _logger;

    public RepositoryCommandService(
        IRepositoryService repositoryService,
        IOutputService outputService,
        IValidationService validationService,
        ILogger<RepositoryCommandService> logger)
    {
        _repositoryService = repositoryService;
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
            var repositoryType = Enum.Parse<RepositoryType>(options.Type, true);
            var isValid = await _repositoryService.ValidateRepositoryAsync(options.Path, repositoryType);
            
            if (!isValid)
            {
                return CommandResult<RepositoryResult>.ErrorResult("Invalid repository path or type");
            }

            // Create the repository object
            var repository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = options.Name,
                Location = options.Path,
                Type = repositoryType,
                DefaultBranch = options.DefaultBranch ?? "main",
                AddedAt = DateTime.UtcNow
            };

            // Add to the repository service
            var addedRepository = await _repositoryService.AddRepositoryAsync(repository);

            stopwatch.Stop();

            var result = new RepositoryResult
            {
                Repository = addedRepository,
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

            var repositories = await _repositoryService.GetAllRepositoriesAsync();
            var repositoryList = repositories.ToList();

            // Get additional details if requested
            var repositoryDetails = new List<RepositoryDetail>();
            
            foreach (var repo in repositoryList)
            {
                var detail = new RepositoryDetail
                {
                    Repository = repo,
                    ScanCount = options.Detailed ? await _repositoryService.GetScanCountAsync(repo.Id) : 0,
                    IssueCount = options.Detailed ? await _repositoryService.GetIssueCountAsync(repo.Id) : 0,
                    HasActiveScans = options.Detailed ? await _repositoryService.HasActiveScansAsync(repo.Id) : false
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

            var repositoryType = Enum.Parse<RepositoryType>(options.Type, true);
            var isValid = await _repositoryService.ValidateRepositoryAsync(options.Path, repositoryType);

            stopwatch.Stop();

            var result = new RepositoryValidationResult
            {
                Path = options.Path,
                Type = repositoryType,
                IsValid = isValid,
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
            Repository? repository = null;
            
            if (Guid.TryParse(options.RepositoryId, out var repoId))
            {
                repository = await _repositoryService.GetRepositoryByIdAsync(repoId);
            }
            else
            {
                // Search by name
                var repositories = await _repositoryService.GetAllRepositoriesAsync();
                repository = repositories.FirstOrDefault(r => 
                    string.Equals(r.Name, options.RepositoryId, StringComparison.OrdinalIgnoreCase));
                
                if (repository != null)
                {
                    repoId = repository.Id;
                }
            }

            if (repository == null)
            {
                return CommandResult<RepositoryResult>.ErrorResult($"Repository '{options.RepositoryId}' not found");
            }

            // Check for confirmation if not forced
            if (!options.Force)
            {
                var scanCount = await _repositoryService.GetScanCountAsync(repoId);
                var issueCount = await _repositoryService.GetIssueCountAsync(repoId);
                
                if (scanCount > 0 || issueCount > 0)
                {
                    _outputService.WriteWarning(
                        $"Repository '{repository.Name}' has {scanCount} scans and {issueCount} issues that will be deleted.");
                    _outputService.WriteInfo("Use --force to proceed with deletion.");
                    
                    return CommandResult<RepositoryResult>.ErrorResult("Repository removal cancelled - use --force to proceed");
                }
            }

            var success = await _repositoryService.DeleteRepositoryAsync(repoId);
            
            if (!success)
            {
                return CommandResult<RepositoryResult>.ErrorResult($"Failed to remove repository '{repository.Name}'");
            }

            stopwatch.Stop();

            var result = new RepositoryResult
            {
                Repository = repository,
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
            Repository? repository = null;
            
            if (Guid.TryParse(options.RepositoryId, out var repoId))
            {
                repository = await _repositoryService.GetRepositoryByIdAsync(repoId);
            }
            else
            {
                // Search by name
                var repositories = await _repositoryService.GetAllRepositoriesAsync();
                repository = repositories.FirstOrDefault(r => 
                    string.Equals(r.Name, options.RepositoryId, StringComparison.OrdinalIgnoreCase));
                
                if (repository != null)
                {
                    repoId = repository.Id;
                }
            }

            if (repository == null)
            {
                return CommandResult<RepositoryInfoResult>.ErrorResult($"Repository '{options.RepositoryId}' not found");
            }

            // Get repository statistics
            var scanCount = await _repositoryService.GetScanCountAsync(repoId);
            var issueCount = await _repositoryService.GetIssueCountAsync(repoId);
            var hasActiveScans = await _repositoryService.HasActiveScansAsync(repoId);

            // Get repository structure if requested
            RepositoryFile? structure = null;
            if (options.IncludeStructure)
            {
                try
                {
                    structure = await _repositoryService.GetRepositoryStructureAsync(repoId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get repository structure for {RepositoryId}", repoId);
                }
            }

            stopwatch.Stop();

            var result = new RepositoryInfoResult
            {
                Repository = repository,
                ScanCount = scanCount,
                IssueCount = issueCount,
                HasActiveScans = hasActiveScans,
                Structure = structure,
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
}