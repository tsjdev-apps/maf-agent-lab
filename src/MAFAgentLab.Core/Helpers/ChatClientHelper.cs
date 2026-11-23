using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using OpenAI;

namespace MAFAgentLab.Core.Helpers;

/// <summary>Creates chat clients for the configured model providers.</summary>
internal static class ChatClientHelper
{
    /// <summary>Creates a chat client for the currently selected provider.</summary>
    public static IChatClient CreateAgentChatClient(IConfiguration config) => ConfigurationHelper.GetAgentProvider(config) switch
    {
        "Ollama" => CreateOllamaChatClient(config),
        "OpenAI" => CreateOpenAIChatClient(config),
        _ => CreateAzureOpenAIChatClient(config)
    };

    /// <summary>Creates an Azure OpenAI chat client from configuration.</summary>
    public static IChatClient CreateAzureOpenAIChatClient(IConfiguration config) =>
        new AzureOpenAIClient(ConfigurationHelper.GetEndpoint(config, "AzureOpenAI:Endpoint"),
            new ApiKeyCredential(ConfigurationHelper.GetRequiredSetting(config, "AzureOpenAI:ApiKey")))
        .GetChatClient(ConfigurationHelper.GetRequiredSetting(config, "AzureOpenAI:Deployment")).AsIChatClient();

    /// <summary>Creates an Ollama chat client from configuration.</summary>
    public static IChatClient CreateOllamaChatClient(IConfiguration config)
    {
        OllamaApiClient client = new(ConfigurationHelper.GetEndpoint(config, "Ollama:Endpoint"),
            ConfigurationHelper.GetRequiredSetting(config, "Ollama:Deployment"));
        if (ConfigurationHelper.GetOptionalSetting(config, "Ollama:ApiKey") is { } key)
            client.DefaultRequestHeaders["Authorization"] = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key).ToString();
        return client;
    }

    /// <summary>Creates an OpenAI chat client from configuration.</summary>
    public static IChatClient CreateOpenAIChatClient(IConfiguration config)
    {
        string? endpoint = ConfigurationHelper.GetOptionalSetting(config, "OpenAI:Endpoint");
        OpenAIClient client = endpoint is null
            ? new OpenAIClient(new ApiKeyCredential(ConfigurationHelper.GetRequiredSetting(config, "OpenAI:ApiKey")))
            : new OpenAIClient(new ApiKeyCredential(ConfigurationHelper.GetRequiredSetting(config, "OpenAI:ApiKey")),
                new OpenAIClientOptions { Endpoint = ConfigurationHelper.GetEndpoint(config, "OpenAI:Endpoint") });
        return client.GetChatClient(ConfigurationHelper.GetRequiredSetting(config, "OpenAI:Model")).AsIChatClient();
    }
}
