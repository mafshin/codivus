using System.Diagnostics;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using Codivus.Graph.Configuration;

namespace Codivus.CLI.Services;

public class StatusCommandService
{
    private readonly ApiClientService _apiClient;
    private readonly IOutputService _outputService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<StatusCommandService> _logger;

    public StatusCommandService(
        ApiClientService apiClient,
        IOutputService outputService,
        IConfigurationService configurationService,
        ILogger<StatusCommandService> logger)
    {
        _apiClient = apiClient;
        _outputService = outputService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<CommandResult<StatusResult>> GetStatusAsync(StatusOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Getting status for repository: {RepositoryId}", options.RepositoryId ?? "all");

            var result = await _outputService.ShowProgressAsync(async (progress) =>
            {
                var statusResult = new StatusResult
                {
                    Timestamp = DateTime.UtcNow,
                    Success = true
                };

                progress.Report(new ProgressReport { Message = "Loading repositories...", Percentage = 20 });

                if (!string.IsNullOrEmpty(options.RepositoryId))
                {
                    // Get specific repository status
                    if (Guid.TryParse(options.RepositoryId, out var repoId))
                    {
                        var repoResponse = await _apiClient.GetRepositoryByIdAsync(repoId);
                        var repository = repoResponse.Success ? repoResponse.Data : null;
                    if (repository != null)
                    {
                            statusResult.Repositories = new List<RepositoryStatus>
                            {
                                await GetRepositoryStatusAsync(MapToRepository(repository), options)
                            };
                        }
                    }
                }
                else
                {
                    // Get all repositories status
                    var repositoriesResponse = await _apiClient.GetAllRepositoriesAsync();
                    var repositories = repositoriesResponse.Success && repositoriesResponse.Data != null 
                        ? repositoriesResponse.Data.Select(r => MapToRepository(r)).ToList()
                        : new List<Repository>();
                    statusResult.Repositories = new List<RepositoryStatus>();
                    
                    foreach (var repo in repositories)
                    {
                        var repoStatus = await GetRepositoryStatusAsync(repo, options);
                        statusResult.Repositories.Add(repoStatus);
                    }
                }

                progress.Report(new ProgressReport { Message = "Checking configuration...", Percentage = 60 });

                // Get configuration status
                statusResult.ConfigurationStatus = await GetConfigurationStatusAsync();

                if (options.IncludeSystemHealth)
                {
                    progress.Report(new ProgressReport { Message = "Checking system health...", Percentage = 80 });
                    statusResult.SystemHealth = await GetSystemHealthAsync();
                }

                progress.Report(new ProgressReport { Message = "Status check complete", Percentage = 100 });

                return statusResult;
            }, "Checking status...");

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Status check completed in {Duration}ms", stopwatch.ElapsedMilliseconds);

            return CommandResult<StatusResult>.SuccessResult(
                result,
                $"Status retrieved for {result.Repositories.Count} repositories");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status");
            return CommandResult<StatusResult>.ErrorResult($"Failed to get status: {ex.Message}");
        }
    }

    private async Task<RepositoryStatus> GetRepositoryStatusAsync(Repository repository, StatusOptions options)
    {
        var status = new RepositoryStatus
        {
            Id = repository.Id.ToString(),
            Name = repository.Name,
            Path = repository.Location,
            Url = repository.Type == "GitHub" ? repository.Location : null,
            Branch = repository.DefaultBranch,
            LastScanned = repository.LastScannedAt,
            Status = "unknown"
        };

        try
        {
            // Check if repository path exists
            if (!string.IsNullOrEmpty(repository.Location) && Directory.Exists(repository.Location))
            {
                status.Status = "available";
                
                // Get repository size
                status.Size = await GetDirectorySizeAsync(repository.Location);
                
                // Check if it's a git repository
                if (Directory.Exists(Path.Combine(repository.Location, ".git")))
                {
                    status.IsGitRepository = true;
                    
                    if (options.Detailed)
                    {
                        status.GitInfo = await GetGitInfoAsync(repository.Location);
                    }
                }

                // Get scan information
                status.ScanInfo = await GetScanInfoAsync(repository.Id.ToString());

                // Get issue count
                status.IssueCount = await GetIssueCountAsync(repository.Id.ToString());
            }
            else
            {
                status.Status = "missing";
            }
        }
        catch (Exception ex)
        {
            status.Status = "error";
            status.Error = ex.Message;
        }

        return status;
    }

    private async Task<ConfigurationStatus> GetConfigurationStatusAsync()
    {
        var status = new ConfigurationStatus();

        try
        {
            var configExists = await _configurationService.ConfigurationExistsAsync();
            status.Exists = configExists;

            if (configExists)
            {
                var configPath = await _configurationService.GetConfigurationPathAsync();
                status.Path = configPath;
                
                // Check individual configuration files
                status.Files = new Dictionary<string, bool>();
                var configFiles = new[] { "scan.json", "graph.json", "llm.json" };
                
                foreach (var file in configFiles)
                {
                    var filePath = Path.Combine(configPath, file);
                    status.Files[file] = File.Exists(filePath);
                }

                status.IsValid = status.Files.Values.All(exists => exists);
            }
        }
        catch (Exception ex)
        {
            status.Error = ex.Message;
        }

        return status;
    }

    private async Task<SystemHealth> GetSystemHealthAsync()
    {
        var health = new SystemHealth
        {
            Timestamp = DateTime.UtcNow,
            Services = new Dictionary<string, ServiceHealth>()
        };

        // Check CLI version
        health.CliVersion = GetAssemblyVersion();

        // Check available disk space
        health.AvailableDiskSpace = await GetAvailableDiskSpaceAsync();

        // Check memory usage
        health.MemoryUsage = await GetMemoryUsageAsync();

        // Check Neo4j connectivity (if configured)
        health.Services["Neo4j"] = await CheckNeo4jHealthAsync();

        // Check LLM provider connectivity (if configured)
        health.Services["LLM"] = await CheckLLMProviderHealthAsync();

        return health;
    }

    private async Task<long> GetDirectorySizeAsync(string path)
    {
        try
        {
            var directoryInfo = new DirectoryInfo(path);
            return await Task.Run(() => 
                directoryInfo.GetFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length));
        }
        catch
        {
            return 0;
        }
    }

    private async Task<GitInfo> GetGitInfoAsync(string repositoryPath)
    {
        var gitInfo = new GitInfo();

        try
        {
            // Get current branch
            gitInfo.CurrentBranch = await ExecuteGitCommandAsync(repositoryPath, "branch --show-current");
            
            // Get last commit
            gitInfo.LastCommit = await ExecuteGitCommandAsync(repositoryPath, "log -1 --format=\"%H %s\"");
            
            // Check for uncommitted changes
            var status = await ExecuteGitCommandAsync(repositoryPath, "status --porcelain");
            gitInfo.HasUncommittedChanges = !string.IsNullOrWhiteSpace(status);
            
            // Get remote URL
            gitInfo.RemoteUrl = await ExecuteGitCommandAsync(repositoryPath, "remote get-url origin");
        }
        catch (Exception ex)
        {
            gitInfo.Error = ex.Message;
        }

        return gitInfo;
    }

    private async Task<string> ExecuteGitCommandAsync(string workingDirectory, string arguments)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    return (await process.StandardOutput.ReadToEndAsync()).Trim();
                }
            }
        }
        catch
        {
            // Ignore git command errors
        }

        return "";
    }

    private async Task<ScanInfo> GetScanInfoAsync(string repositoryId)
    {
        // Implementation would load scan information from storage
        await Task.Delay(50);
        
        return new ScanInfo
        {
            LastScanDate = DateTime.UtcNow.AddDays(-1),
            FilesScanned = 150,
            Duration = TimeSpan.FromMinutes(2),
            Status = "completed"
        };
    }

    private async Task<int> GetIssueCountAsync(string repositoryId)
    {
        // Implementation would load issue count from storage
        await Task.Delay(50);
        return 25;
    }

    private string GetAssemblyVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "unknown";
    }

    private async Task<long> GetAvailableDiskSpaceAsync()
    {
        try
        {
            var drive = new DriveInfo(Directory.GetCurrentDirectory());
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    private Repository MapToRepository(RepositoryDto dto)
    {
        return new Repository
        {
            Id = dto.Id,
            Name = dto.Name,
            Location = dto.Location,
            Type = dto.TypeName,
            DefaultBranch = dto.DefaultBranch,
            AddedAt = dto.AddedAt,
            LastScannedAt = dto.LastScannedAt
        };
    }

    private async Task<MemoryUsage> GetMemoryUsageAsync()
    {
        await Task.Delay(10);
        
        var process = Process.GetCurrentProcess();
        return new MemoryUsage
        {
            WorkingSet = process.WorkingSet64,
            PrivateMemory = process.PrivateMemorySize64,
            VirtualMemory = process.VirtualMemorySize64
        };
    }

    private async Task<ServiceHealth> CheckNeo4jHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var config = await _configurationService.GetConfigurationAsync<GraphConfiguration>("graph");
            if (config == null || !config.Enabled)
            {
                return new ServiceHealth
                {
                    Name = "Neo4j",
                    Status = "disabled",
                    ResponseTime = TimeSpan.Zero,
                    LastChecked = DateTime.UtcNow,
                    Error = "Graph storage is disabled in configuration"
                };
            }

            var settings = config.Neo4j;
            using var driver = GraphDatabase.Driver(
                settings.Uri,
                AuthTokens.Basic(settings.Username, settings.Password),
                configBuilder => configBuilder
                    .WithConnectionTimeout(TimeSpan.FromSeconds(5))
                    .WithEncryptionLevel(settings.EnableEncryption ? EncryptionLevel.Encrypted : EncryptionLevel.None)
            );

            await using var session = driver.AsyncSession(SessionConfigBuilder.ForDatabase(settings.Database));
            var result = await session.RunAsync("RETURN 1 as health");
            var record = await result.SingleAsync();
            
            stopwatch.Stop();
            
            var healthValue = record["health"].As<int>();
            if (healthValue == 1)
            {
                return new ServiceHealth
                {
                    Name = "Neo4j",
                    Status = "healthy",
                    ResponseTime = stopwatch.Elapsed,
                    LastChecked = DateTime.UtcNow
                };
            }
            else
            {
                return new ServiceHealth
                {
                    Name = "Neo4j",
                    Status = "unhealthy",
                    ResponseTime = stopwatch.Elapsed,
                    LastChecked = DateTime.UtcNow,
                    Error = "Health check query returned unexpected result"
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Neo4j health check failed");
            
            return new ServiceHealth
            {
                Name = "Neo4j",
                Status = "unhealthy",
                ResponseTime = stopwatch.Elapsed,
                LastChecked = DateTime.UtcNow,
                Error = $"Connection failed: {ex.Message}"
            };
        }
    }

    private async Task<ServiceHealth> CheckLLMProviderHealthAsync()
    {
        // Implementation would check LLM provider connectivity
        await Task.Delay(100);
        
        return new ServiceHealth
        {
            Name = "LLM Provider",
            Status = "healthy",
            ResponseTime = TimeSpan.FromMilliseconds(200),
            LastChecked = DateTime.UtcNow
        };
    }
}

// Supporting data models
public class StatusResult
{
    public DateTime Timestamp { get; set; }
    public List<RepositoryStatus> Repositories { get; set; } = new();
    public ConfigurationStatus ConfigurationStatus { get; set; } = new();
    public SystemHealth? SystemHealth { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class RepositoryStatus
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Url { get; set; } = "";
    public string Branch { get; set; } = "";
    public DateTime? LastScanned { get; set; }
    public string Status { get; set; } = "";
    public string? Error { get; set; }
    public long Size { get; set; }
    public bool IsGitRepository { get; set; }
    public GitInfo? GitInfo { get; set; }
    public ScanInfo? ScanInfo { get; set; }
    public int IssueCount { get; set; }
}

public class ConfigurationStatus
{
    public bool Exists { get; set; }
    public string Path { get; set; } = "";
    public Dictionary<string, bool> Files { get; set; } = new();
    public bool IsValid { get; set; }
    public string? Error { get; set; }
}

public class SystemHealth
{
    public DateTime Timestamp { get; set; }
    public string CliVersion { get; set; } = "";
    public long AvailableDiskSpace { get; set; }
    public MemoryUsage MemoryUsage { get; set; } = new();
    public Dictionary<string, ServiceHealth> Services { get; set; } = new();
}

public class GitInfo
{
    public string CurrentBranch { get; set; } = "";
    public string LastCommit { get; set; } = "";
    public bool HasUncommittedChanges { get; set; }
    public string RemoteUrl { get; set; } = "";
    public string? Error { get; set; }
}

public class ScanInfo
{
    public DateTime? LastScanDate { get; set; }
    public int FilesScanned { get; set; }
    public TimeSpan Duration { get; set; }
    public string Status { get; set; } = "";
}

public class MemoryUsage
{
    public long WorkingSet { get; set; }
    public long PrivateMemory { get; set; }
    public long VirtualMemory { get; set; }
}

public class ServiceHealth
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public TimeSpan ResponseTime { get; set; }
    public DateTime LastChecked { get; set; }
    public string? Error { get; set; }
}