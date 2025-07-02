using Spectre.Console;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;

namespace Codivus.CLI.Services;

public class ProgressService : IProgressService
{
    private ProgressTask? _currentTask;
    private ProgressContext? _currentContext;

    public IProgress<ProgressReport> CreateProgress(string description = "Processing...")
    {
        return new Progress<ProgressReport>(report =>
        {
            ReportProgress(report.Message, report.Percentage);
        });
    }

    public void ReportProgress(string message, double percentage = 0)
    {
        if (_currentTask != null)
        {
            _currentTask.Description = message;
            _currentTask.Value = Math.Max(0, Math.Min(100, percentage));
        }
        else
        {
            // Fallback for when not in a progress context
            AnsiConsole.MarkupLine($"[grey]{message}[/]");
        }
    }

    public void CompleteProgress(string message = "Completed")
    {
        if (_currentTask != null)
        {
            _currentTask.Value = 100;
            _currentTask.Description = message;
        }
    }

    internal void SetCurrentTask(ProgressTask task, ProgressContext context)
    {
        _currentTask = task;
        _currentContext = context;
    }

    internal void ClearCurrentTask()
    {
        _currentTask = null;
        _currentContext = null;
    }
}