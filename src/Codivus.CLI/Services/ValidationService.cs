using System.Text.RegularExpressions;
using Codivus.CLI.Infrastructure;
using Codivus.CLI.Models;

namespace Codivus.CLI.Services;

public class ValidationService : IValidationService
{
    private static readonly string[] SupportedFormats = { "console", "json", "xml", "csv", "html" };
    private static readonly Regex UrlRegex = new(@"^https?://[^\s/$.?#].[^\s]*$", RegexOptions.IgnoreCase);

    public ValidationResult ValidatePath(string path, bool mustExist = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Invalid("Path cannot be empty");
        }

        try
        {
            // Check for invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (path.Any(c => invalidChars.Contains(c)))
            {
                return ValidationResult.Invalid("Path contains invalid characters");
            }

            // Check if path exists (if required)
            if (mustExist)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    return ValidationResult.Invalid($"Path does not exist: {path}");
                }
            }
            else
            {
                // Check if parent directory exists for new files
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    return ValidationResult.Invalid($"Parent directory does not exist: {directory}");
                }
            }

            return ValidationResult.Valid();
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid($"Invalid path: {ex.Message}");
        }
    }

    public ValidationResult ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return ValidationResult.Invalid("URL cannot be empty");
        }

        if (!UrlRegex.IsMatch(url))
        {
            return ValidationResult.Invalid("Invalid URL format");
        }

        try
        {
            var uri = new Uri(url);
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                return ValidationResult.Invalid("URL must use http or https protocol");
            }

            return ValidationResult.Valid();
        }
        catch (UriFormatException)
        {
            return ValidationResult.Invalid("Invalid URL format");
        }
    }

    public ValidationResult ValidateConfiguration(object config)
    {
        if (config == null)
        {
            return ValidationResult.Invalid("Configuration cannot be null");
        }

        var result = ValidationResult.Valid();

        // Perform basic validation based on configuration type
        switch (config)
        {
            case ScanOptions scanOptions:
                result = ValidateScanOptions(scanOptions);
                break;
            case GraphOptions graphOptions:
                result = ValidateGraphOptions(graphOptions);
                break;
            // Add more configuration types as needed
        }

        return result;
    }

    public ValidationResult ValidateOutputFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return ValidationResult.Invalid("Output format cannot be empty");
        }

        if (!SupportedFormats.Contains(format.ToLowerInvariant()))
        {
            return ValidationResult.Invalid($"Unsupported output format: {format}. Supported formats: {string.Join(", ", SupportedFormats)}");
        }

        return ValidationResult.Valid();
    }

    private ValidationResult ValidateScanOptions(ScanOptions options)
    {
        var result = ValidationResult.Valid();

        // Validate path or repository URL
        if (string.IsNullOrEmpty(options.Path) && string.IsNullOrEmpty(options.RepositoryUrl))
        {
            result.Errors.Add("Either path or repository URL must be specified");
            result.IsValid = false;
        }

        // Validate path if provided
        if (!string.IsNullOrEmpty(options.Path))
        {
            var pathValidation = ValidatePath(options.Path);
            if (!pathValidation.IsValid)
            {
                result.Errors.AddRange(pathValidation.Errors);
                result.IsValid = false;
            }
        }

        // Validate repository URL if provided
        if (!string.IsNullOrEmpty(options.RepositoryUrl))
        {
            var urlValidation = ValidateUrl(options.RepositoryUrl);
            if (!urlValidation.IsValid)
            {
                result.Errors.AddRange(urlValidation.Errors);
                result.IsValid = false;
            }
        }

        // Validate output format
        var formatValidation = ValidateOutputFormat(options.OutputFormat);
        if (!formatValidation.IsValid)
        {
            result.Errors.AddRange(formatValidation.Errors);
            result.IsValid = false;
        }

        // Validate output file if provided
        if (!string.IsNullOrEmpty(options.OutputFile))
        {
            var outputValidation = ValidatePath(options.OutputFile, false);
            if (!outputValidation.IsValid)
            {
                result.Errors.AddRange(outputValidation.Errors);
                result.IsValid = false;
            }
        }

        // Validate file patterns
        if (options.FilePatterns.Any())
        {
            foreach (var pattern in options.FilePatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    result.Warnings.Add("Empty file pattern found");
                }
            }
        }

        return result;
    }

    private ValidationResult ValidateGraphOptions(GraphOptions options)
    {
        var result = ValidationResult.Valid();

        // Validate output format
        var formatValidation = ValidateOutputFormat(options.OutputFormat);
        if (!formatValidation.IsValid)
        {
            result.Errors.AddRange(formatValidation.Errors);
            result.IsValid = false;
        }

        // Validate max depth
        if (options.MaxDepth < 1 || options.MaxDepth > 10)
        {
            result.Warnings.Add("Max depth should be between 1 and 10 for optimal performance");
        }

        // Validate output file if provided
        if (!string.IsNullOrEmpty(options.OutputFile))
        {
            var outputValidation = ValidatePath(options.OutputFile, false);
            if (!outputValidation.IsValid)
            {
                result.Errors.AddRange(outputValidation.Errors);
                result.IsValid = false;
            }
        }

        return result;
    }
}