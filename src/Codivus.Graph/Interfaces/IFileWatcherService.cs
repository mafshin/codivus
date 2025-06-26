using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codivus.Graph.Interfaces
{
    /// <summary>
    /// Interface for file system watching and change detection
    /// </summary>
    public interface IFileWatcherService
    {
        /// <summary>
        /// Starts watching a repository for file changes
        /// </summary>
        Task StartWatchingAsync(string repositoryId, string repositoryPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops watching a repository
        /// </summary>
        Task StopWatchingAsync(string repositoryId);

        /// <summary>
        /// Gets all repositories currently being watched
        /// </summary>
        Task<IEnumerable<string>> GetWatchedRepositoriesAsync();

        /// <summary>
        /// Event fired when files are changed
        /// </summary>
        event EventHandler<FileChangeEventArgs> FileChanged;

        /// <summary>
        /// Event fired when files are created
        /// </summary>
        event EventHandler<FileChangeEventArgs> FileCreated;

        /// <summary>
        /// Event fired when files are deleted
        /// </summary>
        event EventHandler<FileChangeEventArgs> FileDeleted;

        /// <summary>
        /// Event fired when files are renamed
        /// </summary>
        event EventHandler<FileRenameEventArgs> FileRenamed;

        /// <summary>
        /// Gets the current status of all watchers
        /// </summary>
        Task<WatcherStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// File change event arguments
    /// </summary>
    public class FileChangeEventArgs : EventArgs
    {
        public string RepositoryId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public FileChangeType ChangeType { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// File rename event arguments
    /// </summary>
    public class FileRenameEventArgs : EventArgs
    {
        public string RepositoryId { get; set; } = string.Empty;
        public string OldFilePath { get; set; } = string.Empty;
        public string NewFilePath { get; set; } = string.Empty;
        public string OldFullPath { get; set; } = string.Empty;
        public string NewFullPath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Type of file change
    /// </summary>
    public enum FileChangeType
    {
        Created,
        Modified,
        Deleted,
        Renamed
    }

    /// <summary>
    /// File watcher statistics
    /// </summary>
    public class WatcherStatistics
    {
        public int ActiveWatchers { get; set; }
        public long TotalEventsProcessed { get; set; }
        public long EventsInLastHour { get; set; }
        public Dictionary<string, int> EventsByType { get; set; } = new();
        public Dictionary<string, RepositoryWatcherInfo> WatchedRepositories { get; set; } = new();
        public DateTime LastEventTime { get; set; }
    }

    /// <summary>
    /// Information about a watched repository
    /// </summary>
    public class RepositoryWatcherInfo
    {
        public string RepositoryId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public long EventCount { get; set; }
        public DateTime LastEventTime { get; set; }
        public bool IsActive { get; set; }
        public string[] WatchedExtensions { get; set; } = Array.Empty<string>();
    }
}