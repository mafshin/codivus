using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.CLI.Services;

namespace Codivus.CLI.Commands;

public static class ScanCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("scan", "Scan registered repositories for code issues");

        // Subcommands
        command.AddCommand(CreateStartCommand(services));
        command.AddCommand(CreateStatusCommand(services));
        command.AddCommand(CreateResultsCommand(services));
        command.AddCommand(CreatePauseCommand(services));
        command.AddCommand(CreateResumeCommand(services));
        command.AddCommand(CreateCancelCommand(services));
        command.AddCommand(CreateListCommand(services));

        return command;
    }

    private static Command CreateStartCommand(IServiceProvider services)
    {
        var command = new Command("start", "Start scanning a registered repository");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID or name") { IsRequired = true };
        var configNameOption = new Option<string>(
            "--config",
            description: "Scan configuration name");
        var llmProviderOption = new Option<string>(
            "--llm-provider",
            description: "LLM provider to use (Ollama, LMStudio)");
        var modelOption = new Option<string>(
            "--model",
            description: "LLM model to use for analysis");
        var includeTestsOption = new Option<bool>(
            "--include-tests",
            getDefaultValue: () => false,
            description: "Include test files in scan");
        var enableGraphOption = new Option<bool>(
            "--enable-graph",
            getDefaultValue: () => true,
            description: "Enable graph-enhanced analysis");
        var patternsOption = new Option<string[]>(
            "--patterns",
            getDefaultValue: () => Array.Empty<string>(),
            description: "File patterns to include (e.g., *.cs, *.js)");
        var excludeOption = new Option<string[]>(
            "--exclude",
            getDefaultValue: () => Array.Empty<string>(),
            description: "Patterns to exclude");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(configNameOption);
        command.AddOption(llmProviderOption);
        command.AddOption(modelOption);
        command.AddOption(includeTestsOption);
        command.AddOption(enableGraphOption);
        command.AddOption(patternsOption);
        command.AddOption(excludeOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new ScanOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption)!,
                ConfigurationName = context.ParseResult.GetValueForOption(configNameOption),
                LLMProvider = context.ParseResult.GetValueForOption(llmProviderOption),
                Model = context.ParseResult.GetValueForOption(modelOption),
                IncludeTests = context.ParseResult.GetValueForOption(includeTestsOption),
                EnableGraph = context.ParseResult.GetValueForOption(enableGraphOption),
                FilePatterns = context.ParseResult.GetValueForOption(patternsOption)?.ToList() ?? new List<string>(),
                ExcludePatterns = context.ParseResult.GetValueForOption(excludeOption)?.ToList() ?? new List<string>(),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                Verbose = context.ParseResult.GetValueForOption(Program.VerboseOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var scanService = services.GetRequiredService<ScanCommandService>();
            var result = await scanService.StartScanAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateStatusCommand(IServiceProvider services)
    {
        var command = new Command("status", "Check status of running scans");

        var scanIdOption = new Option<string>(
            "--scan-id",
            description: "Specific scan ID to check");
        var repositoryOption = new Option<string>(
            "--repository",
            description: "Show scans for specific repository");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(scanIdOption);
        command.AddOption(repositoryOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new ScanOptions
            {
                ScanId = context.ParseResult.GetValueForOption(scanIdOption),
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var scanService = services.GetRequiredService<ScanCommandService>();
            var result = await scanService.GetScanStatusAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateResultsCommand(IServiceProvider services)
    {
        var command = new Command("results", "Get scan results and issues");

        var scanIdOption = new Option<string>(
            "--scan-id",
            description: "Scan ID to get results for") { IsRequired = true };
        var issueTypeOption = new Option<string>(
            "--issue-type",
            description: "Filter by issue type (Security, Performance, CodeQuality, etc.)");
        var severityOption = new Option<string>(
            "--severity",
            description: "Filter by severity (Critical, High, Medium, Low)");
        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 100,
            description: "Maximum number of results");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(scanIdOption);
        command.AddOption(issueTypeOption);
        command.AddOption(severityOption);
        command.AddOption(limitOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new ScanOptions
            {
                ScanId = context.ParseResult.GetValueForOption(scanIdOption)!,
                IssueType = context.ParseResult.GetValueForOption(issueTypeOption),
                Severity = context.ParseResult.GetValueForOption(severityOption),
                Limit = context.ParseResult.GetValueForOption(limitOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var scanService = services.GetRequiredService<ScanCommandService>();
            var result = await scanService.GetScanResultsAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreatePauseCommand(IServiceProvider services)
    {
        var command = new Command("pause", "Pause a running scan");

        var scanIdOption = new Option<string>(
            "--scan-id",
            description: "Scan ID to pause") { IsRequired = true };

        command.AddOption(scanIdOption);

        command.SetHandler(async (context) =>
        {
            var options = new ScanOptions
            {
                ScanId = context.ParseResult.GetValueForOption(scanIdOption)!,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var scanService = services.GetRequiredService<ScanCommandService>();
            var result = await scanService.PauseScanAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateResumeCommand(IServiceProvider services)
    {
        var command = new Command("resume", "Resume a paused scan");

        var scanIdOption = new Option<string>(
            "--scan-id",
            description: "Scan ID to resume") { IsRequired = true };

        command.AddOption(scanIdOption);

        command.SetHandler(async (context) =>
        {
            var options = new ScanOptions
            {
                ScanId = context.ParseResult.GetValueForOption(scanIdOption)!,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var scanService = services.GetRequiredService<ScanCommandService>();
            var result = await scanService.ResumeScanAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateCancelCommand(IServiceProvider services)
    {
        var command = new Command("cancel", "Cancel a running scan");

        var scanIdOption = new Option<string>(
            "--scan-id",
            description: "Scan ID to cancel") { IsRequired = true };

        command.AddOption(scanIdOption);

        command.SetHandler(async (context) =>
        {
            var options = new ScanOptions
            {
                ScanId = context.ParseResult.GetValueForOption(scanIdOption)!,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var scanService = services.GetRequiredService<ScanCommandService>();
            var result = await scanService.CancelScanAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateListCommand(IServiceProvider services)
    {
        var command = new Command("list", "List all scans");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Filter by repository ID or name");
        var statusOption = new Option<string>(
            "--status",
            description: "Filter by status (Running, Completed, Failed, Paused)");
        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 20,
            description: "Maximum number of scans to show");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(statusOption);
        command.AddOption(limitOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new ScanOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                Status = context.ParseResult.GetValueForOption(statusOption),
                Limit = context.ParseResult.GetValueForOption(limitOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var scanService = services.GetRequiredService<ScanCommandService>();
            var result = await scanService.ListScansAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }
}