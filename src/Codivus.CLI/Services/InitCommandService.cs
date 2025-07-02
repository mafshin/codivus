using System.Diagnostics;
using System.Text.Json;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class InitCommandService
{
    private readonly IConfigurationService _configurationService;
    private readonly IOutputService _outputService;
    private readonly IValidationService _validationService;
    private readonly ILogger<InitCommandService> _logger;

    public InitCommandService(
        IConfigurationService configurationService,
        IOutputService outputService,
        IValidationService validationService,
        ILogger<InitCommandService> logger)
    {
        _configurationService = configurationService;
        _outputService = outputService;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<CommandResult<InitResult>> InitializeProjectAsync(InitOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Initializing project at path: {Path}", options.Path);

            // Validate path
            var pathValidation = _validationService.ValidatePath(options.Path, false);
            if (!pathValidation.IsValid)
            {
                return CommandResult<InitResult>.ErrorResult(string.Join(", ", pathValidation.Errors));
            }

            var result = await _outputService.ShowProgressAsync(async (progress) =>
            {
                progress.Report(new ProgressReport { Message = "Preparing workspace...", Percentage = 10 });

                // Ensure directory exists
                if (!Directory.Exists(options.Path))
                {
                    Directory.CreateDirectory(options.Path);
                }

                var initResult = new InitResult
                {
                    ProjectPath = options.Path,
                    ProjectName = options.Name,
                    Template = options.Template,
                    FilesCreated = new List<string>(),
                    Success = true
                };

                progress.Report(new ProgressReport { Message = "Creating project configuration...", Percentage = 25 });

                // Create .codivus directory
                var codivusDir = Path.Combine(options.Path, ".codivus");
                if (!Directory.Exists(codivusDir) || options.Force)
                {
                    if (Directory.Exists(codivusDir) && options.Force)
                    {
                        Directory.Delete(codivusDir, true);
                    }
                    Directory.CreateDirectory(codivusDir);
                }

                progress.Report(new ProgressReport { Message = "Setting up configuration files...", Percentage = 40 });

                // Create project configuration
                await CreateProjectConfigurationAsync(codivusDir, options);
                initResult.FilesCreated.Add(".codivus/project.json");

                progress.Report(new ProgressReport { Message = "Creating scanning configuration...", Percentage = 55 });

                // Create scanning configuration
                await CreateScanConfigurationAsync(codivusDir, options);
                initResult.FilesCreated.Add(".codivus/scan.json");

                progress.Report(new ProgressReport { Message = "Setting up ignore patterns...", Percentage = 70 });

                // Create .codivusignore file
                await CreateIgnoreFileAsync(options.Path, options);
                initResult.FilesCreated.Add(".codivusignore");

                if (options.InitializeGit)
                {
                    progress.Report(new ProgressReport { Message = "Initializing git repository...", Percentage = 85 });
                    
                    if (!Directory.Exists(Path.Combine(options.Path, ".git")))
                    {
                        await InitializeGitRepositoryAsync(options.Path);
                        initResult.GitInitialized = true;
                    }
                }

                progress.Report(new ProgressReport { Message = "Creating documentation...", Percentage = 95 });

                // Create README if it doesn't exist
                var readmePath = Path.Combine(options.Path, "README.md");
                if (!File.Exists(readmePath))
                {
                    await CreateReadmeFileAsync(readmePath, options);
                    initResult.FilesCreated.Add("README.md");
                }

                progress.Report(new ProgressReport { Message = "Initialization complete", Percentage = 100 });

                return initResult;
            }, "Initializing project...");

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Project initialized in {Duration}ms", stopwatch.ElapsedMilliseconds);

            return CommandResult<InitResult>.SuccessResult(
                result,
                $"Project '{options.Name}' initialized successfully at {options.Path}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing project");
            return CommandResult<InitResult>.ErrorResult($"Failed to initialize project: {ex.Message}");
        }
    }

    private async Task CreateProjectConfigurationAsync(string codivusDir, InitOptions options)
    {
        var projectConfig = new
        {
            name = options.Name,
            version = "1.0.0",
            template = options.Template,
            created = DateTime.UtcNow,
            settings = new
            {
                defaultBranch = "main",
                enableGraph = true,
                enableLLM = true,
                autoScan = false
            }
        };

        var configPath = Path.Combine(codivusDir, "project.json");
        var json = JsonSerializer.Serialize(projectConfig, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json);
    }

    private async Task CreateScanConfigurationAsync(string codivusDir, InitOptions options)
    {
        var scanConfig = GetScanConfigurationTemplate(options.Template);
        var configPath = Path.Combine(codivusDir, "scan.json");
        var json = JsonSerializer.Serialize(scanConfig, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json);
    }

    private object GetScanConfigurationTemplate(string template)
    {
        return template.ToLowerInvariant() switch
        {
            "enterprise" => new
            {
                filePatterns = new[] { "*.cs", "*.js", "*.ts", "*.py", "*.java", "*.cpp", "*.h", "*.hpp", "*.go", "*.rs" },
                excludePatterns = new[] { "bin/**", "obj/**", "node_modules/**", ".git/**", "*.min.js", "dist/**", "build/**", "target/**" },
                maxFileSize = 10485760, // 10MB
                enableParallelProcessing = true,
                maxConcurrency = 8,
                scanDepth = "deep",
                enableSecurityScanning = true,
                enablePerformanceAnalysis = true,
                enableArchitectureAnalysis = true,
                customRules = new string[0]
            },
            "advanced" => new
            {
                filePatterns = new[] { "*.cs", "*.js", "*.ts", "*.py", "*.java", "*.cpp", "*.h", "*.go" },
                excludePatterns = new[] { "bin/**", "obj/**", "node_modules/**", ".git/**", "*.min.js", "dist/**", "build/**" },
                maxFileSize = 5242880, // 5MB
                enableParallelProcessing = true,
                maxConcurrency = 4,
                scanDepth = "medium",
                enableSecurityScanning = true,
                enablePerformanceAnalysis = false,
                enableArchitectureAnalysis = true
            },
            _ => new // basic
            {
                filePatterns = new[] { "*.cs", "*.js", "*.ts", "*.py", "*.java" },
                excludePatterns = new[] { "bin/**", "obj/**", "node_modules/**", ".git/**", "*.min.js" },
                maxFileSize = 1048576, // 1MB
                enableParallelProcessing = true,
                maxConcurrency = 2,
                scanDepth = "basic",
                enableSecurityScanning = true,
                enablePerformanceAnalysis = false,
                enableArchitectureAnalysis = false
            }
        };
    }

    private async Task CreateIgnoreFileAsync(string projectPath, InitOptions options)
    {
        var ignorePatterns = GetIgnorePatterns(options.Template);
        var ignorePath = Path.Combine(projectPath, ".codivusignore");
        await File.WriteAllTextAsync(ignorePath, string.Join(Environment.NewLine, ignorePatterns));
    }

    private string[] GetIgnorePatterns(string template)
    {
        var basicPatterns = new[]
        {
            "# Build outputs",
            "bin/",
            "obj/",
            "build/",
            "dist/",
            "target/",
            "",
            "# Dependencies",
            "node_modules/",
            "packages/",
            "vendor/",
            "",
            "# IDE files",
            ".vs/",
            ".vscode/",
            "*.suo",
            "*.user",
            ".idea/",
            "",
            "# Logs",
            "*.log",
            "logs/",
            "",
            "# Temporary files",
            "*.tmp",
            "*.temp",
            ".cache/",
            "",
            "# Version control",
            ".git/",
            ".svn/",
            "",
            "# Codivus files",
            ".codivus/cache/",
            ".codivus/temp/"
        };

        if (template == "enterprise")
        {
            return basicPatterns.Concat(new[]
            {
                "",
                "# Additional enterprise patterns",
                "coverage/",
                "reports/",
                "docs/generated/",
                "*.coverage",
                "TestResults/",
                ".nyc_output/",
                "playwright-report/",
                "test-results/"
            }).ToArray();
        }

        return basicPatterns;
    }

    private async Task InitializeGitRepositoryAsync(string path)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "init",
                WorkingDirectory = path,
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
                    // Create initial .gitignore if it doesn't exist
                    var gitignorePath = Path.Combine(path, ".gitignore");
                    if (!File.Exists(gitignorePath))
                    {
                        await CreateGitIgnoreFileAsync(gitignorePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize git repository");
        }
    }

    private async Task CreateGitIgnoreFileAsync(string gitignorePath)
    {
        var gitignoreContent = new[]
        {
            "# Codivus cache and temporary files",
            ".codivus/cache/",
            ".codivus/temp/",
            ".codivus/reports/",
            "",
            "# Build outputs",
            "bin/",
            "obj/",
            "build/",
            "dist/",
            "",
            "# Dependencies",
            "node_modules/",
            "packages/",
            "",
            "# IDE files",
            ".vs/",
            ".vscode/",
            "*.suo",
            "*.user",
            ".idea/",
            "",
            "# Logs",
            "*.log",
            "logs/"
        };

        await File.WriteAllTextAsync(gitignorePath, string.Join(Environment.NewLine, gitignoreContent));
    }

    private async Task CreateReadmeFileAsync(string readmePath, InitOptions options)
    {
        var readmeContent = $@"# {options.Name}

This project has been initialized with Codivus for AI-powered code analysis.

## Getting Started

### Scanning Your Code

```bash
# Scan the entire repository
codivus scan repo --path .

# Scan specific files
codivus scan file src/main.cs

# Scan with graph analysis
codivus scan repo --path . --enable-graph
```

### Graph Analysis

```bash
# View graph metrics
codivus graph metrics --repository your-repo-id

# Generate graph visualization
codivus graph visualize --repository your-repo-id --output graph.svg

# Analyze code complexity
codivus graph analyze --repository your-repo-id --type complexity
```

### Managing Issues

```bash
# List all issues
codivus issues list

# Show specific issue details
codivus issues show <issue-id>

# Export issues to file
codivus issues export --output issues.json
```

### Configuration

```bash
# Show current settings
codivus settings show

# Set configuration values
codivus settings set scan.maxFileSize 5242880

# Initialize configuration
codivus settings init
```

## Project Structure

- `.codivus/` - Codivus configuration and cache
- `.codivusignore` - Files and patterns to exclude from scanning

## Template: {options.Template}

This project uses the '{options.Template}' template configuration.

For more information, visit the [Codivus documentation](https://docs.codivus.com).
";

        await File.WriteAllTextAsync(readmePath, readmeContent);
    }
}

public class InitResult
{
    public string ProjectPath { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string Template { get; set; } = "";
    public List<string> FilesCreated { get; set; } = new();
    public bool GitInitialized { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}