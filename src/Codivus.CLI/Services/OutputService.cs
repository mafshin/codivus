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
            case ScanResult scanResult:
                WriteScanResult(scanResult);
                break;
            case IEnumerable<IssueInfo> issues:
                WriteIssues(issues);
                break;
            case GraphMetrics metrics:
                WriteGraphMetrics(metrics);
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
}