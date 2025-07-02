using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.CLI.Services;

namespace Codivus.CLI.Commands;

public static class SettingsCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("settings", "Manage CLI configuration and preferences");

        // Subcommands
        command.AddCommand(CreateInitCommand(services));
        command.AddCommand(CreateShowCommand(services));
        command.AddCommand(CreateSetCommand(services));
        command.AddCommand(CreateGetCommand(services));
        command.AddCommand(CreateResetCommand(services));
        command.AddCommand(CreateValidateCommand(services));
        command.AddCommand(CreateExportCommand(services));
        command.AddCommand(CreateImportCommand(services));

        return command;
    }

    private static Command CreateInitCommand(IServiceProvider services)
    {
        var command = new Command("init", "Initialize configuration files with default values");

        var forceOption = new Option<bool>(
            "--force",
            getDefaultValue: () => false,
            description: "Overwrite existing configuration files");
        var templateOption = new Option<string>(
            "--template",
            description: "Use a specific configuration template (basic, advanced, enterprise)");

        command.AddOption(forceOption);
        command.AddOption(templateOption);

        command.SetHandler(async (context) =>
        {
            var options = new SettingsOptions
            {
                Force = context.ParseResult.GetValueForOption(forceOption),
                Template = context.ParseResult.GetValueForOption(templateOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var settingsService = services.GetRequiredService<SettingsCommandService>();
            var result = await settingsService.InitializeConfigurationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateShowCommand(IServiceProvider services)
    {
        var command = new Command("show", "Display current configuration settings");

        var sectionOption = new Option<string>(
            "--section",
            description: "Show specific configuration section (scan, graph, llm, all)");
        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "table",
            description: "Display format (table, json, yaml)");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(sectionOption);
        command.AddOption(formatOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new SettingsOptions
            {
                Section = context.ParseResult.GetValueForOption(sectionOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(formatOption) ?? "table"
            };

            var settingsService = services.GetRequiredService<SettingsCommandService>();
            var result = await settingsService.ShowConfigurationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateSetCommand(IServiceProvider services)
    {
        var command = new Command("set", "Set a configuration value");

        var keyArgument = new Argument<string>(
            "key",
            description: "Configuration key (e.g., scan.defaultFilePatterns, llm.defaultProvider)");
        var valueArgument = new Argument<string>(
            "value",
            description: "Configuration value");
        var typeOption = new Option<string>(
            "--type",
            getDefaultValue: () => "string",
            description: "Value type (string, number, boolean, array)");

        command.AddArgument(keyArgument);
        command.AddArgument(valueArgument);
        command.AddOption(typeOption);

        command.SetHandler(async (context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArgument);
            var value = context.ParseResult.GetValueForArgument(valueArgument);
            var type = context.ParseResult.GetValueForOption(typeOption);

            var options = new SettingsOptions
            {
                Key = key,
                Value = value,
                ValueType = type!,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var settingsService = services.GetRequiredService<SettingsCommandService>();
            var result = await settingsService.SetConfigurationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateGetCommand(IServiceProvider services)
    {
        var command = new Command("get", "Get a configuration value");

        var keyArgument = new Argument<string>(
            "key",
            description: "Configuration key to retrieve");
        var defaultOption = new Option<string>(
            "--default",
            description: "Default value if key is not found");

        command.AddArgument(keyArgument);
        command.AddOption(defaultOption);

        command.SetHandler(async (context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArgument);
            var defaultValue = context.ParseResult.GetValueForOption(defaultOption);

            var options = new SettingsOptions
            {
                Key = key,
                DefaultValue = defaultValue,
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var settingsService = services.GetRequiredService<SettingsCommandService>();
            var result = await settingsService.GetConfigurationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateResetCommand(IServiceProvider services)
    {
        var command = new Command("reset", "Reset configuration to default values");

        var sectionOption = new Option<string>(
            "--section",
            description: "Reset specific section (scan, graph, llm, all)");
        var confirmOption = new Option<bool>(
            "--confirm",
            getDefaultValue: () => false,
            description: "Skip confirmation prompt");

        command.AddOption(sectionOption);
        command.AddOption(confirmOption);

        command.SetHandler(async (context) =>
        {
            var options = new SettingsOptions
            {
                Section = context.ParseResult.GetValueForOption(sectionOption),
                Confirm = context.ParseResult.GetValueForOption(confirmOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var settingsService = services.GetRequiredService<SettingsCommandService>();
            var result = await settingsService.ResetConfigurationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateValidateCommand(IServiceProvider services)
    {
        var command = new Command("validate", "Validate configuration settings");

        var sectionOption = new Option<string>(
            "--section",
            description: "Validate specific section (scan, graph, llm, all)");
        var fixOption = new Option<bool>(
            "--fix",
            getDefaultValue: () => false,
            description: "Attempt to fix validation errors");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output validation report to file");

        command.AddOption(sectionOption);
        command.AddOption(fixOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new SettingsOptions
            {
                Section = context.ParseResult.GetValueForOption(sectionOption),
                Fix = context.ParseResult.GetValueForOption(fixOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var settingsService = services.GetRequiredService<SettingsCommandService>();
            var result = await settingsService.ValidateConfigurationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateExportCommand(IServiceProvider services)
    {
        var command = new Command("export", "Export configuration to file");

        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path") { IsRequired = true };
        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "json",
            description: "Export format (json, yaml, env)");
        var includeDefaultsOption = new Option<bool>(
            "--include-defaults",
            getDefaultValue: () => false,
            description: "Include default values in export");
        var sectionOption = new Option<string>(
            "--section",
            description: "Export specific section only");

        command.AddOption(outputFileOption);
        command.AddOption(formatOption);
        command.AddOption(includeDefaultsOption);
        command.AddOption(sectionOption);

        command.SetHandler(async (context) =>
        {
            var options = new SettingsOptions
            {
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(formatOption)!,
                IncludeDefaults = context.ParseResult.GetValueForOption(includeDefaultsOption),
                Section = context.ParseResult.GetValueForOption(sectionOption)
            };

            var settingsService = services.GetRequiredService<SettingsCommandService>();
            var result = await settingsService.ExportConfigurationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateImportCommand(IServiceProvider services)
    {
        var command = new Command("import", "Import configuration from file");

        var inputFileArgument = new Argument<string>(
            "input-file",
            description: "Configuration file to import");
        var mergeOption = new Option<bool>(
            "--merge",
            getDefaultValue: () => true,
            description: "Merge with existing configuration (vs replace)");
        var dryRunOption = new Option<bool>(
            "--dry-run",
            getDefaultValue: () => false,
            description: "Show what would be imported without making changes");

        command.AddArgument(inputFileArgument);
        command.AddOption(mergeOption);
        command.AddOption(dryRunOption);

        command.SetHandler(async (context) =>
        {
            var inputFile = context.ParseResult.GetValueForArgument(inputFileArgument);

            var options = new SettingsOptions
            {
                InputFile = inputFile,
                Merge = context.ParseResult.GetValueForOption(mergeOption),
                DryRun = context.ParseResult.GetValueForOption(dryRunOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var settingsService = services.GetRequiredService<SettingsCommandService>();
            var result = await settingsService.ImportConfigurationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }
}