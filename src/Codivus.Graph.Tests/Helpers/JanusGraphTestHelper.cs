using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Codivus.Graph.Tests.Helpers
{
    /// <summary>
    /// Helper class for JanusGraph integration tests
    /// </summary>
    public static class JanusGraphTestHelper
    {
        private const string DefaultHost = "host.docker.internal";
        private const int DefaultPort = 8182;

        /// <summary>
        /// Checks if JanusGraph is available for testing
        /// </summary>
        public static async Task<bool> IsJanusGraphAvailableAsync(string host = DefaultHost, int port = DefaultPort)
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
                    return await TestJanusGraphHttpEndpoint(host, port);
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tests if JanusGraph HTTP endpoint is responding
        /// </summary>
        private static async Task<bool> TestJanusGraphHttpEndpoint(string host, int port)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync($"http://{host}:{port}/");
                
                // Even if we get an error response, if we get any response, the server is running
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the connection string for JanusGraph tests
        /// </summary>
        public static string GetConnectionString(string host = DefaultHost, int port = DefaultPort)
        {
            return $"ws://{host}:{port}/gremlin";
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
        /// Gets the reason why JanusGraph tests should be skipped, or null if tests should run
        /// </summary>
        public static async Task<string?> GetSkipReasonAsync()
        {
            if (IsRunningInCI())
            {
                return "Integration tests are disabled in CI/CD environments";
            }

            if (!await IsJanusGraphAvailableAsync())
            {
                return $"JanusGraph is not available at {DefaultHost}:{DefaultPort}. Please ensure JanusGraph is running.";
            }

            return null;
        }

        /// <summary>
        /// Determines if JanusGraph tests should be skipped
        /// </summary>
        public static async Task<bool> ShouldSkipTestsAsync()
        {
            return await GetSkipReasonAsync() != null;
        }
    }
}