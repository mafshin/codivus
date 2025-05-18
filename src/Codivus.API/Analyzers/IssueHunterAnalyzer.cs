using Codivus.Core.Interfaces;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Codivus.API.Analyzers;

public class IssueHunterAnalyzer : IIssueHunterAnalyzer
{
    private readonly ILogger<IssueHunterAnalyzer> _logger;
    private readonly Dictionary<string, List<PatternRule>> _languageRules;

    public IssueHunterAnalyzer(ILogger<IssueHunterAnalyzer> logger)
    {
        _logger = logger;
        _languageRules = InitializeRules();
    }

    public Task<IEnumerable<CodeIssue>> AnalyzeFileAsync(string filePath, string content, Guid repositoryId, ScanConfiguration configuration, Guid scanId)
    {
        try
        {
            var issues = new List<CodeIssue>();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Get language-specific rules
            if (!_languageRules.TryGetValue(extension, out var rules))
            {
                // No rules for this file type
                return Task.FromResult<IEnumerable<CodeIssue>>(issues);
            }

            // Apply category filter if configured
            if (configuration.IncludeCategories.Count > 0)
            {
                rules = rules.Where(r => configuration.IncludeCategories.Contains(r.Category)).ToList();
            }

            // Apply minimum severity filter
            rules = rules.Where(r => r.Severity >= configuration.MinimumSeverity).ToList();

            // Split content into lines for analysis
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Apply rules
            foreach (var rule in rules)
            {
                var regex = new Regex(rule.Pattern, RegexOptions.Compiled | RegexOptions.Multiline);
                
                // Check for matches in the whole content
                var matches = regex.Matches(content);
                foreach (Match match in matches)
                {
                    // Get line number by counting newlines before the match
                    var lineIndex = content.Substring(0, match.Index).Count(c => c == '\n');
                    var lineNumber = lineIndex + 1; // 1-based line numbers
                    
                    // Get column number by finding distance from the last newline
                    var lastNewlineIndex = content.LastIndexOf('\n', match.Index);
                    var columnNumber = lastNewlineIndex >= 0 ? match.Index - lastNewlineIndex : match.Index + 1;
                    
                    // Create a code snippet
                    var snippetStartLine = Math.Max(0, lineIndex - 2);
                    var snippetEndLine = Math.Min(lines.Length - 1, lineIndex + 2);
                    var snippetLines = new List<string>();
                    
                    for (var i = snippetStartLine; i <= snippetEndLine; i++)
                    {
                        snippetLines.Add(lines[i]);
                    }
                    
                    var codeSnippet = string.Join(Environment.NewLine, snippetLines);

                    // Create issue
                    var issue = new CodeIssue
                    {
                        Id = Guid.NewGuid(),
                        RepositoryId = repositoryId,
                        ScanId = scanId,
                        FilePath = filePath,
                        LineNumber = lineNumber,
                        ColumnNumber = columnNumber,
                        LineSpan = match.Length > 0 ? match.Value.Count(c => c == '\n') + 1 : 1,
                        Title = rule.Title,
                        Description = rule.Description,
                        Severity = rule.Severity,
                        Category = rule.Category,
                        Confidence = rule.Confidence,
                        CodeSnippet = codeSnippet,
                        SuggestedFix = rule.SuggestedFix,
                        DetectionMethod = IssueDetectionMethod.IssueHunter,
                        DetectedAt = DateTime.UtcNow
                    };
                    
                    issues.Add(issue);
                }
            }
            
            return Task.FromResult<IEnumerable<CodeIssue>>(issues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file {FilePath}", filePath);
            return Task.FromResult<IEnumerable<CodeIssue>>(Enumerable.Empty<CodeIssue>());
        }
    }

    public IEnumerable<string> GetSupportedExtensions()
    {
        return _languageRules.Keys;
    }

    public IEnumerable<IssueCategory> GetSupportedCategories()
    {
        return Enum.GetValues<IssueCategory>();
    }

    private Dictionary<string, List<PatternRule>> InitializeRules()
    {
        var rules = new Dictionary<string, List<PatternRule>>();
        
        // C# rules
        rules[".cs"] = new List<PatternRule>
        {
            // Security rules
            new PatternRule
            {
                Title = "SQL Injection vulnerability",
                Description = "Potential SQL injection vulnerability detected. Avoid concatenating user input directly into SQL queries.",
                Category = IssueCategory.Security,
                Severity = IssueSeverity.Critical,
                Confidence = 0.8,
                Pattern = @"string\s+sql\s*=.*?\+\s*[^""']",
                SuggestedFix = "Use parameterized queries or an ORM like Entity Framework to prevent SQL injection attacks."
            },
            new PatternRule
            {
                Title = "Hard-coded credentials",
                Description = "Hard-coded credential detected. Credentials should be stored in a secure configuration system, not in source code.",
                Category = IssueCategory.Security,
                Severity = IssueSeverity.Critical,
                Confidence = 0.7,
                Pattern = @"(?:password|passwd|pwd|secret|credentials?|api_?key)\s*=\s*[""'][^""']{8,}[""']",
                SuggestedFix = "Move credentials to a secure configuration system like Azure Key Vault or environment variables."
            },
            
            // Quality rules
            new PatternRule
            {
                Title = "Large method",
                Description = "This method is too large and should be refactored into smaller, more focused methods.",
                Category = IssueCategory.Quality,
                Severity = IssueSeverity.Medium,
                Confidence = 0.9,
                Pattern = @"(?:public|private|protected|internal)(?:\s+static)?\s+\w+\s+\w+\s*\([^)]*\)\s*\{(?:[^{}]*\{[^{}]*\})*[^{}]*\{[^{}]*\{[^{}]*\}[^{}]*\}[^{}]*\}",
                SuggestedFix = "Refactor the method into smaller, more focused methods with single responsibilities."
            },
            new PatternRule
            {
                Title = "Magic number",
                Description = "Magic number detected. Use named constants to improve readability and maintainability.",
                Category = IssueCategory.Quality,
                Severity = IssueSeverity.Low,
                Confidence = 0.6,
                Pattern = @"[=<>]\s*(?!0|1|2|-1)[0-9]{2,}",
                SuggestedFix = "Replace the magic number with a named constant to improve code readability."
            },
            
            // Performance rules
            new PatternRule
            {
                Title = "Inefficient string concatenation in loop",
                Description = "String concatenation in a loop can be inefficient. Consider using StringBuilder.",
                Category = IssueCategory.Performance,
                Severity = IssueSeverity.Medium,
                Confidence = 0.8,
                Pattern = @"for\s*\(.*\)\s*\{[^}]*\s+\w+\s*\+=[^}]*\}",
                SuggestedFix = "Use StringBuilder instead of string concatenation inside loops to improve performance."
            }
        };
        
        // JavaScript/TypeScript rules
        var jsRules = new List<PatternRule>
        {
            // Security rules
            new PatternRule
            {
                Title = "Potential XSS vulnerability",
                Description = "Potential Cross-Site Scripting (XSS) vulnerability detected. Avoid directly inserting user input into the DOM.",
                Category = IssueCategory.Security,
                Severity = IssueSeverity.Critical,
                Confidence = 0.8,
                Pattern = @"\.innerHTML\s*=\s*[^""']",
                SuggestedFix = "Use textContent instead of innerHTML, or sanitize input with a library like DOMPurify."
            },
            new PatternRule
            {
                Title = "Hard-coded credentials",
                Description = "Hard-coded credential detected. Credentials should not be stored in source code.",
                Category = IssueCategory.Security,
                Severity = IssueSeverity.Critical,
                Confidence = 0.7,
                Pattern = @"(?:password|passwd|pwd|secret|credentials?|api_?key)\s*=\s*[""'][^""']{8,}[""']",
                SuggestedFix = "Move credentials to environment variables or a secure configuration system."
            },
            
            // Quality rules
            new PatternRule
            {
                Title = "Console.log statement",
                Description = "Console.log statement found. These should be removed in production code.",
                Category = IssueCategory.Quality,
                Severity = IssueSeverity.Low,
                Confidence = 0.9,
                Pattern = @"console\.log\(",
                SuggestedFix = "Replace console.log with a proper logging system or remove it in production code."
            },
            
            // Performance rules
            new PatternRule
            {
                Title = "Array forEach with async function",
                Description = "Using forEach with async functions doesn't work as expected. Use for...of loop instead.",
                Category = IssueCategory.Performance,
                Severity = IssueSeverity.Medium,
                Confidence = 0.8,
                Pattern = @"\.forEach\(\s*async\s+",
                SuggestedFix = "Replace forEach with for...of loop when using async functions to ensure proper async execution."
            }
        };
        
        rules[".js"] = jsRules;
        rules[".ts"] = jsRules;
        rules[".jsx"] = jsRules;
        rules[".tsx"] = jsRules;
        rules[".vue"] = jsRules;
        
        return rules;
    }

    private class PatternRule
    {
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required IssueCategory Category { get; set; }
        public required IssueSeverity Severity { get; set; }
        public required double Confidence { get; set; }
        public required string Pattern { get; set; }
        public string? SuggestedFix { get; set; }
    }
}
