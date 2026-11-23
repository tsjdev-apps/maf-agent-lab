using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace MAFAgentLab.Core.Demos;

/// <summary>Compares responses from every configured chat provider.</summary>
internal static class SimpleAgentWithDifferentProviders
{
    internal const string DefaultPrompt = "Write a vivid story about a developer discovering Munich. Use at most 100 words.";

    /// <summary>Runs the same prompt through each complete provider configuration.</summary>
    public static async Task RunAsync(DemoContext context)
    {
        string prompt = context.GetPrompt(DefaultPrompt);

        Provider[] providers =
        [
            new(
                "Azure OpenAI",
                ConfigurationHelper.GetMissingAzureOpenAISettings,
                ChatClientHelper.CreateAzureOpenAIChatClient),
            new(
                "Ollama",
                ConfigurationHelper.GetMissingOllamaSettings,
                ChatClientHelper.CreateOllamaChatClient),
            new(
                "OpenAI",
                ConfigurationHelper.GetMissingOpenAISettings,
                ChatClientHelper.CreateOpenAIChatClient)
        ];

        int successful = 0;

        foreach (Provider provider in providers)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> missing =
                provider.GetMissing(context.Configuration);

            if (missing.Count > 0)
            {
                context.Trace.Event(provider.Name, "Skipped: " + string.Join(", ", missing), "skipped");
                continue;
            }

            try
            {
                using IChatClient client = provider.Create(context.Configuration);

                AIAgent agent = client.AsAIAgent(
                    name: provider.Name,
                    instructions:
                        "You are a concise storytelling assistant. " +
                        "Turn the prompt into a vivid, compact story.");

                await context.StreamAgentAsync(agent, prompt);
                successful++;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                context.Trace.Fail(provider.Name, error.Message);
            }
        }

        if (successful == 0)
        {
            throw new InvalidOperationException(
                "No provider completed the request. " +
                "Check the provider errors above.");
        }
    }

    private sealed record Provider(
        string Name,
        Func<IConfiguration, IReadOnlyList<string>> GetMissing,
        Func<IConfiguration, IChatClient> Create);
}
