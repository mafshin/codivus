using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Codivus.CLI.Services;

namespace Codivus.CLI.Commands;

public static class GraphCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("graph", "Perform graph-based analysis and queries");

        // Subcommands
        command.AddCommand(CreateScanCommand(services));
        command.AddCommand(CreateStatusCommand(services));
        command.AddCommand(CreateQueryCommand(services));
        command.AddCommand(CreateMetricsCommand(services));
        command.AddCommand(CreateVisualizationCommand(services));
        command.AddCommand(CreateAnalysisCommand(services));
        command.AddCommand(CreateExportCommand(services));

        return command;
    }

    private static Command CreateScanCommand(IServiceProvider services)
    {
        var command = new Command("scan", "Start graph scanning for a repository");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID or name") { IsRequired = true };
        var modeOption = new Option<string>(
            "--mode",
            getDefaultValue: () => "full",
            description: "Scan mode (full, incremental, differential)");
        var batchSizeOption = new Option<int>(
            "--batch-size",
            getDefaultValue: () => 100,
            description: "Processing batch size");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(modeOption);
        command.AddOption(batchSizeOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new GraphOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                ScanMode = context.ParseResult.GetValueForOption(modeOption),
                BatchSize = context.ParseResult.GetValueForOption(batchSizeOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var graphService = services.GetRequiredService<GraphCommandService>();
            var result = await graphService.StartGraphScanAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateStatusCommand(IServiceProvider services)
    {
        var command = new Command("status", "Check graph scan status");

        var scanIdOption = new Option<string>(
            "--scan-id",
            description: "Scan ID to check status for") { IsRequired = true };
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(scanIdOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new GraphOptions
            {
                ScanId = context.ParseResult.GetValueForOption(scanIdOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var graphService = services.GetRequiredService<GraphCommandService>();
            var result = await graphService.GetScanStatusAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateQueryCommand(IServiceProvider services)
    {
        var command = new Command("query", "Execute graph queries");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID to query") { IsRequired = true };
        var queryOption = new Option<string>(
            "--query",
            description: "Gremlin query to execute") { IsRequired = false };
        var nodeIdOption = new Option<string>(
            "--node-id",
            description: "Node ID for relationship queries") { IsRequired = false };
        var relationshipTypeOption = new Option<string>(
            "--relationship-type",
            description: "Type of relationships to query");
        var maxDepthOption = new Option<int>(
            "--max-depth",
            getDefaultValue: () => 3,
            description: "Maximum traversal depth");
        var limitOption = new Option<int>(
            "--limit",
            getDefaultValue: () => 100,
            description: "Maximum number of results");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(queryOption);
        command.AddOption(nodeIdOption);
        command.AddOption(relationshipTypeOption);
        command.AddOption(maxDepthOption);
        command.AddOption(limitOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new GraphOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                Query = context.ParseResult.GetValueForOption(queryOption),
                NodeId = context.ParseResult.GetValueForOption(nodeIdOption),
                MaxDepth = context.ParseResult.GetValueForOption(maxDepthOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var graphService = services.GetRequiredService<GraphCommandService>();
            var result = await graphService.ExecuteQueryAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateMetricsCommand(IServiceProvider services)
    {
        var command = new Command("metrics", "Get graph metrics and statistics");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID") { IsRequired = true };
        var detailedOption = new Option<bool>(
            "--detailed",
            getDefaultValue: () => false,
            description: "Show detailed metrics");
        var typeBreakdownOption = new Option<bool>(
            "--type-breakdown",
            getDefaultValue: () => true,
            description: "Include breakdown by node/relationship types");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(detailedOption);
        command.AddOption(typeBreakdownOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new GraphOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                IncludeMetrics = context.ParseResult.GetValueForOption(detailedOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var graphService = services.GetRequiredService<GraphCommandService>();
            var result = await graphService.GetMetricsAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateVisualizationCommand(IServiceProvider services)
    {
        var command = new Command("visualize", "Generate graph visualizations");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID") { IsRequired = true };
        var nodeIdOption = new Option<string>(
            "--focus-node",
            description: "Node to focus visualization on");
        var maxDepthOption = new Option<int>(
            "--max-depth",
            getDefaultValue: () => 2,
            description: "Maximum depth for subgraph");
        var layoutOption = new Option<string>(
            "--layout",
            getDefaultValue: () => "force-directed",
            description: "Layout algorithm (force-directed, hierarchical, circular)");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path") { IsRequired = true };
        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "svg",
            description: "Output format (svg, png, html, json)");

        command.AddOption(repositoryOption);
        command.AddOption(nodeIdOption);
        command.AddOption(maxDepthOption);
        command.AddOption(layoutOption);
        command.AddOption(outputFileOption);
        command.AddOption(formatOption);

        command.SetHandler(async (context) =>
        {
            var options = new GraphOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                NodeId = context.ParseResult.GetValueForOption(nodeIdOption),
                MaxDepth = context.ParseResult.GetValueForOption(maxDepthOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(formatOption) ?? "svg"
            };

            var graphService = services.GetRequiredService<GraphCommandService>();
            var result = await graphService.GenerateVisualizationAsync(options);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateAnalysisCommand(IServiceProvider services)
    {
        var command = new Command("analyze", "Perform graph-based code analysis");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID") { IsRequired = true };
        var analysisTypeOption = new Option<string>(
            "--type",
            getDefaultValue: () => "all",
            description: "Analysis type (complexity, coupling, dependencies, cycles, all)");
        var nodeIdOption = new Option<string>(
            "--focus-node",
            description: "Node to focus analysis on");
        var thresholdOption = new Option<double>(
            "--threshold",
            getDefaultValue: () => 0.0,
            description: "Threshold for filtering results");
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path");

        command.AddOption(repositoryOption);
        command.AddOption(analysisTypeOption);
        command.AddOption(nodeIdOption);
        command.AddOption(thresholdOption);
        command.AddOption(outputFileOption);

        command.SetHandler(async (context) =>
        {
            var options = new GraphOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                NodeId = context.ParseResult.GetValueForOption(nodeIdOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(Program.OutputFormatOption) ?? "console"
            };

            var analysisType = context.ParseResult.GetValueForOption(analysisTypeOption);
            var threshold = context.ParseResult.GetValueForOption(thresholdOption);

            var graphService = services.GetRequiredService<GraphCommandService>();
            var result = await graphService.PerformAnalysisAsync(options, analysisType!, threshold);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }

    private static Command CreateExportCommand(IServiceProvider services)
    {
        var command = new Command("export", "Export graph data");

        var repositoryOption = new Option<string>(
            "--repository",
            description: "Repository ID") { IsRequired = true };
        var outputFileOption = new Option<string>(
            "--output",
            description: "Output file path") { IsRequired = true };
        var formatOption = new Option<string>(
            "--format",
            getDefaultValue: () => "json",
            description: "Export format (json, xml, csv, graphml, cypher)");
        var includeMetadataOption = new Option<bool>(
            "--include-metadata",
            getDefaultValue: () => true,
            description: "Include node and relationship metadata");
        var compressOption = new Option<bool>(
            "--compress",
            getDefaultValue: () => false,
            description: "Compress the output file");

        command.AddOption(repositoryOption);
        command.AddOption(outputFileOption);
        command.AddOption(formatOption);
        command.AddOption(includeMetadataOption);
        command.AddOption(compressOption);

        command.SetHandler(async (context) =>
        {
            var options = new GraphOptions
            {
                RepositoryId = context.ParseResult.GetValueForOption(repositoryOption),
                OutputFile = context.ParseResult.GetValueForOption(outputFileOption),
                OutputFormat = context.ParseResult.GetValueForOption(formatOption) ?? "json"
            };

            var includeMetadata = context.ParseResult.GetValueForOption(includeMetadataOption);
            var compress = context.ParseResult.GetValueForOption(compressOption);

            var graphService = services.GetRequiredService<GraphCommandService>();
            var result = await graphService.ExportGraphAsync(options, includeMetadata, compress);
            
            var outputService = services.GetRequiredService<IOutputService>();
            await outputService.WriteResultsAsync(result);
            
            context.ExitCode = result.Success ? 0 : 1;
        });

        return command;
    }
}