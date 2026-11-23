using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Demos;

/// <summary>Demonstrates native Magentic planning and delegation.</summary>
internal static class MagenticPlannerWorkflow
{
    /// <summary>Gets the default delivery request for the Magentic workflow.</summary>
    internal const string DefaultPrompt = """
        Create a compact delivery proposal for a supervised recovery approval service in our B2B identity platform.
        Helpdesk supervisors can approve temporary second-factor resets for locked-out partner users.
        Require step-up authentication, dual control for high-risk tenants, and exportable audit records.
        The approval-check API must stay below 250 ms p95. Start with three pilot tenants and a kill switch.
        Produce a technical proposal with architecture, risks, and rollout steps.
        """;

    /// <summary>Creates the Magentic planner workflow and its specialist agents.</summary>
    internal static Workflow CreateWorkflow(IChatClient client)
    {
        AIAgent lead = client.AsAIAgent(name: "DeliveryLead", instructions: "Coordinate a concise technical delivery proposal. Delegate architecture, controls, and rollout questions to the team and synthesize their findings.");
        AIAgent architect = client.AsAIAgent(name: "IdentityArchitect", description: "Identity API, domain model, authorization and performance specialist.", instructions: "Answer the manager's assigned architecture question. Focus on API shape, authorization boundaries and performance. Be concise.");
        AIAgent risk = client.AsAIAgent(name: "RiskAnalyst", description: "Abuse resistance, dual control, audit and compliance specialist.", instructions: "Answer the manager's assigned risk question. Focus on abuse resistance, dual control, auditability and failure modes. Be concise.");
        AIAgent rollout = client.AsAIAgent(name: "RolloutEngineer", description: "Pilot sequencing, feature flags, monitoring and rollback specialist.", instructions: "Answer the manager's assigned rollout question. Focus on pilots, feature flags, monitoring and rollback. Be concise.");

        return AgentWorkflowBuilder.CreateMagenticBuilderWith(lead)
            .AddParticipants([architect, risk, rollout])
            .WithMaxRounds(8).WithMaxResets(1).WithMaxStalls(2)
            .RequirePlanSignoff(false)
            .Build();
    }

    /// <summary>Runs the Magentic planner workflow demo.</summary>
    public static Task RunAsync(DemoContext context) => context.RunWorkflowAsync(CreateWorkflow, DefaultPrompt);
}
