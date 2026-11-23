using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Demos;

/// <summary>Demonstrates model-selected weather and local-time tools.</summary>
internal static class AgentsWithTools
{
    internal const string DefaultPrompt = "What's the weather like in Pforzheim, Germany, and what time is it there right now?";

    /// <summary>Runs the tools demo with the configured chat provider.</summary>
    public static async Task RunAsync(DemoContext context)
    {
        string prompt = context.GetPrompt(DefaultPrompt);

        RunTrace trace = context.Trace;

        WeatherTools tools = new(
            ConfigurationHelper.GetRequiredSetting(
                context.Configuration,
                "OpenWeatherMap:ApiKey"),
            trace);

        using IChatClient client =
            ChatClientHelper.CreateAgentChatClient(context.Configuration);

        AIAgent agent = client.AsAIAgent(
            name: "WeatherAssistant",
            instructions:
                "Use the available tools for weather and time questions. " +
                "Report tool failures honestly; never invent live data. " +
                "Keep the answer concise.",
            tools:
            [
                AIFunctionFactory.Create(tools.GetWeatherAsync),
                AIFunctionFactory.Create(tools.GetCurrentTimeAsync)
            ]);

        await context.StreamAgentAsync(agent, prompt);
    }
}
