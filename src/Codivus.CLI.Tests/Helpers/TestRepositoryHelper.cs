using Codivus.Core.Models;

namespace Codivus.CLI.Tests.Helpers;

public static class TestRepositoryHelper
{
    public static string CreateTestRepository(string basePath, string name = "test-repo")
    {
        var repoPath = Path.Combine(basePath, name);
        Directory.CreateDirectory(repoPath);

        // Create a simple C# project structure
        CreateTestFiles(repoPath);
        
        return repoPath;
    }

    private static void CreateTestFiles(string repoPath)
    {
        // Create src directory
        var srcPath = Path.Combine(repoPath, "src");
        Directory.CreateDirectory(srcPath);

        // Create a simple C# class
        var classContent = @"using System;
using System.Collections.Generic;

namespace TestProject
{
    public class Calculator
    {
        private readonly List<double> _history = new();

        public double Add(double a, double b)
        {
            var result = a + b;
            _history.Add(result);
            return result;
        }

        public double Subtract(double a, double b)
        {
            var result = a - b;
            _history.Add(result);
            return result;
        }

        public double Multiply(double a, double b)
        {
            var result = a * b;
            _history.Add(result);
            return result;
        }

        public double Divide(double a, double b)
        {
            if (b == 0)
                throw new ArgumentException(""Division by zero"");
            
            var result = a / b;
            _history.Add(result);
            return result;
        }

        public List<double> GetHistory() => new(_history);
        
        public void ClearHistory() => _history.Clear();
    }

    public interface IDataService
    {
        Task<string> GetDataAsync(string id);
        Task SaveDataAsync(string id, string data);
    }

    public class DataService : IDataService
    {
        private readonly Dictionary<string, string> _storage = new();

        public async Task<string> GetDataAsync(string id)
        {
            await Task.Delay(10); // Simulate async operation
            return _storage.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException($""Data with id '{id}' not found"");
        }

        public async Task SaveDataAsync(string id, string data)
        {
            await Task.Delay(10); // Simulate async operation
            _storage[id] = data;
        }
    }
}";

        File.WriteAllText(Path.Combine(srcPath, "Calculator.cs"), classContent);

        // Create a test file
        var testContent = @"using System;
using Xunit;
using TestProject;

namespace TestProject.Tests
{
    public class CalculatorTests
    {
        [Fact]
        public void Add_TwoNumbers_ReturnsSum()
        {
            var calculator = new Calculator();
            var result = calculator.Add(2, 3);
            Assert.Equal(5, result);
        }

        [Fact]
        public void Divide_ByZero_ThrowsException()
        {
            var calculator = new Calculator();
            Assert.Throws<ArgumentException>(() => calculator.Divide(10, 0));
        }

        [Theory]
        [InlineData(10, 5, 2)]
        [InlineData(20, 4, 5)]
        public void Divide_ValidNumbers_ReturnsQuotient(double a, double b, double expected)
        {
            var calculator = new Calculator();
            var result = calculator.Divide(a, b);
            Assert.Equal(expected, result);
        }
    }
}";

        var testPath = Path.Combine(repoPath, "tests");
        Directory.CreateDirectory(testPath);
        File.WriteAllText(Path.Combine(testPath, "CalculatorTests.cs"), testContent);

        // Create configuration files
        var projectContent = @"{
  ""name"": ""TestProject"",
  ""version"": ""1.0.0"",
  ""description"": ""A test project for Codivus CLI integration tests"",
  ""main"": ""Calculator.cs"",
  ""scripts"": {
    ""test"": ""dotnet test"",
    ""build"": ""dotnet build""
  }
}";

        File.WriteAllText(Path.Combine(repoPath, "project.json"), projectContent);

        // Create README
        var readmeContent = @"# Test Project

This is a test project for Codivus CLI integration tests.

## Features

- Calculator class with basic arithmetic operations
- Data service with async operations
- Unit tests with xUnit

## Issues to detect

- Potential division by zero (handled)
- Dictionary access without null checks (handled)
- Memory usage with history collection
- Async operations without ConfigureAwait
";

        File.WriteAllText(Path.Combine(repoPath, "README.md"), readmeContent);

        // Create .gitignore
        var gitignoreContent = @"bin/
obj/
.vs/
*.user
*.suo
TestResults/
.coverage
coverage.xml
";

        File.WriteAllText(Path.Combine(repoPath, ".gitignore"), gitignoreContent);
    }

    public static Repository CreateRepositoryModel(string path, string name = "test-repo")
    {
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test repository for integration tests",
            Location = path,
            Type = RepositoryType.Local,
            AddedAt = DateTime.UtcNow,
            LastScanAt = null
        };
    }
}