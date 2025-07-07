using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Codivus.Graph.Tests.Helpers
{
    /// <summary>
    /// Helper class for Neo4j integration tests
    /// </summary>
    public static class Neo4jTestHelper
    {
        private const string DefaultHost = "localhost";
        private const int DefaultPort = 7687;

        /// <summary>
        /// Checks if Neo4j is available for testing
        /// </summary>
        public static async Task<bool> IsNeo4jAvailableAsync(string host = DefaultHost, int port = DefaultPort)
        {
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == connectTask && tcpClient.Connected)
                {
                    // Additional check: try to send a simple HTTP request
                    return await TestNeo4jHttpEndpoint(host, port);
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tests if Neo4j HTTP endpoint is responding
        /// </summary>
        private static async Task<bool> TestNeo4jHttpEndpoint(string host, int port)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(2);
                
                // Try a GET to / which should respond quickly
                var httpPort = port == 7687 ? 7474 : port; // Use HTTP port if Bolt port is provided
                var response = await httpClient.GetAsync($"http://{host}:{httpPort}/");
                
                // Even if we get an error response, if we get any response, the server is running
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the connection string for Neo4j tests
        /// </summary>
        public static string GetConnectionString(string host = DefaultHost, int port = DefaultPort)
        {
            return $"bolt://{host}:{port}";
        }

        /// <summary>
        /// Checks if we're running in a CI environment
        /// </summary>
        public static bool IsRunningInCI()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_BUILDID"));
        }

        /// <summary>
        /// Gets the reason why Neo4j tests should be skipped, or null if tests should run
        /// </summary>
        public static async Task<string?> GetSkipReasonAsync()
        {
            if (IsRunningInCI())
            {
                return "Integration tests are disabled in CI/CD environments";
            }

            if (!await IsNeo4jAvailableAsync())
            {
                return $"Neo4j is not available at {DefaultHost}:{DefaultPort}. Please ensure Neo4j is running.";
            }

            return null;
        }

        /// <summary>
        /// Determines if Neo4j tests should be skipped
        /// </summary>
        public static async Task<bool> ShouldSkipTestsAsync()
        {
            return await GetSkipReasonAsync() != null;
        }
    }
}