using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.CLI.Services;

namespace Codivus.CLI.Commands;

public static class RepositoryCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("repository", "Manage repositories for analysis");
        command.AddAlias("repo");

        // Subcommands
        command.AddCommand(CreateAddCommand(services));
        command.AddCommand(CreateListCommand(services));
        command.AddCommand(CreateValidateCommand(services));
        command.AddCommand(CreateRemoveCommand(services));
        command.AddCommand(CreateInfoCommand(services));

        return command;
    }

    private static Command CreateAddCommand(IServiceProvider services)
    {
        var command = new Command("add", "Add a repository for analysis");

        var pathOption = new Option<string>(
            "--path",
            description: "Local path to repository") { IsRequired = true };
        var nameOption = new Option<string>(
            "--name",
            description: "Repository name (defaults to directory name)");
        var urlOption = new Option<string>(
            "--url",
            description: "Repository URL (for remote repositories)");
        var branchOption = new Option<string>(
            "--branch",
            getDefaultValue: () => "main",
            description: "Default branch");
        var typeOption = new Option<string>(
            "--type",
            getDefaultValue: () => "Local",
            description: "Repository type (Local, GitHub)");

        command.AddOption(pathOption);
        command.AddOption(nameOption);
        command.AddOption(urlOption);
        command.AddOption(branchOption);
        command.AddOption(typeOption);

        command.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForOption(pathOption)!;
            var name = context.ParseResult.GetValueForOption(nameOption) ?? Path.GetFileName(Path.GetFullPath(path));
            var url = context.ParseResult.GetValueForOption(urlOption);
            var branch = context.ParseResult.GetValueForOption(branchOption)!;
            var type = context.ParseResult.GetValueForOption(typeOption)!;

            var options = new RepositoryOptions
            {
                Path = path,
                Name = name,
                Url = url,
                DefaultBranch = branch,
                Type = type,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var repositoryService = services.GetRequiredService<RepositoryCommandService>();
            var result = await repositoryService.AddRepositoryAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateListCommand(IServiceProvider services)
    {
        var command = new Command("list", "List all registered repositories");

        var detailedOption = new Option<bool>(
            "--detailed",
            getDefaultValue: () => false,
            description: "Show detailed repository information");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(detailedOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new RepositoryOptions
            {
                Detailed = context.ParseResult.GetValueForOption(detailedOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var repositoryService = services.GetRequiredService<RepositoryCommandService>();
            var result = await repositoryService.ListRepositoriesAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateValidateCommand(IServiceProvider services)
    {
        var command = new Command("validate", "Validate a repository path");

        var pathOption = new Option<string>(
            "--path",
            description: "Path to validate") { IsRequired = true };
        var typeOption = new Option<string>(
            "--type",
            getDefaultValue: () => "Local",
            description: "Repository type (Local, GitHub)");

        command.AddOption(pathOption);
        command.AddOption(typeOption);

        command.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForOption(pathOption)!;
            var type = context.ParseResult.GetValueForOption(typeOption)!;

            var options = new RepositoryOptions
            {
                Path = path,
                Type = type,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var repositoryService = services.GetRequiredService<RepositoryCommandService>();
            var result = await repositoryService.ValidateRepositoryAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateRemoveCommand(IServiceProvider services)
    {
        var command = new Command("remove", "Remove a repository");
        command.AddAlias("rm");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID or name to remove") { IsRequired = true };
        var forceOption = new Option<bool>(
            "--force",
            getDefaultValue: () => false,
            description: "Force removal without confirmation");

        command.AddOption(repositoryOption);
        command.AddOption(forceOption);

        command.SetHandler(async (context) =>
        {
            var repository = context.ParseResult.GetValueForOption(repositoryOption)!;
            var force = context.ParseResult.GetValueForOption(forceOption);

            var options = new RepositoryOptions
            {
                RepositoryId = repository,
                Force = force,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var repositoryService = services.GetRequiredService<RepositoryCommandService>();
            var result = await repositoryService.RemoveRepositoryAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateInfoCommand(IServiceProvider services)
    {
        var command = new Command("info", "Show detailed information about a repository");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID or name") { IsRequired = true };
        var includeStructureOption = new Option<bool>(
            "--include-structure",
            getDefaultValue: () => false,
            description: "Include file structure information");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(includeStructureOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var repository = context.ParseResult.GetValueForOption(repositoryOption)!;
            var includeStructure = context.ParseResult.GetValueForOption(includeStructureOption);
            var outputFile = context.ParseResult.GetValueForOption(outputFileOption);

            var options = new RepositoryOptions
            {
                RepositoryId = repository,
                IncludeStructure = includeStructure,
                OutputFile = outputFile,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var repositoryService = services.GetRequiredService<RepositoryCommandService>();
            var result = await repositoryService.GetRepositoryInfoAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }
}