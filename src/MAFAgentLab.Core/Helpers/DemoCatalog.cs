using MAFAgentLab.Core.Demos;

namespace MAFAgentLab.Core.Helpers;

/// <summary>Provides the catalog of available agent demonstrations.</summary>
public static class DemoCatalog
{
    /// <summary>Gets the default follow-up for the conversation and persistence demo.</summary>
    public const string ConversationFollowUp = "Which rollout and customer did I mention, and what communication style do I prefer?";

    /// <summary>Gets all available demo definitions in presentation order.</summary>
    public static IReadOnlyList<DemoDefinition> All { get; } =
    [
        new("01", "Simple agent", "Single agent",
            SimpleAgent.DefaultPrompt,
            SimpleAgent.RunAsync, ConfigurationHelper.GetMissingAgentSettings),
        new("02", "Conversation & persistence", "Session",
            SimpleAgentWithConversationThread.DefaultPrompt,
            SimpleAgentWithConversationThread.RunAsync, ConfigurationHelper.GetMissingAgentSettings),
        new("03", "Compare providers", "IChatClient",
            SimpleAgentWithDifferentProviders.DefaultPrompt,
            SimpleAgentWithDifferentProviders.RunAsync, ConfigurationHelper.GetMissingProviderSettings),
        new("04", "Agents with tools", "Function calling",
            AgentsWithTools.DefaultPrompt,
            AgentsWithTools.RunAsync,
            config => [.. ConfigurationHelper.GetMissingAgentSettings(config), .. ConfigurationHelper.GetMissingWeatherSettings(config)]),
        new("05", "Sequential workflow", "Pipeline",
            MultiAgentWorkflowSequential.DefaultPrompt,
            MultiAgentWorkflowSequential.RunAsync, ConfigurationHelper.GetMissingAgentSettings),
        new("06", "Concurrent workflow", "Fan-out / fan-in",
            MultiAgentWorkflowConcurrent.DefaultPrompt,
            MultiAgentWorkflowConcurrent.RunAsync, ConfigurationHelper.GetMissingAgentSettings),
        new("07", "Handoff workflow", "Dynamic routing",
            MultiAgentWorkflowHandoff.DefaultPrompt,
            MultiAgentWorkflowHandoff.RunAsync, ConfigurationHelper.GetMissingAgentSettings),
        new("08", "Group chat", "Round robin",
            MultiAgentWorkflowGroupChat.DefaultPrompt,
            MultiAgentWorkflowGroupChat.RunAsync, ConfigurationHelper.GetMissingAgentSettings),
        new("09", "Magentic planner", "Plan / delegate / review",
            MagenticPlannerWorkflow.DefaultPrompt,
            MagenticPlannerWorkflow.RunAsync, ConfigurationHelper.GetMissingAgentSettings)
    ];

    /// <summary>Finds a demo by its one- or two-digit identifier.</summary>
    public static DemoDefinition? Find(string id) => All.FirstOrDefault(demo => demo.Id == id.PadLeft(2, '0'));
}
