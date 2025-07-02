using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Codivus.CLI.Commands;
using Codivus.CLI.Services;
using Codivus.CLI.Infrastructure;
using Codivus.Core.Interfaces;
using Codivus.API.Services;
using Codivus.API.Data;
using Codivus.Graph.Services;
using Codivus.Graph.Configuration;
using Codivus.Graph.Interfaces;
using Spectre.Console;
using System.IO.Abstractions;

namespace Codivus.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Display banner
            DisplayBanner();

            // Build configuration
            var configuration = BuildConfiguration();

            // Create host and services
            var host = CreateHost(configuration);

            // Create root command with all subcommands
            var rootCommand = CreateRootCommand(host.Services);

            // Execute the command
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }
    }

    private static void DisplayBanner()
    {
        var banner = new FigletText("Codivus")
            .LeftJustified()
            .Color(Color.Cyan1);

        AnsiConsole.Write(banner);
        
        AnsiConsole.MarkupLine("[grey]AI-Powered Code Analysis & Graph Intelligence[/]");
        AnsiConsole.MarkupLine("[grey]Version 1.0.0[/]");
        AnsiConsole.WriteLine();
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables("CODIVUS_")
            .Build();
    }

    private static IHost CreateHost(IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.AddSingleton(configuration);

                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                });

                // System dependencies
                services.AddScoped<IFileSystem, FileSystem>();
                services.AddScoped<JsonDataStore>();
                
                // Core Services
                services.AddScoped<IRepositoryService, RepositoryService>();
                services.AddScoped<IScanningService, ScanningService>();
                
                // Graph Services
                services.Configure<GraphConfiguration>(configuration.GetSection("Codivus:Graph"));
                services.AddScoped<IGraphStorageService, GraphStorageService>();
                services.AddScoped<IGraphQueryService, GraphQueryService>();
                services.AddScoped<IGraphEnhancedScanningService, GraphEnhancedScanningService>();

                // CLI Services
                services.AddScoped<IOutputService, OutputService>();
                services.AddScoped<IConfigurationService, ConfigurationService>();
                services.AddScoped<IProgressService, ProgressService>();
                services.AddScoped<IValidationService, ValidationService>();

                // Command Services
                services.AddScoped<RepositoryCommandService>();
                services.AddScoped<ScanCommandService>();
                services.AddScoped<GraphCommandService>();
                services.AddScoped<IssuesCommandService>();
                services.AddScoped<SettingsCommandService>();
                services.AddScoped<InitCommandService>();
                services.AddScoped<StatusCommandService>();
            })
            .Build();
    }

    // Static options that can be referenced by commands
    public static readonly Option<bool> VerboseOption = new Option<bool>(
        "--verbose",
        description: "Enable verbose output");
    
    public static readonly Option<string> OutputFormatOption = new Option<string>(
        "--format",
        getDefaultValue: () => "console",
        description: "Output format (console, json, xml, csv, html)");

    private static RootCommand CreateRootCommand(IServiceProvider services)
    {
        var rootCommand = new RootCommand("Codivus - AI-Powered Code Analysis Tool")
        {
            Description = "Analyze code with AI-powered insights and graph-based intelligence"
        };

        // Add global options
        var configOption = new Option<FileInfo?>(
            "--config",
            description: "Path to configuration file");

        rootCommand.AddGlobalOption(VerboseOption);
        rootCommand.AddGlobalOption(configOption);
        rootCommand.AddGlobalOption(OutputFormatOption);

        // Add subcommands
        rootCommand.AddCommand(RepositoryCommand.Create(services));
        rootCommand.AddCommand(ScanCommand.Create(services));
        rootCommand.AddCommand(GraphCommand.Create(services));
        rootCommand.AddCommand(IssuesCommand.Create(services));
        rootCommand.AddCommand(SettingsCommand.Create(services));
        rootCommand.AddCommand(InitCommand.Create(services));
        rootCommand.AddCommand(StatusCommand.Create(services));

        return rootCommand;
    }
}