namespace Codivus.Graph.Configuration
{
    public class GraphScanningConfiguration
    {
        public bool Enabled { get; set; } = true;
        public JanusGraphSettings JanusGraph { get; set; } = new();
        public ProcessingSettings Processing { get; set; } = new();
        public AnalysisSettings Analysis { get; set; } = new();
    }

    public class GraphScanningProcessingSettings
    {
        public int MaxConcurrentFiles { get; set; } = 50;
        public int BatchSize { get; set; } = 1000;
        public int TimeoutMinutes { get; set; } = 30;
        public int RetryAttempts { get; set; } = 3;
    }

    public class GraphScanningAnalysisSettings
    {
        public bool IncludeTests { get; set; } = false;
        public long MaxFileSize { get; set; } = 1048576; // 1MB
        public string[] SupportedExtensions { get; set; } = { ".cs", ".vb" };
    }
}