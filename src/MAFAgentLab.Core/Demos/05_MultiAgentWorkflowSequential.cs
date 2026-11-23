using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Demos;

/// <summary>Demonstrates a sequential multi-agent workflow.</summary>
internal static class MultiAgentWorkflowSequential
{
    /// <summary>Gets the default request for the sequential workflow.</summary>
    internal const string DefaultPrompt = "Prepare a delivery brief for adding passkey-based step-up authentication to our B2B admin portal. Cover scope, implementation approach, rollout dependencies, and open risks.";

    /// <summary>
    /// Builds a fixed sequence; each agent sees the accumulated conversation.
    /// </summary>
    internal static Workflow CreateWorkflow(IChatClient chatClient)
    {
        AIAgent scopeAnalyst = chatClient.AsAIAgent(
            name: "ScopeAnalyst",
            instructions: "Clarify the business objective, affected actors, constraints, and missing assumptions in the request.");

        AIAgent solutionDrafter = chatClient.AsAIAgent(
            name: "SolutionDrafter",
            instructions: "Turn the scoped request into a practical delivery brief with implementation approach and milestones.");

        AIAgent architectureReviewer = chatClient.AsAIAgent(
            name: "ArchitectureReviewer",
            instructions: "Strengthen the brief by calling out technical risks, dependencies, and design trade-offs.");

        AIAgent executiveEditor = chatClient.AsAIAgent(
            name: "ExecutiveEditor",
            instructions: "Polish the brief so it is concise, structured, and suitable for engineering leadership.");

        // The last agent receives the accumulated result of the preceding agents.
        Workflow pipeline = AgentWorkflowBuilder.BuildSequential(
            scopeAnalyst, solutionDrafter, architectureReviewer, executiveEditor);

        return pipeline;
    }

    /// <summary>Runs the sequential workflow demo.</summary>
    public static Task RunAsync(DemoContext context) => context.RunWorkflowAsync(CreateWorkflow, DefaultPrompt);

}

