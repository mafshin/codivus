using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Codivus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoriesController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly ILogger<RepositoriesController> _logger;

    public RepositoriesController(
        IRepositoryService repositoryService,
        ILogger<RepositoriesController> logger)
    {
        _repositoryService = repositoryService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all repositories
    /// </summary>
    /// <returns>Collection of repositories</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Repository>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRepositories()
    {
        try
        {
            var repositories = await _repositoryService.GetAllRepositoriesAsync();
            return Ok(repositories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all repositories");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving repositories");
        }
    }

    /// <summary>
    /// Gets a repository by ID
    /// </summary>
    /// <param name="id">Repository ID</param>
    /// <returns>Repository if found</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepositoryById(Guid id)
    {
        try
        {
            _logger.LogInformation("Getting repository with ID: {RepositoryId}", id);
            var repository = await _repositoryService.GetRepositoryByIdAsync(id);
            if (repository == null)
            {
                _logger.LogWarning("Repository with ID {RepositoryId} not found", id);
                return NotFound($"Repository with ID {id} not found");
            }

            _logger.LogInformation("Successfully retrieved repository: {RepositoryName}", repository.Name);
            return Ok(repository);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repository with ID {RepositoryId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the repository");
        }
    }

    /// <summary>
    /// Creates a new repository
    /// </summary>
    /// <param name="repository">Repository to create</param>
    /// <returns>Created repository</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRepository(Repository repository)
    {
        try
        {
            _logger.LogInformation("Creating new repository: {RepositoryName} at {RepositoryLocation} of type {RepositoryType}", 
                repository?.Name, repository?.Location, repository?.Type);
                
            if (repository == null)
            {
                _logger.LogWarning("Repository cannot be null");
                return BadRequest("Repository cannot be null");
            }
            
            if (string.IsNullOrWhiteSpace(repository.Location))
            {
                _logger.LogWarning("Repository location cannot be empty");
                return BadRequest("Repository location cannot be empty");
            }

            try
            {
                // Validate repository
                _logger.LogInformation("Validating repository at {RepositoryLocation}", repository.Location);
                var isValid = await _repositoryService.ValidateRepositoryAsync(repository.Location, repository.Type);
                if (!isValid)
                {
                    _logger.LogWarning("Invalid repository at location: {RepositoryLocation}", repository.Location);
                    // Return a more specific message for debugging
                    return BadRequest($"Invalid repository at location: {repository.Location} - Directory does not exist or is not accessible");
                }

                var createdRepository = await _repositoryService.AddRepositoryAsync(repository);
                _logger.LogInformation("Repository created successfully with ID: {RepositoryId}", createdRepository.Id);
                return CreatedAtAction(nameof(GetRepositoryById), new { id = createdRepository.Id }, createdRepository);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating repository location: {RepositoryLocation}", repository.Location);
                return BadRequest($"Error validating repository location: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating repository: {RepositoryName} at {RepositoryLocation}", 
                repository?.Name, repository?.Location);
            return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while creating the repository: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing repository
    /// </summary>
    /// <param name="id">Repository ID</param>
    /// <param name="repository">Updated repository data</param>
    /// <returns>Updated repository</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRepository(Guid id, Repository repository)
    {
        try
        {
            if (repository == null || id != repository.Id)
            {
                return BadRequest("Invalid repository data or ID mismatch");
            }

            var existingRepository = await _repositoryService.GetRepositoryByIdAsync(id);
            if (existingRepository == null)
            {
                return NotFound($"Repository with ID {id} not found");
            }

            var updatedRepository = await _repositoryService.UpdateRepositoryAsync(repository);
            return Ok(updatedRepository);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating repository with ID {RepositoryId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the repository");
        }
    }

    /// <summary>
    /// Deletes a repository and all its associated scans and issues
    /// </summary>
    /// <param name="id">Repository ID to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteRepository(Guid id)
    {
        try
        {
            var repository = await _repositoryService.GetRepositoryByIdAsync(id);
            if (repository == null)
            {
                return NotFound($"Repository with ID {id} not found");
            }

            var result = await _repositoryService.DeleteRepositoryAsync(id);
            if (!result)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete the repository");
            }

            _logger.LogInformation("Repository {RepositoryName} (ID: {RepositoryId}) and all associated data deleted successfully", 
                repository.Name, id);
            
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            // This is thrown when there are active scans
            _logger.LogWarning("Cannot delete repository with ID {RepositoryId}: {Message}", id, ex.Message);
            return Conflict(new { error = ex.Message, 
                                suggestedAction = "Please wait for active scans to complete or cancel them before deleting the repository." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting repository with ID {RepositoryId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the repository");
        }
    }

    /// <summary>
    /// Gets detailed information about a repository including scan and issue counts
    /// </summary>
    /// <param name="id">Repository ID</param>
    /// <returns>Detailed repository information</returns>
    [HttpGet("{id}/details")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepositoryDetails(Guid id)
    {
        try
        {
            var repository = await _repositoryService.GetRepositoryByIdAsync(id);
            if (repository == null)
            {
                return NotFound($"Repository with ID {id} not found");
            }

            // Get related data counts
            var hasActiveScans = await _repositoryService.HasActiveScansAsync(id);
            var totalScans = await _repositoryService.GetScanCountAsync(id);
            var totalIssues = await _repositoryService.GetIssueCountAsync(id);
            var totalConfigurations = await _repositoryService.GetConfigurationCountAsync(id);
            
            var details = new
            {
                Repository = repository,
                Summary = new
                {
                    HasActiveScans = hasActiveScans,
                    TotalScans = totalScans,
                    TotalIssues = totalIssues,
                    TotalConfigurations = totalConfigurations
                },
                LastActivity = repository.LastScanAt,
                CanDelete = !hasActiveScans,
                DeletionInfo = hasActiveScans 
                    ? "Repository cannot be deleted while scans are in progress"
                    : $"Deleting this repository will remove {totalScans} scans, {totalIssues} issues, and {totalConfigurations} configurations"
            };

            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting details for repository with ID {RepositoryId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving repository details");
        }
    }

    /// <summary>
    /// Gets the file structure of a repository
    /// </summary>
    /// <param name="id">Repository ID</param>
    /// <returns>Repository file structure</returns>
    [HttpGet("{id}/structure")]
    [ProducesResponseType(typeof(RepositoryFile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepositoryStructure(Guid id)
    {
        try
        {
            var repository = await _repositoryService.GetRepositoryByIdAsync(id);
            if (repository == null)
            {
                return NotFound($"Repository with ID {id} not found");
            }

            var structure = await _repositoryService.GetRepositoryStructureAsync(id);
            return Ok(structure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting structure for repository with ID {RepositoryId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the repository structure");
        }
    }

    /// <summary>
    /// Gets the content of a file in a repository
    /// </summary>
    /// <param name="id">Repository ID</param>
    /// <param name="filePath">Path to the file</param>
    /// <returns>File content</returns>
    [HttpGet("{id}/files")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFileContent(Guid id, [FromQuery] string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BadRequest("File path cannot be empty");
            }

            var repository = await _repositoryService.GetRepositoryByIdAsync(id);
            if (repository == null)
            {
                return NotFound($"Repository with ID {id} not found");
            }

            var content = await _repositoryService.GetFileContentAsync(id, filePath);
            return Ok(content);
        }
        catch (FileNotFoundException)
        {
            return NotFound($"File not found: {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file content for {FilePath} in repository {RepositoryId}", filePath, id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the file content");
        }
    }

    /// <summary>
    /// Validates if a repository exists at the given location
    /// </summary>
    /// <param name="location">Repository path or URL</param>
    /// <param name="type">Repository type (Local or GitHub)</param>
    /// <returns>Validation result</returns>
    [HttpGet("validate")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateRepository([FromQuery] string location, [FromQuery] RepositoryType type)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return BadRequest("Repository location cannot be empty");
            }

            var isValid = await _repositoryService.ValidateRepositoryAsync(location, type);
            return Ok(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating repository at {Location}", location);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while validating the repository");
        }
    }
}
