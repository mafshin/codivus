using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.CLI.Services;

namespace Codivus.CLI.Commands;

public static class InitCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("init", "Initialize a new Codivus project or workspace");

        var pathOption = new Option<string>(
            "--path",
            getDefaultValue: () => Directory.GetCurrentDirectory(),
            description: "Path to initialize (defaults to current directory)");
        var templateOption = new Option<string>(
            "--template",
            getDefaultValue: () => "basic",
            description: "Project template (basic, advanced, enterprise)");
        var nameOption = new Option<string>(
            "--name",
            description: "Project name (defaults to directory name)");
        var forceOption = new Option<bool>(
            "--force",
            getDefaultValue: () => false,
            description: "Overwrite existing configuration");
        var gitOption = new Option<bool>(
            "--git",
            getDefaultValue: () => true,
            description: "Initialize git repository if not exists");

        command.AddOption(pathOption);
        command.AddOption(templateOption);
        command.AddOption(nameOption);
        command.AddOption(forceOption);
        command.AddOption(gitOption);

        command.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForOption(pathOption)!;
            var template = context.ParseResult.GetValueForOption(templateOption)!;
            var name = context.ParseResult.GetValueForOption(nameOption) ?? Path.GetFileName(Path.GetFullPath(path));
            var force = context.ParseResult.GetValueForOption(forceOption);
            var initGit = context.ParseResult.GetValueForOption(gitOption);

            var options = new InitOptions
            {
                Path = path,
                Template = template,
                Name = name,
                Force = force,
                InitializeGit = initGit,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var initService = services.GetRequiredService<InitCommandService>();
            var result = await initService.InitializeProjectAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }
}