using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services
{
    /// <summary>
    /// File system watcher service for detecting repository changes
    /// </summary>
    public class FileWatcherService : IFileWatcherService, IDisposable
    {
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers;
        private readonly ConcurrentDictionary<string, RepositoryWatcherInfo> _repositoryInfo;
        private readonly ConcurrentDictionary<string, DateTime> _recentEvents;
        private readonly FileWatcherOptions _options;
        private readonly ILogger<FileWatcherService> _logger;
        private readonly Timer _cleanupTimer;
        private long _totalEventsProcessed;
        private DateTime _lastEventTime;
        private bool _disposed;

        public event EventHandler<FileChangeEventArgs>? FileChanged;
        public event EventHandler<FileChangeEventArgs>? FileCreated;
        public event EventHandler<FileChangeEventArgs>? FileDeleted;
        public event EventHandler<FileRenameEventArgs>? FileRenamed;

        public FileWatcherService(
            IOptions<FileWatcherOptions> options,
            ILogger<FileWatcherService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();
            _repositoryInfo = new ConcurrentDictionary<string, RepositoryWatcherInfo>();
            _recentEvents = new ConcurrentDictionary<string, DateTime>();

            // Cleanup timer to remove old events from tracking
            _cleanupTimer = new Timer(CleanupRecentEvents, null, 
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _logger.LogInformation("File watcher service initialized with debounce {DebounceMs}ms", 
                _options.DebounceDelayMs);
        }

        public async Task StartWatchingAsync(string repositoryId, string repositoryPath, CancellationToken cancellationToken = default)
        {
            if (_watchers.ContainsKey(repositoryId))
            {
                _logger.LogWarning("Repository {RepositoryId} is already being watched", repositoryId);
                return;
            }

            if (!Directory.Exists(repositoryPath))
            {
                _logger.LogError("Repository path {Path} does not exist", repositoryPath);
                throw new DirectoryNotFoundException($"Repository path {repositoryPath} does not exist");
            }

            try
            {
                var watcher = new FileSystemWatcher(repositoryPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | 
                                  NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    Filter = "*.*"
                };

                // Set up event handlers
                watcher.Created += (sender, e) => OnFileEvent(repositoryId, e, FileChangeType.Created);
                watcher.Changed += (sender, e) => OnFileEvent(repositoryId, e, FileChangeType.Modified);
                watcher.Deleted += (sender, e) => OnFileEvent(repositoryId, e, FileChangeType.Deleted);
                watcher.Renamed += (sender, e) => OnFileRenamed(repositoryId, e);
                watcher.Error += (sender, e) => OnWatcherError(repositoryId, e);

                // Start watching
                watcher.EnableRaisingEvents = true;

                _watchers[repositoryId] = watcher;
                _repositoryInfo[repositoryId] = new RepositoryWatcherInfo
                {
                    RepositoryId = repositoryId,
                    Path = repositoryPath,
                    StartedAt = DateTime.UtcNow,
                    IsActive = true,
                    WatchedExtensions = _options.WatchedExtensions
                };

                _logger.LogInformation("Started watching repository {RepositoryId} at {Path}", 
                    repositoryId, repositoryPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start watching repository {RepositoryId} at {Path}", 
                    repositoryId, repositoryPath);
                throw;
            }
        }

        public async Task StopWatchingAsync(string repositoryId)
        {
            if (_watchers.TryRemove(repositoryId, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();

                if (_repositoryInfo.TryGetValue(repositoryId, out var info))
                {
                    info.IsActive = false;
                }

                _logger.LogInformation("Stopped watching repository {RepositoryId}", repositoryId);
            }
        }

        public async Task<IEnumerable<string>> GetWatchedRepositoriesAsync()
        {
            return _repositoryInfo.Keys.ToList();
        }

        public async Task<WatcherStatistics> GetStatisticsAsync()
        {
            var now = DateTime.UtcNow;
            var oneHourAgo = now.AddHours(-1);
            var eventsInLastHour = _recentEvents.Values.Count(t => t > oneHourAgo);

            var eventsByType = new Dictionary<string, int>();
            foreach (FileChangeType type in Enum.GetValues<FileChangeType>())
            {
                eventsByType[type.ToString()] = 0;
            }

            return new WatcherStatistics
            {
                ActiveWatchers = _watchers.Count,
                TotalEventsProcessed = _totalEventsProcessed,
                EventsInLastHour = eventsInLastHour,
                EventsByType = eventsByType,
                WatchedRepositories = new Dictionary<string, RepositoryWatcherInfo>(_repositoryInfo),
                LastEventTime = _lastEventTime
            };
        }

        private void OnFileEvent(string repositoryId, FileSystemEventArgs e, FileChangeType changeType)
        {
            try
            {
                // Filter by extension if configured
                if (_options.WatchedExtensions.Length > 0)
                {
                    var extension = Path.GetExtension(e.FullPath);
                    if (!_options.WatchedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                // Skip excluded patterns
                if (ShouldExcludeFile(e.FullPath))
                {
                    return;
                }

                // Debounce duplicate events
                var eventKey = $"{repositoryId}:{e.FullPath}:{changeType}";
                var now = DateTime.UtcNow;
                
                if (_recentEvents.TryGetValue(eventKey, out var lastEventTime))
                {
                    if (now.Subtract(lastEventTime).TotalMilliseconds < _options.DebounceDelayMs)
                    {
                        return; // Skip duplicate event
                    }
                }

                _recentEvents[eventKey] = now;
                Interlocked.Increment(ref _totalEventsProcessed);
                _lastEventTime = now;

                // Update repository info
                if (_repositoryInfo.TryGetValue(repositoryId, out var repoInfo))
                {
                    repoInfo.EventCount++;
                    repoInfo.LastEventTime = now;
                }

                // Get relative path
                var repoPath = _repositoryInfo[repositoryId].Path;
                var relativePath = Path.GetRelativePath(repoPath, e.FullPath);

                var eventArgs = new FileChangeEventArgs
                {
                    RepositoryId = repositoryId,
                    FilePath = relativePath,
                    FullPath = e.FullPath,
                    Timestamp = now,
                    ChangeType = changeType,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Extension"] = Path.GetExtension(e.FullPath),
                        ["FileName"] = Path.GetFileName(e.FullPath),
                        ["Directory"] = Path.GetDirectoryName(relativePath) ?? string.Empty
                    }
                };

                // Fire appropriate event
                switch (changeType)
                {
                    case FileChangeType.Created:
                        FileCreated?.Invoke(this, eventArgs);
                        break;
                    case FileChangeType.Modified:
                        FileChanged?.Invoke(this, eventArgs);
                        break;
                    case FileChangeType.Deleted:
                        FileDeleted?.Invoke(this, eventArgs);
                        break;
                }

                _logger.LogDebug("File {ChangeType}: {FilePath} in repository {RepositoryId}", 
                    changeType, relativePath, repositoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file event for {FullPath} in repository {RepositoryId}", 
                    e.FullPath, repositoryId);
            }
        }

        private void OnFileRenamed(string repositoryId, RenamedEventArgs e)
        {
            try
            {
                // Filter by extension if configured
                if (_options.WatchedExtensions.Length > 0)
                {
                    var extension = Path.GetExtension(e.FullPath);
                    if (!_options.WatchedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                // Skip excluded patterns
                if (ShouldExcludeFile(e.FullPath) || ShouldExcludeFile(e.OldFullPath))
                {
                    return;
                }

                var now = DateTime.UtcNow;
                Interlocked.Increment(ref _totalEventsProcessed);
                _lastEventTime = now;

                // Update repository info
                if (_repositoryInfo.TryGetValue(repositoryId, out var repoInfo))
                {
                    repoInfo.EventCount++;
                    repoInfo.LastEventTime = now;
                }

                // Get relative paths
                var repoPath = _repositoryInfo[repositoryId].Path;
                var oldRelativePath = Path.GetRelativePath(repoPath, e.OldFullPath);
                var newRelativePath = Path.GetRelativePath(repoPath, e.FullPath);

                var eventArgs = new FileRenameEventArgs
                {
                    RepositoryId = repositoryId,
                    OldFilePath = oldRelativePath,
                    NewFilePath = newRelativePath,
                    OldFullPath = e.OldFullPath,
                    NewFullPath = e.FullPath,
                    Timestamp = now,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Extension"] = Path.GetExtension(e.FullPath),
                        ["OldFileName"] = Path.GetFileName(e.OldFullPath),
                        ["NewFileName"] = Path.GetFileName(e.FullPath)
                    }
                };

                FileRenamed?.Invoke(this, eventArgs);

                _logger.LogDebug("File renamed: {OldPath} -> {NewPath} in repository {RepositoryId}", 
                    oldRelativePath, newRelativePath, repositoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file rename event for {FullPath} in repository {RepositoryId}", 
                    e.FullPath, repositoryId);
            }
        }

        private void OnWatcherError(string repositoryId, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "File watcher error for repository {RepositoryId}", repositoryId);
            
            // Mark repository as inactive
            if (_repositoryInfo.TryGetValue(repositoryId, out var repoInfo))
            {
                repoInfo.IsActive = false;
            }
        }

        private bool ShouldExcludeFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            // Check excluded patterns
            foreach (var pattern in _options.ExcludedPatterns)
            {
                if (fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    directory.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanupRecentEvents(object? state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-_options.DebounceDelayMs / 1000.0 * 2);
                var keysToRemove = _recentEvents
                    .Where(kvp => kvp.Value < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _recentEvents.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} old file events from tracking", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file watcher cleanup");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Stop all watchers
                foreach (var kvp in _watchers)
                {
                    try
                    {
                        kvp.Value.EnableRaisingEvents = false;
                        kvp.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing file watcher for repository {RepositoryId}", kvp.Key);
                    }
                }

                _watchers.Clear();
                _cleanupTimer?.Dispose();
                _disposed = true;
                
                _logger.LogInformation("File watcher service disposed");
            }
        }
    }

    /// <summary>
    /// Configuration options for file watcher service
    /// </summary>
    public class FileWatcherOptions
    {
        public int DebounceDelayMs { get; set; } = 500;
        public string[] WatchedExtensions { get; set; } = { ".cs", ".vb", ".fs", ".csproj", ".vbproj", ".fsproj", ".sln" };
        public string[] ExcludedPatterns { get; set; } = { "bin", "obj", ".git", ".vs", "node_modules", "packages" };
        public bool EnableDetailedLogging { get; set; } = false;
        public int MaxConcurrentWatchers { get; set; } = 100;
    }
}