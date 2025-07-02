using Codivus.CLI.Models;

namespace Codivus.CLI.Infrastructure;

public interface IOutputService
{
    void WriteSuccess(string message);
    void WriteError(string message);
    void WriteWarning(string message);
    void WriteInfo(string message);
    void WriteDebug(string message);
    void WriteLine(string message = "");
    void WriteTable<T>(IEnumerable<T> items, string title = "");
    void WriteJson(object obj);
    void WriteMarkup(string markup);
    Task WriteResultsAsync<T>(CommandResult<T> result);
    void ShowProgress(Action<IProgress<ProgressReport>> action, string description = "Processing...");
    Task ShowProgressAsync(Func<IProgress<ProgressReport>, Task> action, string description = "Processing...");
    Task<T> ShowProgressAsync<T>(Func<IProgress<ProgressReport>, Task<T>> action, string description = "Processing...");
}

public interface IProgressService
{
    IProgress<ProgressReport> CreateProgress(string description = "Processing...");
    void ReportProgress(string message, double percentage = 0);
    void CompleteProgress(string message = "Completed");
}

public interface IValidationService
{
    ValidationResult ValidatePath(string path, bool mustExist = true);
    ValidationResult ValidateUrl(string url);
    ValidationResult ValidateConfiguration(object config);
    ValidationResult ValidateOutputFormat(string format);
}

public interface IConfigurationService
{
    Task<T?> GetConfigurationAsync<T>(string key) where T : class;
    Task SetConfigurationAsync<T>(string key, T value) where T : class;
    Task<string> GetConfigurationPathAsync();
    Task InitializeConfigurationAsync();
    Task<bool> ConfigurationExistsAsync();
}