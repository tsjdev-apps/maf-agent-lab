using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Demos;

/// <summary>Demonstrates model-selected handoffs between specialist agents.</summary>
internal static class MultiAgentWorkflowHandoff
{
    /// <summary>Gets the default request for the handoff workflow.</summary>
    internal const string DefaultPrompt = """
        Prepare a rollout recommendation for a new self-service second-factor recovery journey for enterprise tenants.
        We want to reduce helpdesk load, keep the recovery flow strongly audited, support pilot customers in Germany and the Netherlands,
        and enable a phased rollout next quarter without disrupting the existing administrator-driven recovery path.
        Include guidance on security controls, support readiness, rollout sequencing, and key risks.
        """;

    /// <summary>
    /// Defines allowed handoff tools; the model chooses which specialist receives ownership.
    /// </summary>
    internal static Workflow CreateWorkflow(IChatClient chatClient)
    {
        AIAgent identityLead = chatClient.AsAIAgent(
            name: "IdentityLead",
            instructions: """
                You are the lead coordinator for identity feature rollout planning.
                You decide which specialist should handle each part of the request and you present the final consolidated answer.
                Hand over to security when abuse resistance, authorization, auditability, or compliance controls dominate.
                Hand over to support when admin enablement, communications, training, or service readiness need deeper work.
                Hand over to rollout when sequencing, feature flags, monitoring, dependencies, or operational risk become central.
                When the specialists have completed their work, consolidate the recommendation into a clear final plan.
                """);

        AIAgent securityLead = chatClient.AsAIAgent(
            name: "SecurityLead",
            instructions: """
                You own the security and control perspective.
                Focus on authorization boundaries, approval safeguards, auditability, abuse prevention, and tenant risk differentiation.
                When your part is complete, hand over to the best next specialist or return control to IdentityLead.
                """);

        AIAgent supportLead = chatClient.AsAIAgent(
            name: "SupportLead",
            instructions: """
                You own support readiness and administrator experience.
                Focus on helpdesk workflows, operator guidance, customer communication, training, and user-facing recovery clarity.
                When your part is complete, hand over to the best next specialist or return control to IdentityLead.
                """);

        AIAgent rolloutLead = chatClient.AsAIAgent(
            name: "RolloutLead",
            instructions: """
                You own rollout execution.
                Focus on sequencing, feature flags, tenant onboarding, monitoring, rollback, dependencies, and delivery feasibility.
                When your part is complete, return control to IdentityLead for final consolidation.
                """);

#pragma warning disable MAAIW001
        Workflow handoffWorkflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(identityLead)
            .WithHandoff(identityLead, securityLead, "Hand off when authorization, abuse prevention, auditability, or compliance controls dominate.")
            .WithHandoff(identityLead, supportLead, "Hand off when operator readiness, communications, or support workflow design need specialist input.")
            .WithHandoff(identityLead, rolloutLead, "Hand off when sequencing, feature flags, rollout dependencies, or operational risk dominate.")
            .WithHandoff(securityLead, supportLead, "Hand off after the control model is clear and the support experience needs to fit within it.")
            .WithHandoff(securityLead, rolloutLead, "Hand off after security requirements are set and rollout feasibility must be validated.")
            .WithHandoff(securityLead, identityLead, "Hand off when the security recommendation is complete and ready for consolidation.")
            .WithHandoff(supportLead, rolloutLead, "Hand off after support readiness is defined and rollout sequencing must be finalized.")
            .WithHandoff(supportLead, identityLead, "Hand off when support guidance is complete and ready for consolidation.")
            .WithHandoff(rolloutLead, identityLead, "Hand off when rollout planning is complete and the final response can be assembled.")
            .EmitAgentResponseEvents(true)
            .Build();
#pragma warning restore MAAIW001

        return handoffWorkflow;
    }

    /// <summary>Runs the handoff workflow demo.</summary>
    public static Task RunAsync(DemoContext context) => context.RunWorkflowAsync(CreateWorkflow, DefaultPrompt);

}

