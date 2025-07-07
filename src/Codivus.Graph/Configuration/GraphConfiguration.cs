using System;

namespace Codivus.Graph.Configuration
{
    public class GraphConfiguration
    {
        public bool Enabled { get; set; } = false;
        public Neo4jSettings Neo4j { get; set; } = new();
        public ProcessingSettings Processing { get; set; } = new();
        public AnalysisSettings Analysis { get; set; } = new();
    }

    public class Neo4jSettings
    {
        public string Uri { get; set; } = "bolt://localhost:7687";
        public string Username { get; set; } = "neo4j";
        public string Password { get; set; } = "pass12345678";
        public string Database { get; set; } = "neo4j";
        public int MaxConnectionPoolSize { get; set; } = 50;
        public TimeSpan ConnectionAcquisitionTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnableEncryption { get; set; } = false;
        public string TrustStrategy { get; set; } = "TrustAllCertificates"; // TrustAllCertificates, TrustSystemCaSignedCertificates
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