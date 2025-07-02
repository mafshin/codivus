using System.CommandLine;
using Codivus.CLI.Services;
using Codivus.CLI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Codivus.CLI.Commands;

/// <summary>
/// Command for managing LLM provider operations
/// </summary>
public class LlmCommand : Command
{
    public LlmCommand(IServiceProvider serviceProvider) : base("llm", "Manage LLM providers and models")
    {
        // List providers command
        var listProvidersCommand = new Command("list-providers", "List available LLM providers");
        listProvidersCommand.SetHandler(async () =>
        {
            var llmService = serviceProvider.GetRequiredService<LlmCommandService>();
            var outputService = serviceProvider.GetRequiredService<IOutputService>();
            var result = await llmService.ListProvidersAsync();
            await outputService.WriteResultsAsync(result);
        });
        AddCommand(listProvidersCommand);

        // List models command
        var listModelsCommand = new Command("list-models", "List available models for a provider");
        var providerOption = new Option<string>(
            aliases: new[] { "--provider", "-p" },
            description: "LLM provider type (Ollama or LmStudio)")
        {
            IsRequired = true
        };
        listModelsCommand.AddOption(providerOption);
        
        listModelsCommand.SetHandler(async (string provider) =>
        {
            var llmService = serviceProvider.GetRequiredService<LlmCommandService>();
            var outputService = serviceProvider.GetRequiredService<IOutputService>();
            var result = await llmService.ListModelsAsync(provider);
            await outputService.WriteResultsAsync(result);
        }, providerOption);
        AddCommand(listModelsCommand);

        // Test connectivity command
        var testCommand = new Command("test", "Test LLM provider connectivity");
        testCommand.AddOption(providerOption);
        
        testCommand.SetHandler(async (string provider) =>
        {
            var llmService = serviceProvider.GetRequiredService<LlmCommandService>();
            var outputService = serviceProvider.GetRequiredService<IOutputService>();
            var result = await llmService.TestConnectivityAsync(provider);
            await outputService.WriteResultsAsync(result);
        }, providerOption);
        AddCommand(testCommand);
    }
}