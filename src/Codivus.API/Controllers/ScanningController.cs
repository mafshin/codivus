using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Codivus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScanningController : ControllerBase
{
    private readonly IScanningService _scanningService;
    private readonly IRepositoryService _repositoryService;
    private readonly ILogger<ScanningController> _logger;

    public ScanningController(
        IScanningService scanningService,
        IRepositoryService repositoryService,
        ILogger<ScanningController> logger)
    {
        _scanningService = scanningService;
        _repositoryService = repositoryService;
        _logger = logger;
    }

    /// <summary>
    /// Starts a new scan for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID to scan</param>
    /// <param name="configuration">Scan configuration</param>
    /// <returns>Scan progress object</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ScanProgress), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartScan(Guid repositoryId, [FromBody] ScanConfiguration configuration)
    {
        try
        {
            if (configuration == null)
            {
                return BadRequest("Scan configuration cannot be null");
            }

            var repository = await _repositoryService.GetRepositoryByIdAsync(repositoryId);
            if (repository == null)
            {
                return NotFound($"Repository with ID {repositoryId} not found");
            }

            // Ensure the configuration is associated with the repository
            configuration.RepositoryId = repositoryId;

            var scanProgress = await _scanningService.StartScanAsync(repositoryId, configuration);
            return Ok(scanProgress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting scan for repository {RepositoryId}", repositoryId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while starting the scan");
        }
    }

    /// <summary>
    /// Gets the current scan progress
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Current scan progress</returns>
    [HttpGet("{scanId}/progress")]
    [ProducesResponseType(typeof(ScanProgress), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScanProgress(Guid scanId)
    {
        return await GetScanById(scanId);
    }

    /// <summary>
    /// Gets a scan by ID
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Scan details</returns>
    [HttpGet("scan/{scanId}")]
    [ProducesResponseType(typeof(ScanProgress), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScanById(Guid scanId)
    {
        try
        {
            _logger.LogInformation("Getting scan details for ID: {ScanId}", scanId);
            var scanProgress = await _scanningService.GetScanProgressAsync(scanId);
            
            if (scanProgress == null)
            {
                _logger.LogWarning("Scan with ID {ScanId} not found", scanId);
                return NotFound($"Scan with ID {scanId} not found");
            }

            _logger.LogInformation("Successfully retrieved scan {ScanId}, Status: {Status}", scanId, scanProgress.Status);
            return Ok(scanProgress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan progress for scan {ScanId}", scanId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving scan progress");
        }
    }

    /// <summary>
    /// Gets all scans for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Collection of scan progress objects</returns>
    [HttpGet("repository/{repositoryId}")]
    [ProducesResponseType(typeof(IEnumerable<ScanProgress>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScansByRepository(Guid repositoryId)
    {
        try
        {
            var repository = await _repositoryService.GetRepositoryByIdAsync(repositoryId);
            if (repository == null)
            {
                return NotFound($"Repository with ID {repositoryId} not found");
            }

            var scans = await _scanningService.GetScansByRepositoryAsync(repositoryId);
            return Ok(scans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scans for repository {RepositoryId}", repositoryId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving scans");
        }
    }

    /// <summary>
    /// Pauses an in-progress scan
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{scanId}/pause")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PauseScan(Guid scanId)
    {
        try
        {
            var scanProgress = await _scanningService.GetScanProgressAsync(scanId);
            if (scanProgress == null)
            {
                return NotFound($"Scan with ID {scanId} not found");
            }

            if (scanProgress.Status != ScanStatus.InProgress)
            {
                return BadRequest($"Cannot pause scan with status {scanProgress.Status}");
            }

            var result = await _scanningService.PauseScanAsync(scanId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing scan {ScanId}", scanId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while pausing the scan");
        }
    }

    /// <summary>
    /// Resumes a paused scan
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{scanId}/resume")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResumeScan(Guid scanId)
    {
        try
        {
            var scanProgress = await _scanningService.GetScanProgressAsync(scanId);
            if (scanProgress == null)
            {
                return NotFound($"Scan with ID {scanId} not found");
            }

            if (scanProgress.Status != ScanStatus.Paused)
            {
                return BadRequest($"Cannot resume scan with status {scanProgress.Status}");
            }

            var result = await _scanningService.ResumeScanAsync(scanId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming scan {ScanId}", scanId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while resuming the scan");
        }
    }

    /// <summary>
    /// Cancels an in-progress or paused scan
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{scanId}/cancel")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelScan(Guid scanId)
    {
        try
        {
            var scanProgress = await _scanningService.GetScanProgressAsync(scanId);
            if (scanProgress == null)
            {
                return NotFound($"Scan with ID {scanId} not found");
            }

            if (scanProgress.Status != ScanStatus.InProgress && scanProgress.Status != ScanStatus.Paused)
            {
                return BadRequest($"Cannot cancel scan with status {scanProgress.Status}");
            }

            var result = await _scanningService.CancelScanAsync(scanId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling scan {ScanId}", scanId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while canceling the scan");
        }
    }

    /// <summary>
    /// Deletes a scan and all its related issues
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{scanId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteScan(Guid scanId)
    {
        try
        {
            var scanProgress = await _scanningService.GetScanProgressAsync(scanId);
            if (scanProgress == null)
            {
                return NotFound($"Scan with ID {scanId} not found");
            }

            // Prevent deletion of active scans
            if (scanProgress.Status == ScanStatus.InProgress || scanProgress.Status == ScanStatus.Initializing)
            {
                return BadRequest($"Cannot delete scan with status {scanProgress.Status}. Please cancel the scan first.");
            }

            var result = await _scanningService.DeleteScanAsync(scanId);
            if (!result)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete the scan");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting scan {ScanId}", scanId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the scan");
        }
    }

    /// <summary>
    /// Gets issues found in a scan
    /// </summary>
    /// <param name="scanId">Scan ID</param>
    /// <returns>Collection of code issues</returns>
    [HttpGet("{scanId}/issues")]
    [ProducesResponseType(typeof(IEnumerable<CodeIssue>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScanIssues(Guid scanId)
    {
        try
        {
            var scanProgress = await _scanningService.GetScanProgressAsync(scanId);
            if (scanProgress == null)
            {
                return NotFound($"Scan with ID {scanId} not found");
            }

            var issues = await _scanningService.GetScanIssuesAsync(scanId);
            return Ok(issues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issues for scan {ScanId}", scanId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving scan issues");
        }
    }

    /// <summary>
    /// Gets scan configurations for a repository
    /// </summary>
    /// <param name="repositoryId">Repository ID</param>
    /// <returns>Collection of scan configurations</returns>
    [HttpGet("configurations/{repositoryId}")]
    [ProducesResponseType(typeof(IEnumerable<ScanConfiguration>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScanConfigurations(Guid repositoryId)
    {
        try
        {
            var repository = await _repositoryService.GetRepositoryByIdAsync(repositoryId);
            if (repository == null)
            {
                return NotFound($"Repository with ID {repositoryId} not found");
            }

            var configurations = await _scanningService.GetScanConfigurationsAsync(repositoryId);
            return Ok(configurations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan configurations for repository {RepositoryId}", repositoryId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving scan configurations");
        }
    }

    /// <summary>
    /// Gets a scan configuration by ID
    /// </summary>
    /// <param name="configurationId">Configuration ID</param>
    /// <returns>Scan configuration if found</returns>
    [HttpGet("configuration/{configurationId}")]
    [ProducesResponseType(typeof(ScanConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScanConfigurationById(Guid configurationId)
    {
        try
        {
            var configuration = await _scanningService.GetScanConfigurationByIdAsync(configurationId);
            if (configuration == null)
            {
                return NotFound($"Scan configuration with ID {configurationId} not found");
            }

            return Ok(configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan configuration with ID {ConfigurationId}", configurationId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the scan configuration");
        }
    }

    /// <summary>
    /// Creates a new scan configuration
    /// </summary>
    /// <param name="configuration">Scan configuration to create</param>
    /// <returns>Created scan configuration</returns>
    [HttpPost("configuration")]
    [ProducesResponseType(typeof(ScanConfiguration), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateScanConfiguration(ScanConfiguration configuration)
    {
        try
        {
            if (configuration == null)
            {
                return BadRequest("Scan configuration cannot be null");
            }

            var repository = await _repositoryService.GetRepositoryByIdAsync(configuration.RepositoryId);
            if (repository == null)
            {
                return NotFound($"Repository with ID {configuration.RepositoryId} not found");
            }

            var createdConfiguration = await _scanningService.CreateScanConfigurationAsync(configuration);
            return CreatedAtAction(
                nameof(GetScanConfigurationById),
                new { configurationId = createdConfiguration.Id },
                createdConfiguration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating scan configuration");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the scan configuration");
        }
    }

    /// <summary>
    /// Updates an existing scan configuration
    /// </summary>
    /// <param name="configurationId">Configuration ID</param>
    /// <param name="configuration">Updated scan configuration data</param>
    /// <returns>Updated scan configuration</returns>
    [HttpPut("configuration/{configurationId}")]
    [ProducesResponseType(typeof(ScanConfiguration), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateScanConfiguration(Guid configurationId, ScanConfiguration configuration)
    {
        try
        {
            if (configuration == null || configurationId != configuration.Id)
            {
                return BadRequest("Invalid scan configuration data or ID mismatch");
            }

            var existingConfiguration = await _scanningService.GetScanConfigurationByIdAsync(configurationId);
            if (existingConfiguration == null)
            {
                return NotFound($"Scan configuration with ID {configurationId} not found");
            }

            var updatedConfiguration = await _scanningService.UpdateScanConfigurationAsync(configuration);
            return Ok(updatedConfiguration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scan configuration with ID {ConfigurationId}", configurationId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the scan configuration");
        }
    }

    /// <summary>
    /// Deletes a scan configuration
    /// </summary>
    /// <param name="configurationId">Configuration ID to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("configuration/{configurationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteScanConfiguration(Guid configurationId)
    {
        try
        {
            var configuration = await _scanningService.GetScanConfigurationByIdAsync(configurationId);
            if (configuration == null)
            {
                return NotFound($"Scan configuration with ID {configurationId} not found");
            }

            var result = await _scanningService.DeleteScanConfigurationAsync(configurationId);
            if (!result)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete the scan configuration");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting scan configuration with ID {ConfigurationId}", configurationId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the scan configuration");
        }
    }
}
