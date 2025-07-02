using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Xunit;

namespace Codivus.CLI.Tests.Helpers;

public class JanusGraphTestContainer : IAsyncLifetime
{
    private IContainer? _container;
    private INetwork? _network;
    
    public int Port { get; private set; }
    public string Host => "localhost";
    public string ConnectionString => $"ws://{Host}:{Port}/gremlin";

    public async Task InitializeAsync()
    {
        // Create network for container isolation
        _network = new NetworkBuilder()
            .WithName($"janusgraph-test-network-{Guid.NewGuid()}")
            .Build();

        await _network.CreateAsync();

        // Build JanusGraph container
        _container = new ContainerBuilder()
            .WithImage("janusgraph/janusgraph:1.0.0")
            .WithNetwork(_network)
            .WithPortBinding(8182, true)
            .WithEnvironment("JANUSGRAPH_STORAGE_BACKEND", "inmemory")
            .WithEnvironment("JANUSGRAPH_INDEX_SEARCH_BACKEND", "lucene")
            .WithEnvironment("JANUSGRAPH_GREMLIN_GRAPH", "org.janusgraph.core.JanusGraphFactory")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(8182)
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(8182)
                    .ForPath("/")
                    .ForStatusCode(System.Net.HttpStatusCode.OK)))
            .WithStartupCallback(async (container, ct) =>
            {
                Port = container.GetMappedPublicPort(8182);
                
                // Wait for JanusGraph to be fully ready
                await Task.Delay(5000, ct);
                
                // Test connection
                var maxRetries = 30;
                var retryCount = 0;
                
                while (retryCount < maxRetries)
                {
                    try
                    {
                        using var httpClient = new HttpClient();
                        var response = await httpClient.GetAsync($"http://{Host}:{Port}", ct);
                        if (response.IsSuccessStatusCode)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        // Continue retrying
                    }
                    
                    retryCount++;
                    await Task.Delay(1000, ct);
                }
                
                if (retryCount >= maxRetries)
                {
                    throw new InvalidOperationException("JanusGraph container failed to start properly");
                }
            })
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
        
        if (_network != null)
        {
            await _network.DeleteAsync();
            await _network.DisposeAsync();
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        if (_container == null) return false;
        
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"http://{Host}:{Port}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}