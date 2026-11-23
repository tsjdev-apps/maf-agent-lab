using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Demos;

/// <summary>Demonstrates bounded round-robin group chat coordination.</summary>
internal static class MultiAgentWorkflowGroupChat
{
    /// <summary>Gets the default incident brief for the group chat workflow.</summary>
    internal const string DefaultPrompt = """
        We are on an authentication incident bridge for partner sign-in.
        Since 07:45 UTC, passkey completion for B2B users in eu-west dropped from 96% to 72% and the fallback OTP path is timing out for 4.1% of requests.
        Deployment auth-gateway 2026.04.15.2 completed 12 minutes before the spike. Token cache hit rate fell from 89% to 58%,
        while the session store in West Europe shows elevated write latency.
        Work together like an engineering response room: identify the most likely fault line, propose immediate containment,
        assign next technical actions, and finish with a short stakeholder-ready update.
        """;

    /// <summary>
    /// Shares conversation history with round-robin selection and bounded termination.
    /// </summary>
    internal static Workflow CreateWorkflow(IChatClient chatClient)
    {
        AIAgent bridgeLead = chatClient.AsAIAgent(
            name: "BridgeLead",
            instructions: """
                You lead a focused authentication incident bridge.
                Keep each turn concise, build on what the others already said, and avoid repetition.
                Ask for or synthesize only the next most valuable point.
                When the team has enough signal, answer with 'FINAL UPDATE:' followed by:
                probable cause, immediate containment, owner actions, and a short stakeholder update.
                """);

        AIAgent identityEngineer = chatClient.AsAIAgent(
            name: "IdentityEngineer",
            instructions: """
                You are the identity engineer on the bridge.
                Focus on authentication flow behavior, token handling, recovery paths, and code-level regression clues.
                Respond in 2 to 4 short technical bullets and reference the incident context already discussed.
                """);

        AIAgent reliabilityEngineer = chatClient.AsAIAgent(
            name: "ReliabilityEngineer",
            instructions: """
                You are the reliability engineer on the bridge.
                Focus on rollout effects, infrastructure health, cache and session-store signals, and mitigation options.
                Respond in 2 to 4 short technical bullets and keep it suitable for live incident communication.
                """);

        Workflow groupChatWorkflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(static agents =>
            {
                RoundRobinGroupChatManager manager = new(
                    agents,
                    static (_, history, _) => new ValueTask<bool>(ShouldTerminate(history)))
                {
                    MaximumIterationCount = 8
                };
                return manager;
            })
            .WithName("Authentication Incident Group Chat")
            .WithDescription("A technical group chat in which engineers investigate an authentication incident together.")
            .AddParticipants([bridgeLead, identityEngineer, reliabilityEngineer])
            .Build();

        return groupChatWorkflow;
    }

    /// <summary>Runs the group chat workflow demo.</summary>
    public static Task RunAsync(DemoContext context) => context.RunWorkflowAsync(CreateWorkflow, DefaultPrompt);

    /// <summary>Determines whether the bridge lead has issued the final eligible update.</summary>
    internal static bool ShouldTerminate(IEnumerable<ChatMessage> history)
    {
        ChatMessage[] messages = history.ToArray();
        ChatMessage? last = messages.LastOrDefault(message => message.Role == ChatRole.Assistant);
        return last?.AuthorName == "BridgeLead"
            && last.Text.TrimStart().StartsWith("FINAL UPDATE:", StringComparison.OrdinalIgnoreCase)
            && messages.Any(message => message.Role == ChatRole.Assistant && message.AuthorName == "IdentityEngineer")
            && messages.Any(message => message.Role == ChatRole.Assistant && message.AuthorName == "ReliabilityEngineer");
    }
}

