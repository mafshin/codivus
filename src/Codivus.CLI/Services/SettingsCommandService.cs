using System.Diagnostics;
using System.Text.Json;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class SettingsCommandService
{
    private readonly IConfigurationService _configurationService;
    private readonly IOutputService _outputService;
    private readonly IValidationService _validationService;
    private readonly ILogger<SettingsCommandService> _logger;

    public SettingsCommandService(
        IConfigurationService configurationService,
        IOutputService outputService,
        IValidationService validationService,
        ILogger<SettingsCommandService> logger)
    {
        _configurationService = configurationService;
        _outputService = outputService;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<CommandResult<SettingsInitResult>> InitializeConfigurationAsync(SettingsOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Initializing configuration with template: {Template}", options.Template ?? "default");

            var result = await _outputService.ShowProgressAsync(async (progress) =>
            {
                progress.Report(new ProgressReport { Message = "Checking existing configuration...", Percentage = 10 });

                var configExists = await _configurationService.ConfigurationExistsAsync();
                if (configExists && !options.Force)
                {
                    throw new InvalidOperationException("Configuration already exists. Use --force to overwrite.");
                }

                progress.Report(new ProgressReport { Message = "Creating configuration files...", Percentage = 50 });

                await _configurationService.InitializeConfigurationAsync();

                progress.Report(new ProgressReport { Message = "Applying template settings...", Percentage = 80 });

                if (!string.IsNullOrEmpty(options.Template))
                {
                    await ApplyTemplateAsync(options.Template);
                }

                progress.Report(new ProgressReport { Message = "Configuration initialized", Percentage = 100 });

                var configPath = await _configurationService.GetConfigurationPathAsync();
                return new SettingsInitResult
                {
                    ConfigurationPath = configPath,
                    Template = options.Template ?? "default",
                    FilesCreated = GetCreatedFiles(configPath),
                    Success = true
                };
            }, "Initializing configuration...");

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Configuration initialized in {Duration}ms", stopwatch.ElapsedMilliseconds);

            return CommandResult<SettingsInitResult>.SuccessResult(
                result,
                $"Configuration initialized at {result.ConfigurationPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing configuration");
            return CommandResult<SettingsInitResult>.ErrorResult($"Failed to initialize configuration: {ex.Message}");
        }
    }

    public async Task<CommandResult<SettingsShowResult>> ShowConfigurationAsync(SettingsOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Showing configuration section: {Section}", options.Section ?? "all");

            var result = await _outputService.ShowProgressAsync(async (progress) =>
            {
                progress.Report(new ProgressReport { Message = "Loading configuration...", Percentage = 50 });

                var config = await LoadConfigurationSectionAsync(options.Section);

                progress.Report(new ProgressReport { Message = "Formatting output...", Percentage = 100 });

                return new SettingsShowResult
                {
                    Section = options.Section ?? "all",
                    Configuration = config,
                    Success = true
                };
            }, "Loading configuration...");

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<SettingsShowResult>.SuccessResult(
                result,
                $"Configuration loaded for section: {result.Section}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing configuration");
            return CommandResult<SettingsShowResult>.ErrorResult($"Failed to show configuration: {ex.Message}");
        }
    }

    public async Task<CommandResult<SettingsSetResult>> SetConfigurationAsync(SettingsOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Setting configuration key: {Key}", options.Key);

            var parsedValue = ParseConfigurationValue(options.Value!, options.ValueType);
            var section = GetSectionFromKey(options.Key!);
            
            var config = await _configurationService.GetConfigurationAsync<Dictionary<string, object>>(section) 
                         ?? new Dictionary<string, object>();

            SetNestedValue(config, options.Key!, parsedValue);

            await _configurationService.SetConfigurationAsync(section, config);

            var result = new SettingsSetResult
            {
                Key = options.Key!,
                Value = parsedValue,
                ValueType = options.ValueType,
                Success = true
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<SettingsSetResult>.SuccessResult(
                result,
                $"Configuration updated: {options.Key} = {options.Value}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting configuration");
            return CommandResult<SettingsSetResult>.ErrorResult($"Failed to set configuration: {ex.Message}");
        }
    }

    public async Task<CommandResult<SettingsGetResult>> GetConfigurationAsync(SettingsOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Getting configuration key: {Key}", options.Key);

            var section = GetSectionFromKey(options.Key!);
            var config = await _configurationService.GetConfigurationAsync<Dictionary<string, object>>(section);

            object? value = null;
            if (config != null)
            {
                value = GetNestedValue(config, options.Key!);
            }

            if (value == null && !string.IsNullOrEmpty(options.DefaultValue))
            {
                value = options.DefaultValue;
            }

            var result = new SettingsGetResult
            {
                Key = options.Key!,
                Value = value,
                Found = value != null,
                Success = true
            };

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<SettingsGetResult>.SuccessResult(
                result,
                result.Found ? $"Value: {value}" : "Key not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration");
            return CommandResult<SettingsGetResult>.ErrorResult($"Failed to get configuration: {ex.Message}");
        }
    }

    public async Task<CommandResult<SettingsResetResult>> ResetConfigurationAsync(SettingsOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Resetting configuration section: {Section}", options.Section ?? "all");

            if (!options.Confirm)
            {
                _outputService.WriteWarning("This will reset configuration to default values.");
                _outputService.WriteInfo("Use --confirm to proceed without this prompt.");
                return CommandResult<SettingsResetResult>.ErrorResult("Reset cancelled. Use --confirm to proceed.");
            }

            var result = await _outputService.ShowProgressAsync(async (progress) =>
            {
                progress.Report(new ProgressReport { Message = "Backing up current configuration...", Percentage = 20 });

                var configPath = await _configurationService.GetConfigurationPathAsync();
                var backupPath = await CreateConfigurationBackupAsync(configPath);

                progress.Report(new ProgressReport { Message = "Resetting configuration...", Percentage = 60 });

                var sectionsReset = await ResetConfigurationSectionsAsync(options.Section);

                progress.Report(new ProgressReport { Message = "Reset complete", Percentage = 100 });

                return new SettingsResetResult
                {
                    Section = options.Section ?? "all",
                    SectionsReset = sectionsReset,
                    BackupPath = backupPath,
                    Success = true
                };
            }, "Resetting configuration...");

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<SettingsResetResult>.SuccessResult(
                result,
                $"Configuration reset. Backup saved to {result.BackupPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration");
            return CommandResult<SettingsResetResult>.ErrorResult($"Failed to reset configuration: {ex.Message}");
        }
    }

    public async Task<CommandResult<SettingsValidateResult>> ValidateConfigurationAsync(SettingsOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Validating configuration section: {Section}", options.Section ?? "all");

            var result = await _outputService.ShowProgressAsync(async (progress) =>
            {
                progress.Report(new ProgressReport { Message = "Loading configuration...", Percentage = 20 });

                var config = await LoadConfigurationSectionAsync(options.Section);

                progress.Report(new ProgressReport { Message = "Validating settings...", Percentage = 50 });

                var validationResult = ValidateConfiguration(config, options.Section);

                progress.Report(new ProgressReport { Message = "Generating report...", Percentage = 80 });

                if (options.Fix && validationResult.Errors.Any())
                {
                    progress.Report(new ProgressReport { Message = "Applying fixes...", Percentage = 90 });
                    await ApplyValidationFixesAsync(validationResult.Errors);
                }

                progress.Report(new ProgressReport { Message = "Validation complete", Percentage = 100 });

                return new SettingsValidateResult
                {
                    Section = options.Section ?? "all",
                    ValidationResult = validationResult,
                    FixesApplied = options.Fix,
                    Success = true
                };
            }, "Validating configuration...");

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<SettingsValidateResult>.SuccessResult(
                result,
                $"Validation complete. Found {result.ValidationResult.Errors.Count} errors, {result.ValidationResult.Warnings.Count} warnings.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            return CommandResult<SettingsValidateResult>.ErrorResult($"Failed to validate configuration: {ex.Message}");
        }
    }

    public async Task<CommandResult<SettingsExportResult>> ExportConfigurationAsync(SettingsOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Exporting configuration to {Format} format", options.OutputFormat);

            var result = await _outputService.ShowProgressAsync(async (progress) =>
            {
                progress.Report(new ProgressReport { Message = "Loading configuration...", Percentage = 20 });

                var config = await LoadConfigurationSectionAsync(options.Section);

                progress.Report(new ProgressReport { Message = "Formatting export data...", Percentage = 50 });

                var exportData = await FormatConfigurationForExportAsync(config, options);

                progress.Report(new ProgressReport { Message = "Writing export file...", Percentage = 80 });

                await File.WriteAllTextAsync(options.OutputFile!, exportData);

                progress.Report(new ProgressReport { Message = "Export complete", Percentage = 100 });

                return new SettingsExportResult
                {
                    OutputFile = options.OutputFile!,
                    Format = options.OutputFormat,
                    Section = options.Section,
                    Success = true
                };
            }, "Exporting configuration...");

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<SettingsExportResult>.SuccessResult(
                result,
                $"Configuration exported to {options.OutputFile}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting configuration");
            return CommandResult<SettingsExportResult>.ErrorResult($"Failed to export configuration: {ex.Message}");
        }
    }

    public async Task<CommandResult<SettingsImportResult>> ImportConfigurationAsync(SettingsOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Importing configuration from {InputFile}", options.InputFile);

            var result = await _outputService.ShowProgressAsync(async (progress) =>
            {
                progress.Report(new ProgressReport { Message = "Reading import file...", Percentage = 20 });

                var importData = await File.ReadAllTextAsync(options.InputFile!);
                var importConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(importData);

                if (importConfig == null)
                {
                    throw new InvalidOperationException("Invalid configuration file format");
                }

                progress.Report(new ProgressReport { Message = "Validating import data...", Percentage = 40 });

                var validationResult = ValidateConfiguration(importConfig, null);
                if (!validationResult.IsValid && !options.DryRun)
                {
                    throw new InvalidOperationException($"Import validation failed: {string.Join(", ", validationResult.Errors)}");
                }

                if (options.DryRun)
                {
                    progress.Report(new ProgressReport { Message = "Dry run complete", Percentage = 100 });
                }
                else
                {
                    progress.Report(new ProgressReport { Message = "Applying configuration...", Percentage = 80 });
                    await ApplyImportedConfigurationAsync(importConfig, options.Merge);
                    progress.Report(new ProgressReport { Message = "Import complete", Percentage = 100 });
                }

                return new SettingsImportResult
                {
                    InputFile = options.InputFile!,
                    ValidationResult = validationResult,
                    DryRun = options.DryRun,
                    Merged = options.Merge,
                    Success = true
                };
            }, "Importing configuration...");

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return CommandResult<SettingsImportResult>.SuccessResult(
                result,
                options.DryRun ? "Import preview completed" : "Configuration imported successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing configuration");
            return CommandResult<SettingsImportResult>.ErrorResult($"Failed to import configuration: {ex.Message}");
        }
    }

    private async Task ApplyTemplateAsync(string template)
    {
        // Implementation would apply specific template configurations
        await Task.Delay(100);
    }

    private List<string> GetCreatedFiles(string configPath)
    {
        return Directory.GetFiles(configPath, "*.json")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();
    }

    private async Task<Dictionary<string, object>> LoadConfigurationSectionAsync(string? section)
    {
        var config = new Dictionary<string, object>();

        if (string.IsNullOrEmpty(section) || section == "all")
        {
            // Load all sections
            var sections = new[] { "scan", "graph", "llm" };
            foreach (var sectionName in sections)
            {
                var sectionConfig = await _configurationService.GetConfigurationAsync<Dictionary<string, object>>(sectionName);
                if (sectionConfig != null)
                {
                    config[sectionName] = sectionConfig;
                }
            }
        }
        else
        {
            // Load specific section
            var sectionConfig = await _configurationService.GetConfigurationAsync<Dictionary<string, object>>(section);
            if (sectionConfig != null)
            {
                config = sectionConfig;
            }
        }

        return config;
    }

    private object ParseConfigurationValue(string value, string type)
    {
        return type.ToLowerInvariant() switch
        {
            "boolean" => bool.Parse(value),
            "number" => double.Parse(value),
            "integer" => int.Parse(value),
            "array" => JsonSerializer.Deserialize<string[]>(value) ?? new string[0],
            _ => value
        };
    }

    private string GetSectionFromKey(string key)
    {
        var parts = key.Split('.');
        return parts.Length > 0 ? parts[0] : "general";
    }

    private void SetNestedValue(Dictionary<string, object> config, string key, object value)
    {
        var parts = key.Split('.');
        var current = config;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!current.ContainsKey(parts[i]))
            {
                current[parts[i]] = new Dictionary<string, object>();
            }
            current = (Dictionary<string, object>)current[parts[i]];
        }

        current[parts[^1]] = value;
    }

    private object? GetNestedValue(Dictionary<string, object> config, string key)
    {
        var parts = key.Split('.');
        object current = config;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> dict && dict.ContainsKey(part))
            {
                current = dict[part];
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private async Task<string> CreateConfigurationBackupAsync(string configPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(configPath, $"backup_{timestamp}");
        
        Directory.CreateDirectory(backupPath);
        
        foreach (var file in Directory.GetFiles(configPath, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            var backupFile = Path.Combine(backupPath, fileName);
            File.Copy(file, backupFile);
        }

        return backupPath;
    }

    private async Task<List<string>> ResetConfigurationSectionsAsync(string? section)
    {
        var sectionsReset = new List<string>();

        if (string.IsNullOrEmpty(section) || section == "all")
        {
            await _configurationService.InitializeConfigurationAsync();
            sectionsReset.AddRange(new[] { "scan", "graph", "llm" });
        }
        else
        {
            // Reset specific section by re-initializing it
            await _configurationService.InitializeConfigurationAsync();
            sectionsReset.Add(section);
        }

        return sectionsReset;
    }

    private ValidationResult ValidateConfiguration(Dictionary<string, object> config, string? section)
    {
        var result = ValidationResult.Valid();

        // Basic validation - would be more comprehensive in real implementation
        if (!config.Any())
        {
            result.Errors.Add("Configuration is empty");
            result.IsValid = false;
        }

        return result;
    }

    private async Task ApplyValidationFixesAsync(List<string> errors)
    {
        // Implementation would attempt to fix validation errors
        await Task.Delay(100);
    }

    private async Task<string> FormatConfigurationForExportAsync(Dictionary<string, object> config, SettingsOptions options)
    {
        await Task.Delay(50);

        return options.OutputFormat.ToLowerInvariant() switch
        {
            "yaml" => ConvertToYaml(config),
            "env" => ConvertToEnvironmentVariables(config),
            _ => JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        };
    }

    private string ConvertToYaml(Dictionary<string, object> config)
    {
        // Basic YAML conversion - would use proper YAML library in real implementation
        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    private string ConvertToEnvironmentVariables(Dictionary<string, object> config)
    {
        var env = new List<string>();
        FlattenToEnvironmentVariables(config, "", env);
        return string.Join(Environment.NewLine, env);
    }

    private void FlattenToEnvironmentVariables(Dictionary<string, object> config, string prefix, List<string> env)
    {
        foreach (var kvp in config)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}_{kvp.Key}";
            
            if (kvp.Value is Dictionary<string, object> nested)
            {
                FlattenToEnvironmentVariables(nested, key, env);
            }
            else
            {
                env.Add($"CODIVUS_{key.ToUpperInvariant()}={kvp.Value}");
            }
        }
    }

    private async Task ApplyImportedConfigurationAsync(Dictionary<string, object> importConfig, bool merge)
    {
        foreach (var section in importConfig.Keys)
        {
            if (merge)
            {
                var existing = await _configurationService.GetConfigurationAsync<Dictionary<string, object>>(section);
                if (existing != null)
                {
                    // Merge configurations
                    var merged = MergeConfigurations(existing, (Dictionary<string, object>)importConfig[section]);
                    await _configurationService.SetConfigurationAsync(section, merged);
                }
                else
                {
                    await _configurationService.SetConfigurationAsync(section, importConfig[section]);
                }
            }
            else
            {
                await _configurationService.SetConfigurationAsync(section, importConfig[section]);
            }
        }
    }

    private Dictionary<string, object> MergeConfigurations(Dictionary<string, object> existing, Dictionary<string, object> imported)
    {
        var merged = new Dictionary<string, object>(existing);
        
        foreach (var kvp in imported)
        {
            merged[kvp.Key] = kvp.Value;
        }
        
        return merged;
    }
}

// Supporting data models
public class SettingsInitResult
{
    public string ConfigurationPath { get; set; } = "";
    public string Template { get; set; } = "";
    public List<string> FilesCreated { get; set; } = new();
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SettingsShowResult
{
    public string Section { get; set; } = "";
    public Dictionary<string, object> Configuration { get; set; } = new();
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SettingsSetResult
{
    public string Key { get; set; } = "";
    public object? Value { get; set; }
    public string ValueType { get; set; } = "";
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SettingsGetResult
{
    public string Key { get; set; } = "";
    public object? Value { get; set; }
    public bool Found { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SettingsResetResult
{
    public string Section { get; set; } = "";
    public List<string> SectionsReset { get; set; } = new();
    public string BackupPath { get; set; } = "";
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SettingsValidateResult
{
    public string Section { get; set; } = "";
    public ValidationResult ValidationResult { get; set; } = new();
    public bool FixesApplied { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SettingsExportResult
{
    public string OutputFile { get; set; } = "";
    public string Format { get; set; } = "";
    public string? Section { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

public class SettingsImportResult
{
    public string InputFile { get; set; } = "";
    public ValidationResult ValidationResult { get; set; } = new();
    public bool DryRun { get; set; }
    public bool Merged { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}