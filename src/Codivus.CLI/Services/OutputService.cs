using System.Text.Json;
using Spectre.Console;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Codivus.CLI.Services;

public class OutputService : IOutputService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutputService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OutputService(IConfiguration configuration, ILogger<OutputService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    public void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
        _logger.LogError(message);
    }

    public void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
        _logger.LogWarning(message);
    }

    public void WriteInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }

    public void WriteDebug(string message)
    {
        if (_configuration.GetValue<bool>("Codivus:Output:EnableDebug"))
        {
            AnsiConsole.MarkupLine($"[grey]🔧 {Markup.Escape(message)}[/]");
        }
        _logger.LogDebug(message);
    }

    public void WriteLine(string message = "")
    {
        AnsiConsole.WriteLine(message);
    }

    public void WriteTable<T>(IEnumerable<T> items, string title = "")
    {
        var itemList = items.ToList();
        if (!itemList.Any())
        {
            WriteInfo("No items to display");
            return;
        }

        var table = new Table();
        if (!string.IsNullOrEmpty(title))
        {
            table.Title = new TableTitle(title);
        }

        table.BorderColor(Color.Grey);

        // Get properties for columns
        var properties = typeof(T).GetProperties();
        foreach (var prop in properties)
        {
            table.AddColumn(new TableColumn(prop.Name).Centered());
        }

        // Add rows
        foreach (var item in itemList)
        {
            var values = properties.Select(p => 
            {
                var value = p.GetValue(item);
                return value?.ToString() ?? "";
            }).ToArray();

            table.AddRow(values);
        }

        AnsiConsole.Write(table);
    }

    public void WriteJson(object obj)
    {
        var json = JsonSerializer.Serialize(obj, _jsonOptions);
        AnsiConsole.WriteLine(json);
    }

    public void WriteMarkup(string markup)
    {
        AnsiConsole.Markup(markup);
    }

    public async Task WriteResultsAsync<T>(CommandResult<T> result)
    {
        if (result.Success)
        {
            if (!string.IsNullOrEmpty(result.Message))
            {
                WriteSuccess(result.Message);
            }

            if (result.Data != null)
            {
                var format = _configuration.GetValue<string>("Codivus:Output:DefaultFormat", "console");
                await WriteDataAsync(result.Data, format);
            }
        }
        else
        {
            foreach (var error in result.Errors)
            {
                WriteError(error);
            }
        }

        foreach (var warning in result.Warnings)
        {
            WriteWarning(warning);
        }

        if (result.Duration > TimeSpan.Zero)
        {
            WriteInfo($"Completed in {result.Duration.TotalSeconds:F2} seconds");
        }
    }

    public void ShowProgress(Action<IProgress<ProgressReport>> action, string description = "Processing...")
    {
        AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .Start(ctx =>
            {
                var task = ctx.AddTask(description);
                var progress = new Progress<ProgressReport>(report =>
                {
                    task.Description = report.Message;
                    task.Value = report.Percentage;
                });

                action(progress);
                task.Value = 100;
            });
    }

    public async Task ShowProgressAsync(Func<IProgress<ProgressReport>, Task> action, string description = "Processing...")
    {
        await AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(description);
                var progress = new Progress<ProgressReport>(report =>
                {
                    task.Description = report.Message;
                    task.Value = report.Percentage;
                });

                await action(progress);
                task.Value = 100;
            });
    }

    public async Task<T> ShowProgressAsync<T>(Func<IProgress<ProgressReport>, Task<T>> action, string description = "Processing...")
    {
        return await AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(description);
                var progress = new Progress<ProgressReport>(report =>
                {
                    task.Description = report.Message;
                    task.Value = report.Percentage;
                });

                var result = await action(progress);
                task.Value = 100;
                return result;
            });
    }

    private async Task WriteDataAsync<T>(T data, string format)
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                WriteJson(data);
                break;
            case "table":
                if (data is IEnumerable<object> items)
                {
                    WriteTable(items);
                }
                else
                {
                    WriteTable(new[] { data });
                }
                break;
            case "console":
            default:
                await WriteConsoleFormatAsync(data);
                break;
        }
    }

    private async Task WriteConsoleFormatAsync<T>(T data)
    {
        switch (data)
        {
            case RepositoryListResult repositoryList:
                WriteRepositoryList(repositoryList);
                break;
            case IssuesListResult issuesList:
                WriteIssuesList(issuesList);
                break;
            case StatusResult statusResult:
                WriteStatusResult(statusResult);
                break;
            case ScanResult scanResult:
                WriteScanResult(scanResult);
                break;
            case IEnumerable<IssueInfo> issues:
                WriteIssues(issues);
                break;
            case GraphMetrics metrics:
                WriteGraphMetrics(metrics);
                break;
            case RepositoryResult repositoryResult:
                WriteRepositoryResult(repositoryResult);
                break;
            case RepositoryValidationResult validationResult:
                WriteRepositoryValidationResult(validationResult);
                break;
            case RepositoryInfoResult infoResult:
                WriteRepositoryInfoResult(infoResult);
                break;
            case ScanStartResult scanStartResult:
                WriteScanStartResult(scanStartResult);
                break;
            case ScanStatusResult scanStatusResult:
                WriteScanStatusResult(scanStatusResult);
                break;
            case ScanResultsResult scanResultsResult:
                WriteScanResultsResult(scanResultsResult);
                break;
            case ScanOperationResult scanOperationResult:
                WriteScanOperationResult(scanOperationResult);
                break;
            case ScanListResult scanListResult:
                WriteScanListResult(scanListResult);
                break;
            case GraphScanResult graphScanResult:
                WriteGraphScanResult(graphScanResult);
                break;
            case GraphQueryResult graphQueryResult:
                WriteGraphQueryResult(graphQueryResult);
                break;
            case GraphMetricsResult graphMetricsResult:
                WriteGraphMetricsResult(graphMetricsResult);
                break;
            case GraphAnalysisResult graphAnalysisResult:
                WriteGraphAnalysisResult(graphAnalysisResult);
                break;
            case GraphExportResult graphExportResult:
                WriteGraphExportResult(graphExportResult);
                break;
            case GraphVisualizationResult graphVisualizationResult:
                WriteGraphVisualizationResult(graphVisualizationResult);
                break;
            case IssueDetailResult issueDetailResult:
                WriteIssueDetailResult(issueDetailResult);
                break;
            case IssueUpdateResult issueUpdateResult:
                WriteIssueUpdateResult(issueUpdateResult);
                break;
            case IssueExportResult issueExportResult:
                WriteIssueExportResult(issueExportResult);
                break;
            case IssueStatsResult issueStatsResult:
                WriteIssueStatsResult(issueStatsResult);
                break;
            case LlmProvidersResult llmProvidersResult:
                WriteLlmProvidersResult(llmProvidersResult);
                break;
            case LlmModelsResult llmModelsResult:
                WriteLlmModelsResult(llmModelsResult);
                break;
            case LlmTestResult llmTestResult:
                WriteLlmTestResult(llmTestResult);
                break;
            default:
                WriteInfo(data?.ToString() ?? "No data");
                break;
        }
    }

    private void WriteScanResult(ScanResult result)
    {
        var panel = new Panel(new Markup($"""
            [bold]Repository:[/] {result.RepositoryId}
            [bold]Path:[/] {result.Path}
            [bold]Files Scanned:[/] {result.FilesScanned}
            [bold]Issues Found:[/] {result.IssuesFound}
            [bold]Duration:[/] {result.Duration.TotalSeconds:F2}s
            """))
        {
            Header = new PanelHeader("Scan Results"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };

        AnsiConsole.Write(panel);

        if (result.Issues.Any())
        {
            WriteLine();
            WriteIssues(result.Issues);
        }

        if (result.GraphMetrics != null)
        {
            WriteLine();
            WriteGraphMetrics(result.GraphMetrics);
        }
    }

    private void WriteIssues(IEnumerable<IssueInfo> issues)
    {
        var issueList = issues.ToList();
        if (!issueList.Any())
        {
            WriteInfo("No issues found");
            return;
        }

        var tree = new Tree("Issues Found");

        var groupedIssues = issueList.GroupBy(i => i.Severity);
        foreach (var group in groupedIssues.OrderBy(g => GetSeverityOrder(g.Key)))
        {
            var severityColor = GetSeverityColor(group.Key);
            var severityNode = tree.AddNode($"[{severityColor}]{group.Key} ({group.Count()})[/]");

            foreach (var issue in group)
            {
                var issueNode = severityNode.AddNode($"[bold]{issue.Type}[/]: {issue.Message}");
                issueNode.AddNode($"File: {issue.File}:{issue.Line}:{issue.Column}");
                if (!string.IsNullOrEmpty(issue.Description))
                {
                    issueNode.AddNode($"Description: {issue.Description}");
                }
                if (issue.ConfidenceScore > 0)
                {
                    issueNode.AddNode($"Confidence: {issue.ConfidenceScore:P0}");
                }
                if (issue.Recommendations.Any())
                {
                    var recNode = issueNode.AddNode("Recommendations:");
                    foreach (var rec in issue.Recommendations)
                    {
                        recNode.AddNode($"• {rec}");
                    }
                }
            }
        }

        AnsiConsole.Write(tree);
    }

    private void WriteGraphMetrics(GraphMetrics metrics)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Title("Graph Metrics");

        table.AddColumn("Metric");
        table.AddColumn("Value");

        table.AddRow("Total Nodes", metrics.TotalNodes.ToString("N0"));
        table.AddRow("Total Relationships", metrics.TotalRelationships.ToString("N0"));
        table.AddRow("Average Complexity", metrics.AverageComplexity.ToString("F2"));
        table.AddRow("Average Coupling", metrics.AverageCoupling.ToString("F2"));

        AnsiConsole.Write(table);

        if (metrics.NodesByType.Any())
        {
            WriteLine();
            var nodeChart = new BarChart()
                .Width(60)
                .Label("Nodes by Type");

            foreach (var kvp in metrics.NodesByType)
            {
                nodeChart.AddItem(kvp.Key, kvp.Value, Color.Blue);
            }

            AnsiConsole.Write(nodeChart);
        }
    }

    private static string GetSeverityColor(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => "red",
        "high" => "orange1",
        "medium" => "yellow",
        "low" => "blue",
        "info" => "green",
        _ => "white"
    };

    private static int GetSeverityOrder(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 0,
        "high" => 1,
        "medium" => 2,
        "low" => 3,
        "info" => 4,
        _ => 5
    };

    private void WriteRepositoryList(RepositoryListResult result)
    {
        if (!result.Repositories.Any())
        {
            WriteInfo("No repositories found.");
            return;
        }

        var table = new Table();
        table.Title = new TableTitle("Repositories");
        table.BorderColor(Color.Grey);
        table.AddColumn("Name");
        table.AddColumn("Type");
        table.AddColumn("Location");
        table.AddColumn("Scans");
        table.AddColumn("Issues");
        table.AddColumn("Added");

        foreach (var repoDetail in result.Repositories)
        {
            table.AddRow(
                repoDetail.Repository.Name,
                repoDetail.Repository.Type,
                repoDetail.Repository.Location,
                repoDetail.ScanCount.ToString(),
                repoDetail.IssueCount.ToString(),
                repoDetail.Repository.AddedAt.ToString("yyyy-MM-dd")
            );
        }

        AnsiConsole.Write(table);
    }

    private void WriteIssuesList(IssuesListResult result)
    {
        if (!result.Issues.Any())
        {
            WriteInfo("No issues found.");
            return;
        }

        WriteIssues(result.Issues);
        
        if (result.TotalCount != result.FilteredCount)
        {
            WriteInfo($"Showing {result.FilteredCount} of {result.TotalCount} total issues");
        }
    }

    private void WriteStatusResult(StatusResult result)
    {
        var table = new Table();
        table.Title = new TableTitle("Repository Status");
        table.BorderColor(Color.Grey);
        table.AddColumn("Repository");
        table.AddColumn("Status");
        table.AddColumn("Last Scan");
        table.AddColumn("Issues");

        foreach (var status in result.Repositories)
        {
            table.AddRow(
                status.Name ?? "Unknown",
                status.Status ?? "Unknown",
                status.LastScanned?.ToString("yyyy-MM-dd HH:mm") ?? "Never",
                status.IssueCount.ToString()
            );
        }

        AnsiConsole.Write(table);
    }

    private void WriteRepositoryResult(RepositoryResult result)
    {
        if (result.Repository == null)
        {
            WriteError("Repository data not available");
            return;
        }

        var panel = new Panel(new Markup($"""
            [bold]Name:[/] {result.Repository.Name}
            [bold]Location:[/] {result.Repository.Location}
            [bold]Type:[/] {result.Repository.Type}
            [bold]Added:[/] {result.Repository.AddedAt:yyyy-MM-dd HH:mm}
            """))
        {
            Header = new PanelHeader("Repository"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(result.Success ? Color.Green : Color.Red)
        };

        AnsiConsole.Write(panel);
    }

    private void WriteRepositoryValidationResult(RepositoryValidationResult result)
    {
        var statusColor = result.IsValid ? "green" : "red";
        var statusIcon = result.IsValid ? "✓" : "✗";
        
        AnsiConsole.MarkupLine($"[{statusColor}]{statusIcon}[/] Repository validation: [{statusColor}]{(result.IsValid ? "VALID" : "INVALID")}[/]");
        WriteLine();

        var table = new Table();
        table.Title = new TableTitle("Validation Details");
        table.BorderColor(result.IsValid ? Color.Green : Color.Red);
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Path", result.Path);
        table.AddRow("Type", result.Type);
        table.AddRow("Valid", result.IsValid ? "[green]Yes[/]" : "[red]No[/]");

        AnsiConsole.Write(table);

        if (result.ValidationErrors.Any())
        {
            WriteLine();
            var errorPanel = new Panel(string.Join("\n", result.ValidationErrors.Select(e => $"• {e}")))
            {
                Header = new PanelHeader("[red]Errors[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            };
            AnsiConsole.Write(errorPanel);
        }

        if (result.ValidationWarnings.Any())
        {
            WriteLine();
            var warningPanel = new Panel(string.Join("\n", result.ValidationWarnings.Select(w => $"• {w}")))
            {
                Header = new PanelHeader("[yellow]Warnings[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };
            AnsiConsole.Write(warningPanel);
        }
    }

    private void WriteRepositoryInfoResult(RepositoryInfoResult result)
    {
        if (result.Repository == null)
        {
            WriteError("Repository information not available");
            return;
        }

        var panel = new Panel(new Markup($"""
            [bold]Name:[/] {result.Repository.Name}
            [bold]Location:[/] {result.Repository.Location}
            [bold]Type:[/] {result.Repository.Type}
            [bold]Added:[/] {result.Repository.AddedAt:yyyy-MM-dd HH:mm}
            [bold]Scans:[/] {result.ScanCount}
            [bold]Issues:[/] {result.IssueCount}
            [bold]Active Scans:[/] {(result.HasActiveScans ? "[yellow]Yes[/]" : "[green]No[/]")}
            """))
        {
            Header = new PanelHeader("Repository Information"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);

        if (result.Structure != null)
        {
            WriteLine();
            WriteInfo("File structure available (use detailed view for more info)");
        }
    }

    private void WriteScanStartResult(ScanStartResult result)
    {
        var statusColor = result.Success ? Color.Green : Color.Red;
        var statusIcon = result.Success ? "✓" : "✗";
        
        var panel = new Panel(new Markup($"""
            [bold]Scan ID:[/] {result.ScanId}
            [bold]Repository:[/] {result.RepositoryName} ({result.RepositoryId})
            [bold]Status:[/] {result.Status}
            [bold]Files to Process:[/] {result.FilesTotal:N0}
            """))
        {
            Header = new PanelHeader($"[{(result.Success ? "green" : "red")}]{statusIcon} Scan Started[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(statusColor)
        };

        AnsiConsole.Write(panel);
    }

    private void WriteScanStatusResult(ScanStatusResult result)
    {
        if (!result.Scans.Any())
        {
            WriteInfo("No active scans found");
            return;
        }

        var table = new Table();
        table.Title = new TableTitle("Active Scans");
        table.BorderColor(Color.Blue);
        table.AddColumn("Scan ID");
        table.AddColumn("Repository");
        table.AddColumn("Status");
        table.AddColumn("Progress");
        table.AddColumn("Files");
        table.AddColumn("Issues");
        table.AddColumn("Started");
        table.AddColumn("ETA");

        foreach (var scan in result.Scans)
        {
            var progressBar = $"[{GetProgressColor(scan.Progress)}]{scan.Progress:P0}[/]";
            var filesProgress = $"{scan.FilesProcessed:N0}/{scan.FilesTotal:N0}";
            
            table.AddRow(
                scan.ScanId,
                scan.RepositoryId,
                GetStatusMarkup(scan.Status),
                progressBar,
                filesProgress,
                scan.IssuesFound.ToString("N0"),
                scan.StartedAt.ToString("HH:mm:ss"),
                scan.EstimatedCompletion?.ToString("HH:mm:ss") ?? "Unknown"
            );
        }

        AnsiConsole.Write(table);
    }

    private void WriteScanResultsResult(ScanResultsResult result)
    {
        if (!result.Issues.Any())
        {
            WriteInfo($"No issues found for scan {result.ScanId}");
            return;
        }

        var panel = new Panel(new Markup($"""
            [bold]Scan ID:[/] {result.ScanId}
            [bold]Total Issues:[/] {result.TotalIssues:N0}
            [bold]Filtered Issues:[/] {result.FilteredIssues:N0}
            """))
        {
            Header = new PanelHeader("Scan Results"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);
        WriteLine();
        WriteIssues(result.Issues);

        if (result.TotalIssues != result.FilteredIssues)
        {
            WriteLine();
            WriteInfo($"Showing {result.FilteredIssues} of {result.TotalIssues} total issues");
        }
    }

    private void WriteScanOperationResult(ScanOperationResult result)
    {
        var statusColor = result.Success ? "green" : "red";
        var statusIcon = result.Success ? "✓" : "✗";
        
        AnsiConsole.MarkupLine($"[{statusColor}]{statusIcon}[/] Scan {result.Operation}: [{statusColor}]{(result.Success ? "SUCCESS" : "FAILED")}[/]");
        AnsiConsole.MarkupLine($"Scan ID: {result.ScanId}");
    }

    private void WriteScanListResult(ScanListResult result)
    {
        if (!result.Scans.Any())
        {
            WriteInfo("No scans found");
            return;
        }

        var table = new Table();
        table.Title = new TableTitle($"Scans ({result.TotalCount:N0} total)");
        table.BorderColor(Color.Grey);
        table.AddColumn("Scan ID");
        table.AddColumn("Repository");
        table.AddColumn("Status");
        table.AddColumn("Progress");
        table.AddColumn("Files");
        table.AddColumn("Issues");
        table.AddColumn("Started");

        foreach (var scan in result.Scans)
        {
            var progressBar = $"[{GetProgressColor(scan.Progress)}]{scan.Progress:P0}[/]";
            var filesProgress = $"{scan.FilesProcessed:N0}/{scan.FilesTotal:N0}";
            
            table.AddRow(
                scan.ScanId,
                scan.RepositoryId,
                GetStatusMarkup(scan.Status),
                progressBar,
                filesProgress,
                scan.IssuesFound.ToString("N0"),
                scan.StartedAt.ToString("yyyy-MM-dd HH:mm")
            );
        }

        AnsiConsole.Write(table);
    }

    private void WriteGraphScanResult(GraphScanResult result)
    {
        var statusColor = result.Success ? Color.Green : Color.Red;
        var statusIcon = result.Success ? "✓" : "✗";
        
        var panel = new Panel(new Markup($"""
            [bold]Scan ID:[/] {result.ScanId}
            [bold]Repository:[/] {result.RepositoryName} ({result.RepositoryId})
            [bold]Status:[/] {result.Status}
            [bold]Files Processed:[/] {result.FilesProcessed:N0}
            [bold]Nodes Created:[/] {result.NodesCreated:N0}
            [bold]Relationships Created:[/] {result.RelationshipsCreated:N0}
            """))
        {
            Header = new PanelHeader($"[{(result.Success ? "green" : "red")}]{statusIcon} Graph Scan Started[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(statusColor)
        };

        AnsiConsole.Write(panel);
    }

    private void WriteGraphQueryResult(GraphQueryResult result)
    {
        if (!result.Results.Any())
        {
            WriteInfo($"No results found for query: {result.Query}");
            return;
        }

        var panel = new Panel(new Markup($"""
            [bold]Query:[/] {result.Query}
            [bold]Results:[/] {result.Results.Count:N0}
            """))
        {
            Header = new PanelHeader("Graph Query Results"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);
        WriteLine();

        var table = new Table();
        table.BorderColor(Color.Grey);
        table.AddColumn("Type");
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Properties");

        foreach (var item in result.Results)
        {
            var properties = item.Properties.Any() 
                ? string.Join(", ", item.Properties.Take(3).Select(p => $"{p.Key}={p.Value}"))
                : "None";
            
            if (item.Properties.Count > 3)
                properties += $" (+{item.Properties.Count - 3} more)";

            table.AddRow(
                item.Type,
                item.Id[..Math.Min(12, item.Id.Length)] + (item.Id.Length > 12 ? "..." : ""),
                item.Name,
                properties
            );
        }

        AnsiConsole.Write(table);
    }

    private void WriteGraphMetricsResult(GraphMetricsResult result)
    {
        var metrics = new GraphMetrics
        {
            TotalNodes = result.Metrics.VertexCount,
            TotalRelationships = result.Metrics.EdgeCount,
            NodesByType = result.Metrics.VertexCountByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            RelationshipsByType = result.Metrics.EdgeCountByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            AverageComplexity = result.Metrics.AverageComplexity,
            AverageCoupling = result.Metrics.AverageCoupling
        };
        
        WriteGraphMetrics(metrics);
    }

    private void WriteGraphAnalysisResult(GraphAnalysisResult result)
    {
        var panel = new Panel(new Markup($"""
            [bold]Repository:[/] {result.RepositoryId}
            [bold]Analysis Type:[/] {result.AnalysisType}
            [bold]Results:[/] {result.Results.Count:N0}
            """))
        {
            Header = new PanelHeader("Graph Analysis Results"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);

        if (result.Results.Any())
        {
            WriteLine();
            var tree = new Tree("Analysis Results");

            var groupedResults = result.Results.GroupBy(r => r.Severity);
            foreach (var group in groupedResults.OrderBy(g => GetSeverityOrder(g.Key)))
            {
                var severityColor = GetSeverityColor(group.Key);
                var severityNode = tree.AddNode($"[{severityColor}]{group.Key} ({group.Count()})[/]");

                foreach (var item in group)
                {
                    var itemNode = severityNode.AddNode($"[bold]{item.Name}[/] (Score: {item.Score:F2})");
                    itemNode.AddNode($"Type: {item.Type}");
                    itemNode.AddNode($"Description: {item.Description}");
                    
                    if (item.Recommendations.Any())
                    {
                        var recNode = itemNode.AddNode("Recommendations:");
                        foreach (var rec in item.Recommendations)
                        {
                            recNode.AddNode($"• {rec}");
                        }
                    }
                }
            }

            AnsiConsole.Write(tree);
        }
    }

    private void WriteGraphExportResult(GraphExportResult result)
    {
        var statusColor = result.Success ? "green" : "red";
        var statusIcon = result.Success ? "✓" : "✗";
        
        var panel = new Panel(new Markup($"""
            [bold]Repository:[/] {result.RepositoryId}
            [bold]Format:[/] {result.ExportFormat}
            [bold]Output File:[/] {result.OutputFile}
            [bold]File Size:[/] {result.FileSizeBytes:N0} bytes
            """))
        {
            Header = new PanelHeader($"[{statusColor}]{statusIcon} Graph Export Complete[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(result.Success ? Color.Green : Color.Red)
        };

        AnsiConsole.Write(panel);
    }

    private void WriteGraphVisualizationResult(GraphVisualizationResult result)
    {
        var statusColor = result.Success ? "green" : "red";
        var statusIcon = result.Success ? "✓" : "✗";
        
        var panel = new Panel(new Markup($"""
            [bold]Repository:[/] {result.RepositoryId}
            [bold]Output File:[/] {result.OutputFile}
            """))
        {
            Header = new PanelHeader($"[{statusColor}]{statusIcon} Graph Visualization Complete[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(result.Success ? Color.Green : Color.Red)
        };

        AnsiConsole.Write(panel);
    }

    private void WriteIssueDetailResult(IssueDetailResult result)
    {
        if (result.Issue == null)
        {
            WriteError("Issue details not available");
            return;
        }

        var issue = result.Issue;
        var severityColor = GetSeverityColor(issue.Severity);
        
        var panel = new Panel(new Markup($"""
            [bold]Type:[/] {issue.Type}
            [bold]Severity:[/] [{severityColor}]{issue.Severity}[/]
            [bold]File:[/] {issue.File}:{issue.Line}:{issue.Column}
            [bold]Confidence:[/] {issue.ConfidenceScore:P0}
            [bold]Status:[/] {issue.Status}
            [bold]Created:[/] {issue.CreatedAt:yyyy-MM-dd HH:mm}
            
            [bold]Message:[/]
            {issue.Message}
            
            [bold]Description:[/]
            {issue.Description}
            """))
        {
            Header = new PanelHeader($"Issue Details - {issue.Id}"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);

        if (!string.IsNullOrEmpty(result.SourceCode))
        {
            WriteLine();
            var codePanel = new Panel(result.SourceCode)
            {
                Header = new PanelHeader("Source Code Context"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey)
            };
            AnsiConsole.Write(codePanel);
        }

        if (issue.Recommendations.Any())
        {
            WriteLine();
            var recPanel = new Panel(string.Join("\n", issue.Recommendations.Select(r => $"• {r}")))
            {
                Header = new PanelHeader("Recommendations"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green)
            };
            AnsiConsole.Write(recPanel);
        }

        if (result.RelatedIssues.Any())
        {
            WriteLine();
            WriteInfo($"Related issues: {string.Join(", ", result.RelatedIssues)}");
        }
    }

    private void WriteIssueUpdateResult(IssueUpdateResult result)
    {
        var statusColor = result.Success ? "green" : "red";
        var statusIcon = result.Success ? "✓" : "✗";
        
        AnsiConsole.MarkupLine($"[{statusColor}]{statusIcon}[/] Issue {result.Operation}: [{statusColor}]{(result.Success ? "SUCCESS" : "FAILED")}[/]");
        AnsiConsole.MarkupLine($"Issue ID: {result.IssueId}");
    }

    private void WriteIssueExportResult(IssueExportResult result)
    {
        var statusColor = result.Success ? "green" : "red";
        var statusIcon = result.Success ? "✓" : "✗";
        
        var panel = new Panel(new Markup($"""
            [bold]Format:[/] {result.ExportFormat}
            [bold]Output File:[/] {result.OutputFile}
            [bold]Issues Exported:[/] {result.IssuesExported:N0}
            """))
        {
            Header = new PanelHeader($"[{statusColor}]{statusIcon} Issues Export Complete[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(result.Success ? Color.Green : Color.Red)
        };

        AnsiConsole.Write(panel);
    }

    private void WriteIssueStatsResult(IssueStatsResult result)
    {
        var panel = new Panel(new Markup($"""
            [bold]Total Issues:[/] {result.TotalIssues:N0}
            """))
        {
            Header = new PanelHeader("Issue Statistics"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue)
        };

        AnsiConsole.Write(panel);

        if (result.IssuesBySeverity.Any())
        {
            WriteLine();
            var severityChart = new BarChart()
                .Width(60)
                .Label("Issues by Severity");

            foreach (var kvp in result.IssuesBySeverity.OrderBy(x => GetSeverityOrder(x.Key)))
            {
                var color = GetSeverityColor(kvp.Key) switch
                {
                    "red" => Color.Red,
                    "orange1" => Color.Orange1,
                    "yellow" => Color.Yellow,
                    "blue" => Color.Blue,
                    "green" => Color.Green,
                    _ => Color.White
                };
                severityChart.AddItem(kvp.Key, kvp.Value, color);
            }

            AnsiConsole.Write(severityChart);
        }

        if (result.IssuesByCategory.Any())
        {
            WriteLine();
            var categoryChart = new BarChart()
                .Width(60)
                .Label("Issues by Category");

            foreach (var kvp in result.IssuesByCategory.OrderByDescending(x => x.Value).Take(10))
            {
                categoryChart.AddItem(kvp.Key, kvp.Value, Color.Blue);
            }

            AnsiConsole.Write(categoryChart);
        }

        if (result.IssuesByFile.Any())
        {
            WriteLine();
            var table = new Table();
            table.Title = new TableTitle("Top Files by Issue Count");
            table.BorderColor(Color.Grey);
            table.AddColumn("File");
            table.AddColumn("Issues");

            foreach (var kvp in result.IssuesByFile.OrderByDescending(x => x.Value).Take(10))
            {
                table.AddRow(kvp.Key, kvp.Value.ToString("N0"));
            }

            AnsiConsole.Write(table);
        }
    }

    private static string GetProgressColor(double progress) => progress switch
    {
        >= 0.8 => "green",
        >= 0.5 => "yellow",
        _ => "red"
    };

    private static string GetStatusMarkup(string status) => status.ToLowerInvariant() switch
    {
        "completed" => "[green]Completed[/]",
        "failed" => "[red]Failed[/]",
        "canceled" => "[orange1]Canceled[/]",
        "paused" => "[yellow]Paused[/]",
        "inprogress" => "[blue]In Progress[/]",
        "pending" => "[grey]Pending[/]",
        _ => status
    };

    private void WriteLlmProvidersResult(LlmProvidersResult result)
    {
        if (!result.Providers.Any())
        {
            WriteInfo("No LLM providers supported");
            return;
        }

        var table = new Table();
        table.Title = new TableTitle("Supported LLM Providers");
        table.BorderColor(Color.Blue);
        table.AddColumn("Provider");
        table.AddColumn("Type");
        table.AddColumn("Default Endpoint");
        table.AddColumn("Description");

        foreach (var provider in result.Providers)
        {
            var description = provider.Type switch
            {
                "Ollama" => "Local LLM runtime for open models",
                "LmStudio" => "Desktop app for local LLM inference",
                _ => "AI model provider"
            };
            
            table.AddRow(
                provider.Name,
                provider.Type,
                provider.Endpoint,
                description
            );
        }

        AnsiConsole.Write(table);
        WriteLine();
        WriteInfo("Use 'codivus llm test --provider <type>' to check connectivity");
    }

    private void WriteLlmModelsResult(LlmModelsResult result)
    {
        var panel = new Panel(new Markup($"""
            [bold]Provider:[/] {result.Provider}
            [bold]Status:[/] {(result.IsProviderAvailable ? "[green]Available[/]" : "[red]Not Available[/]")}
            [bold]Model Count:[/] {result.Models.Count}
            """))
        {
            Header = new PanelHeader("LLM Models"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(result.IsProviderAvailable ? Color.Green : Color.Red)
        };

        AnsiConsole.Write(panel);

        if (result.Models.Any())
        {
            WriteLine();
            var tree = new Tree("Available Models");
            foreach (var model in result.Models)
            {
                tree.AddNode($"[cyan]{model}[/]");
            }
            AnsiConsole.Write(tree);
        }
        else
        {
            WriteLine();
            WriteWarning($"No models available. Make sure {result.Provider} is running.");
        }
    }

    private void WriteLlmTestResult(LlmTestResult result)
    {
        var statusColor = result.IsAvailable ? Color.Green : Color.Red;
        var statusIcon = result.IsAvailable ? "✓" : "✗";
        
        var panel = new Panel(new Markup($"""
            [bold]Provider:[/] {result.Provider}
            [bold]Status:[/] [{(result.IsAvailable ? "green" : "red")}]{result.Status}[/]
            [bold]Message:[/] {result.Message}
            """))
        {
            Header = new PanelHeader($"[{(result.IsAvailable ? "green" : "red")}]{statusIcon} Connectivity Test[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(statusColor)
        };

        AnsiConsole.Write(panel);

        if (result.AvailableModels.Any())
        {
            WriteLine();
            WriteInfo($"Sample models: {string.Join(", ", result.AvailableModels)}");
            if (result.AvailableModels.Count < result.AvailableModels.Count)
            {
                WriteInfo("(showing first 5 models)");
            }
        }
    }
}