using System.Text.Json;
using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Demos;

/// <summary>Demonstrates conversation persistence through an agent session.</summary>
internal static class SimpleAgentWithConversationThread
{
    internal const string DefaultPrompt = "I am preparing the Atlas MAF rollout for Fabrikam. I prefer concise technical summaries.";

    /// <summary>Runs two turns with serialized and restored session state.</summary>
    public static async Task RunAsync(DemoContext context)
    {
        using IChatClient client =
            ChatClientHelper.CreateAgentChatClient(context.Configuration);

        AIAgent agent = client.AsAIAgent(
            name: "ProjectMemory",
            instructions:
                "Remember project facts from this session and " +
                "answer follow-ups concisely.");

        AgentSession session =
            await agent.CreateSessionAsync(context.CancellationToken);

        string seed = context.GetPrompt(DefaultPrompt);

        context.Trace.Event("Session", "Turn 1: " + seed, "session");
        await context.StreamAgentAsync(agent, seed, session, isFinal: false);

        // Restore a fresh session object to demonstrate persistence, rather than just displaying JSON.
        JsonElement state =
            await agent.SerializeSessionAsync(
                session,
                cancellationToken: context.CancellationToken);

        session =
            await agent.DeserializeSessionAsync(
                state,
                cancellationToken: context.CancellationToken);

        context.Trace.Event(
            "Session",
            "Restored a new AgentSession from serialized history.\n" +
            JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions { WriteIndented = true }),
            "session");

        string followUp = context.FollowUp ?? DemoCatalog.ConversationFollowUp;

        context.Trace.Event("Session", "Turn 2: " + followUp, "session");
        await context.StreamAgentAsync(agent, followUp, session);
    }
}
