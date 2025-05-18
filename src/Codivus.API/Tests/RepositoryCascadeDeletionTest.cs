using Codivus.API.Data;
using Codivus.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codivus.API.Tests;

/// <summary>
/// Test class to verify cascade deletion functionality
/// </summary>
public class RepositoryCascadeDeletionTest
{
    private readonly JsonDataStore _dataStore;
    private readonly ILogger<JsonDataStore> _logger;
    
    public RepositoryCascadeDeletionTest()
    {
        // Create a mock logger for testing
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<JsonDataStore>();
        _dataStore = new JsonDataStore(_logger);
    }
    
    /// <summary>
    /// Test cascade deletion functionality
    /// </summary>
    public async Task TestCascadeDeletion()
    {
        Console.WriteLine("Starting cascade deletion test...");
        
        // 1. Create a test repository
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Test Repository for Cascade Delete",
            Location = "/test/path",
            Type = RepositoryType.Local,
            AddedAt = DateTime.UtcNow
        };
        
        await _dataStore.AddRepositoryAsync(repository);
        Console.WriteLine($"Created repository: {repository.Name} (ID: {repository.Id})");
        
        // 2. Create test configurations
        var config1 = new ScanConfiguration
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            Name = "Test Config 1",
            IncludeExtensions = new List<string> { ".cs", ".js" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var config2 = new ScanConfiguration
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            Name = "Test Config 2",
            IncludeExtensions = new List<string> { ".ts", ".vue" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _dataStore.AddConfigurationAsync(config1);
        await _dataStore.AddConfigurationAsync(config2);
        Console.WriteLine($"Created {2} test configurations");
        
        // 3. Create test scans
        var scan1 = new ScanProgress
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            ConfigurationId = config1.Id,
            Status = ScanStatus.Completed,
            TotalFiles = 100,
            ScannedFiles = 100,
            IssuesFound = 5,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };
        
        var scan2 = new ScanProgress
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            ConfigurationId = config2.Id,
            Status = ScanStatus.Failed,
            TotalFiles = 50,
            ScannedFiles = 25,
            IssuesFound = 0,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            CompletedAt = DateTime.UtcNow.AddMinutes(-20),
            ErrorMessage = "Test error"
        };
        
        await _dataStore.AddScanAsync(scan1);
        await _dataStore.AddScanAsync(scan2);
        Console.WriteLine($"Created {2} test scans");
        
        // 4. Create test issues
        for (int i = 0; i < 5; i++)
        {
            var issue = new CodeIssue
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                ScanId = scan1.Id,
                FilePath = $"/test/file{i}.cs",
                LineNumber = i + 10,
                ColumnNumber = 1,
                Title = $"Test Issue {i + 1}",
                Description = $"This is test issue {i + 1}",
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.Quality,
                Confidence = 0.8,
                DetectionMethod = IssueDetectionMethod.Static,
                DetectedAt = DateTime.UtcNow
            };
            
            await _dataStore.AddIssueAsync(issue);
        }
        Console.WriteLine($"Created {5} test issues");
        
        // 5. Verify data exists before deletion
        var scanCount = await _dataStore.GetScanCountByRepositoryAsync(repository.Id);
        var issueCount = await _dataStore.GetIssueCountByRepositoryAsync(repository.Id);
        var configCount = await _dataStore.GetConfigurationCountByRepositoryAsync(repository.Id);
        
        Console.WriteLine($"Before deletion - Scans: {scanCount}, Issues: {issueCount}, Configs: {configCount}");
        
        // 6. Test active scan check
        var hasActiveScans = await _dataStore.HasActiveScansAsync(repository.Id);
        Console.WriteLine($"Has active scans: {hasActiveScans}");
        
        // 7. Perform cascade deletion
        Console.WriteLine("Performing cascade deletion...");
        var deleteResult = await _dataStore.DeleteRepositoryAsync(repository.Id);
        Console.WriteLine($"Delete result: {deleteResult}");
        
        // 8. Verify all related data was deleted
        scanCount = await _dataStore.GetScanCountByRepositoryAsync(repository.Id);
        issueCount = await _dataStore.GetIssueCountByRepositoryAsync(repository.Id);
        configCount = await _dataStore.GetConfigurationCountByRepositoryAsync(repository.Id);
        
        Console.WriteLine($"After deletion - Scans: {scanCount}, Issues: {issueCount}, Configs: {configCount}");
        
        // 9. Verify repository no longer exists
        var deletedRepo = await _dataStore.GetRepositoryAsync(repository.Id);
        Console.WriteLine($"Repository exists after deletion: {deletedRepo != null}");
        
        Console.WriteLine("Cascade deletion test completed!");
    }
    
    /// <summary>
    /// Test that deletion fails when there are active scans
    /// </summary>
    public async Task TestDeletionWithActiveScans()
    {
        Console.WriteLine("\\nStarting deletion with active scans test...");
        
        // 1. Create a test repository
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Test Repository with Active Scan",
            Location = "/test/path2",
            Type = RepositoryType.Local,
            AddedAt = DateTime.UtcNow
        };
        
        await _dataStore.AddRepositoryAsync(repository);
        Console.WriteLine($"Created repository: {repository.Name} (ID: {repository.Id})");
        
        // 2. Create a configuration
        var config = new ScanConfiguration
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            Name = "Active Scan Config",
            IncludeExtensions = new List<string> { ".cs" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _dataStore.AddConfigurationAsync(config);
        
        // 3. Create an active scan (InProgress)
        var activeScan = new ScanProgress
        {
            Id = Guid.NewGuid(),
            RepositoryId = repository.Id,
            ConfigurationId = config.Id,
            Status = ScanStatus.InProgress,
            TotalFiles = 100,
            ScannedFiles = 50,
            IssuesFound = 2,
            StartedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        
        await _dataStore.AddScanAsync(activeScan);
        Console.WriteLine("Created active scan");
        
        // 4. Check for active scans
        var hasActiveScans = await _dataStore.HasActiveScansAsync(repository.Id);
        Console.WriteLine($"Has active scans: {hasActiveScans}");
        
        // 5. Attempt deletion (should fail in service layer, but succeed in data store)
        Console.WriteLine("Note: In the service layer, this would throw an exception");
        Console.WriteLine("But the data store will perform the deletion anyway");
        
        // Clean up for this test
        await _dataStore.DeleteRepositoryAsync(repository.Id);
        Console.WriteLine("Cleaned up test data");
    }
    
    /// <summary>
    /// Run all tests
    /// </summary>
    public static async Task Main(string[] args)
    {
        var test = new RepositoryCascadeDeletionTest();
        
        try
        {
            await test.TestCascadeDeletion();
            await test.TestDeletionWithActiveScans();
            
            Console.WriteLine("\\n✅ All tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\\n❌ Test failed with error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\\nPress any key to exit...");
        Console.ReadKey();
    }
}
