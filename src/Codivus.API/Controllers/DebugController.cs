using Codivus.API.Data;
using Microsoft.AspNetCore.Mvc;

namespace Codivus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly JsonDataStore _dataStore;
    private readonly ILogger<DebugController> _logger;

    public DebugController(JsonDataStore dataStore, ILogger<DebugController> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }

    /// <summary>
    /// Gets information about all repositories for debugging
    /// </summary>
    [HttpGet("repositories")]
    public async Task<IActionResult> GetRepositoriesDebugInfo()
    {
        try
        {
            var repositories = await _dataStore.GetRepositoriesAsync();
            var debugInfo = repositories.Select(r => new
            {
                Id = r.Id,
                Name = r.Name,
                Location = r.Location,
                Type = r.Type,
                AddedAt = r.AddedAt
            }).ToList();

            return Ok(new
            {
                Count = debugInfo.Count,
                Repositories = debugInfo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting debug info for repositories");
            return StatusCode(500, "Error getting repository debug info");
        }
    }

    /// <summary>
    /// Gets debug information about a specific repository
    /// </summary>
    [HttpGet("repositories/{id}")]
    public async Task<IActionResult> GetRepositoryDebugInfo(Guid id)
    {
        try
        {
            _logger.LogInformation("Debug: Getting repository with ID: {RepositoryId}", id);
            var repository = await _dataStore.GetRepositoryAsync(id);
            
            if (repository == null)
            {
                var allRepos = await _dataStore.GetRepositoriesAsync();
                return NotFound(new
                {
                    Message = $"Repository with ID {id} not found",
                    RequestedId = id,
                    AvailableRepositories = allRepos.Select(r => new { r.Id, r.Name }).ToList()
                });
            }

            return Ok(new
            {
                Found = true,
                Repository = new
                {
                    Id = repository.Id,
                    Name = repository.Name,
                    Location = repository.Location,
                    Type = repository.Type,
                    AddedAt = repository.AddedAt,
                    LastScanAt = repository.LastScanAt,
                    DefaultBranch = repository.DefaultBranch,
                    Description = repository.Description
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting debug info for repository {RepositoryId}", id);
            return StatusCode(500, $"Error getting repository debug info: {ex.Message}");
        }
    }
}
