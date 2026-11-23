using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Demos;

/// <summary>Demonstrates a single streaming agent.</summary>
internal static class SimpleAgent
{
    internal const string DefaultPrompt = "Plan a Mediterranean cruise from Genoa in June for me and my parents. Explain your assumptions and suggest a compact itinerary.";

    /// <summary>Runs the simple agent demo.</summary>
    public static async Task RunAsync(DemoContext context)
    {
        string prompt = context.GetPrompt(DefaultPrompt);

        using IChatClient client =
            ChatClientHelper.CreateAgentChatClient(context.Configuration);

        AIAgent agent = client.AsAIAgent(
            name: "CruisePlanner",
            instructions:
                "Help plan a cruise. Be concise. " +
                "Distinguish suggestions from verified availability and prices.");

        await context.StreamAgentAsync(agent, prompt);
    }
}
