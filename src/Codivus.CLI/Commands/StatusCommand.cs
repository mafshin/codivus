using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.CLI.Services;

namespace Codivus.CLI.Commands;

public static class StatusCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("status", "Show status of repositories, scans, and system health");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Show status for specific repository");
        var systemOption = new Option<bool>(
            "--system",
            getDefaultValue: () => false,
            description: "Include system health information");
        var detailedOption = new Option<bool>(
            "--detailed",
            getDefaultValue: () => false,
            description: "Show detailed status information");
        var refreshOption = new Option<bool>(
            "--refresh",
            getDefaultValue: () => false,
            description: "Refresh cached status information");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output status report to file");

        command.AddOption(repositoryOption);
        command.AddOption(systemOption);
        command.AddOption(detailedOption);
        command.AddOption(refreshOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new StatusOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                IncludeSystemHealth = context.ParseResult.GetValueForOption(systemOption),
                Detailed = context.ParseResult.GetValueForOption(detailedOption),
                Refresh = context.ParseResult.GetValueForOption(refreshOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var statusService = services.GetRequiredService<StatusCommandService>();
            var result = await statusService.GetStatusAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }
}