using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Demos;

/// <summary>Demonstrates a concurrent multi-agent workflow.</summary>
internal static class MultiAgentWorkflowConcurrent
{
    /// <summary>Gets the default rollout update for the concurrent workflow.</summary>
    internal const string DefaultPrompt = """
        Pilot feedback for the new passkey sign-in rollout:
        - Adoption reached 68% across 14 enterprise tenants in the first 10 days.
        - Sign-in success improved from 91% to 97%, but helpdesk tickets rose by 23%.
        - Most praise mentions faster sign-in and fewer SMS delays.
        - Most complaints come from shared-device scenarios and recovery confusion.
        - Two regulated customers asked for clearer audit reporting before full rollout.
        - Product wants a decision memo by Friday on whether to expand the pilot.
        """;

    /// <summary>
    /// Fans the same input out to independent agents and collects their final messages.
    /// </summary>
    internal static Workflow CreateWorkflow(IChatClient chatClient)
    {
        AIAgent leadershipBrief = chatClient.AsAIAgent(
            name: "LeadershipBrief",
            instructions: "Summarize the rollout update for leadership in exactly 3 short bullets. Highlight decision-relevant context.");

        AIAgent riskLens = chatClient.AsAIAgent(
            name: "RiskLens",
            instructions: "Identify the rollout risks, blockers, and recommended follow-up actions from the update. Keep it practical and concise.");

        AIAgent signalSpotter = chatClient.AsAIAgent(
            name: "SignalSpotter",
            instructions: "Extract the most important metrics, stakeholder signals, and trend indicators from the update.");

        // Fan out once, then preserve each perspective for the trace.
        Workflow concurrent = AgentWorkflowBuilder.BuildConcurrent(
            [leadershipBrief, riskLens, signalSpotter],
            null);

        return concurrent;
    }

    /// <summary>Runs the concurrent workflow demo and collects every perspective.</summary>
    public static Task RunAsync(DemoContext context) => context.RunWorkflowAsync(CreateWorkflow, DefaultPrompt, collectAll: true);

}

