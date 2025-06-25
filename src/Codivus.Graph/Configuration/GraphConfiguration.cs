namespace Codivus.Graph.Configuration
{
    public class GraphConfiguration
    {
        public bool Enabled { get; set; } = false;
        public JanusGraphSettings JanusGraph { get; set; } = new();
        public ProcessingSettings Processing { get; set; } = new();
        public AnalysisSettings Analysis { get; set; } = new();
    }

    public class JanusGraphSettings
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 8182;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public int ConnectionPoolSize { get; set; } = 10;
        public int ConnectionTimeout { get; set; } = 30000; // milliseconds
        public bool EnableSsl { get; set; } = false;
        public string GraphName { get; set; } = "codivus";
    }

    public class ProcessingSettings
    {
        public int MaxConcurrentFiles { get; set; } = 50;
        public int BatchSize { get; set; } = 1000;
        public int TimeoutMinutes { get; set; } = 30;
        public int RetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 5;
        public int CheckpointIntervalMinutes { get; set; } = 5;
    }

    public class AnalysisSettings
    {
        public bool IncludeTests { get; set; } = false;
        public long MaxFileSize { get; set; } = 1048576; // 1MB
        public string[] SupportedExtensions { get; set; } = { ".cs", ".vb" };
        public string[] ExcludedDirectories { get; set; } = { "bin", "obj", "packages", ".git", ".vs" };
        public bool AnalyzeGeneratedCode { get; set; } = false;
    }
}