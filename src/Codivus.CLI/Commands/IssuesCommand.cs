using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.CLI.Services;

namespace Codivus.CLI.Commands;

public static class IssuesCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("issues", "Manage and analyze code issues");

        // Subcommands
        command.AddCommand(CreateListCommand(services));
        command.AddCommand(CreateShowCommand(services));
        command.AddCommand(CreateFixCommand(services));
        command.AddCommand(CreateExportCommand(services));
        command.AddCommand(CreateStatsCommand(services));

        return command;
    }

    private static Command CreateListCommand(IServiceProvider services)
    {
        var command = new Command("list", "List issues from previous scans");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID to list issues for") { IsRequired = false };
        var severityOption = new Option<string>(
            "--severity",
            description: "Filter by severity (critical, high, medium, low, info)");
        var typeOption = new Option<string>(
            "--type",
            description: "Filter by issue type (Security, Performance, CodeQuality, etc.)");
        var statusOption = new Option<string>(
            "--status",
            getDefaultValue: () => "all",
            description: "Filter by status (open, resolved, ignored, all)");
        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 100,
            description: "Maximum number of issues to display");
        var sortOption = new Option<string>(
            "--sort",
            getDefaultValue: () => "severity",
            description: "Sort by (severity, type, file, date)");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(severityOption);
        command.AddOption(typeOption);
        command.AddOption(statusOption);
        command.AddOption(limitOption);
        command.AddOption(sortOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new IssuesOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                Severity = context.ParseResult.GetValueForOption(severityOption),
                Type = context.ParseResult.GetValueForOption(typeOption),
                Status = context.ParseResult.GetValueForOption(statusOption)!,
                Limit = context.ParseResult.GetValueForOption(limitOption),
                SortBy = context.ParseResult.GetValueForOption(sortOption)!,
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var issuesService = services.GetRequiredService<IssuesCommandService>();
            var result = await issuesService.ListIssuesAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateShowCommand(IServiceProvider services)
    {
        var command = new Command("show", "Show detailed information about a specific issue");

        var issueIdArgument = new Argument<string>(
            "issue-id",
            description: "ID of the issue to show");
        var includeFixesOption = new Option<bool>(
            "--include-fixes",
            getDefaultValue: () => true,
            description: "Include suggested fixes");
        var includeContextOption = new Option<bool>(
            "--include-context",
            getDefaultValue: () => true,
            description: "Include surrounding code context");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddArgument(issueIdArgument);
        command.AddOption(includeFixesOption);
        command.AddOption(includeContextOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var issueId = context.ParseResult.GetValueForArgument(issueIdArgument);
            var options = new IssuesOptions
            {
                IssueId = issueId,
                IncludeFixes = context.ParseResult.GetValueForOption(includeFixesOption),
                IncludeContext = context.ParseResult.GetValueForOption(includeContextOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var issuesService = services.GetRequiredService<IssuesCommandService>();
            var result = await issuesService.ShowIssueAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateFixCommand(IServiceProvider services)
    {
        var command = new Command("fix", "Apply automated fixes to issues");

        var issueIdOption = new Option<string>(
            "--issue-id",
            description: "Specific issue ID to fix");
        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID to fix issues for");
        var severityOption = new Option<string>(
            "--severity",
            description: "Fix issues of specific severity (critical, high, medium)");
        var typeOption = new Option<string>(
            "--type",
            description: "Fix issues of specific type");
        var dryRunOption = new Option<bool>(
            "--dry-run",
            getDefaultValue: () => false,
            description: "Show what would be fixed without making changes");
        var backupOption = new Option<bool>(
            "--backup",
            getDefaultValue: () => true,
            description: "Create backup before applying fixes");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path for fix report");

        command.AddOption(issueIdOption);
        command.AddOption(repositoryOption);
        command.AddOption(severityOption);
        command.AddOption(typeOption);
        command.AddOption(dryRunOption);
        command.AddOption(backupOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new IssuesOptions
            {
                IssueId = context.ParseResult.GetValueForOption(issueIdOption),
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                Severity = context.ParseResult.GetValueForOption(severityOption),
                Type = context.ParseResult.GetValueForOption(typeOption),
                DryRun = context.ParseResult.GetValueForOption(dryRunOption),
                CreateBackup = context.ParseResult.GetValueForOption(backupOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var issuesService = services.GetRequiredService<IssuesCommandService>();
            var result = await issuesService.FixIssuesAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateExportCommand(IServiceProvider services)
    {
        var command = new Command("export", "Export issues to various formats");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID to export issues for") { IsRequired = false };
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path") { IsRequired = true };
        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "json",
            description: "Export format (json, csv, xml, sarif, html)");
        var includeDismissedOption = new Option<bool>(
            "--include-dismissed",
            getDefaultValue: () => false,
            description: "Include dismissed issues");
        var includeFixedOption = new Option<bool>(
            "--include-fixed",
            getDefaultValue: () => false,
            description: "Include fixed issues");
        var templateOption = new Option<string>(
            "--template",
            description: "Custom template file for formatting");

        command.AddOption(repositoryOption);
        command.AddOption(outputFileOption);
        command.AddOption(formatOption);
        command.AddOption(includeDismissedOption);
        command.AddOption(includeFixedOption);
        command.AddOption(templateOption);

        command.SetHandler(async (context) =>
        {
            var options = new IssuesOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(formatOption)!,
                IncludeDismissed = context.ParseResult.GetValueForOption(includeDismissedOption),
                IncludeFixed = context.ParseResult.GetValueForOption(includeFixedOption),
                Template = context.ParseResult.GetValueForOption(templateOption)
            };

            var issuesService = services.GetRequiredService<IssuesCommandService>();
            var result = await issuesService.ExportIssuesAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateStatsCommand(IServiceProvider services)
    {
        var command = new Command("stats", "Show statistics about issues");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID to get stats for");
        var timeRangeOption = new Option<string>(
            "--time-range",
            getDefaultValue: () => "30d",
            description: "Time range for stats (7d, 30d, 90d, 1y, all)");
        var groupByOption = new Option<string>(
            "--group-by",
            getDefaultValue: () => "severity",
            description: "Group statistics by (severity, type, file, date)");
        var includeChartsOption = new Option<bool>(
            "--include-charts",
            getDefaultValue: () => false,
            description: "Include ASCII charts in output");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(timeRangeOption);
        command.AddOption(groupByOption);
        command.AddOption(includeChartsOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new IssuesOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                TimeRange = context.ParseResult.GetValueForOption(timeRangeOption)!,
                GroupBy = context.ParseResult.GetValueForOption(groupByOption)!,
                IncludeCharts = context.ParseResult.GetValueForOption(includeChartsOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var issuesService = services.GetRequiredService<IssuesCommandService>();
            var result = await issuesService.GetIssueStatsAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }
}