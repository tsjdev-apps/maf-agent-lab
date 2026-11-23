namespace MAFAgentLab.Core.Helpers;

/// <summary>Illustrative data for exploring the UI. Does not call a model or execute a live workflow.</summary>
public static class GuidedReplay
{
    /// <summary>Replays a demo using illustrative data without making external calls.</summary>
    public static async Task RunAsync(DemoDefinition demo, RunTrace trace, CancellationToken cancellationToken, TimeSpan? stepDelay = null)
    {
        TimeSpan delay = stepDelay ?? TimeSpan.FromMilliseconds(280);
        trace.Event("Guided replay", "Illustrative sample data. No model or external API calls are made.", "replay");
        if (demo.Id == "09") trace.Event("Magentic | plan", "1. Define an approval API.\n2. Review dual control and audit requirements.\n3. Plan a pilot with a kill switch.", "plan");
        Step[] steps = Steps(demo.Id);
        if (demo.Id == "06")
            await Task.WhenAll(steps.Select(step => PlayAsync(step, trace, delay, cancellationToken)));
        else
            foreach (Step step in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (demo.Id == "02" && step.Final)
                    trace.Event("Session", "Serialized turn 1 and restored a fresh AgentSession.\n{\"sample\":true,\"customer\":\"Fabrikam\",\"project\":\"Atlas MAF\"}", "session");
                if (demo.Id == "07") trace.Event("Handoff", $"Illustrated owner: {step.Actor}", "handoff");
                if (demo.Id == "09") trace.Event("Magentic | progress", $"Next speaker: {step.Actor}. Review progress before the next assignment.", "progress");
                await PlayAsync(step, trace, delay, cancellationToken);
            }
    }

    /// <summary>Streams one illustrative replay step into the trace.</summary>
    private static async Task PlayAsync(Step step, RunTrace trace, TimeSpan delay, CancellationToken cancellationToken)
    {
        trace.Start(step.Actor);
        if (step.Actor.StartsWith("Tool | ", StringComparison.Ordinal))
            trace.Event(step.Actor, "Sample arguments: {\"location\":\"Pforzheim, Germany\"}", "tool");
        if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
        for (int i = 0; i < step.Text.Length; i += 48)
        {
            cancellationToken.ThrowIfCancellationRequested();
            trace.Append(step.Actor, step.Text.Substring(i, Math.Min(48, step.Text.Length - i)));
            if (delay > TimeSpan.Zero) await Task.Delay(TimeSpan.FromMilliseconds(45), cancellationToken);
        }
        trace.Complete(step.Actor, step.Text);
        if (step.Final) trace.SetFinalOutputs([new TraceResponse(step.Actor, step.Text)]);
    }

    /// <summary>Returns the illustrative steps for the specified demo.</summary>
    private static Step[] Steps(string id) => id switch
    {
        "01" => [new("CruisePlanner", "## A Mediterranean week\nStart and finish in **Genoa**, with stops in Marseille, Barcelona, Palma and La Spezia.\n\n- Choose a ship with lifts and easy access to dining.\n- Leave a relaxed day between long excursions.\n- Check departure dates, accessibility and cabin availability with the operator.\n\nThis is an illustrative itinerary, not a checked sailing or quote.", true)],
        "02" => [new("ProjectMemory", "I've noted your **Atlas MAF** rollout for **Fabrikam** and your preference for concise technical summaries."), new("ProjectMemory", "You are preparing the **Atlas MAF rollout for Fabrikam**. Your preferred communication style is **concise technical summaries**.\n\nThis sample illustrates a follow-up using a restored conversation session.", true)],
        "03" => [new("Azure OpenAI", "### Azure OpenAI - sample\nIn Munich, a developer followed the tramlines to a quiet cafe. Between two sips of coffee, a small idea became the first line of a new project.", true), new("Ollama", "### Ollama - sample\nThe city unfolded one square at a time. By evening, the developer's notebook held a sketch of Munich and the beginnings of an application.", true), new("OpenAI", "### OpenAI - sample\nA walk through Munich turned a difficult bug into a simple question. Back at the keyboard, the developer finally knew where to begin.", true)],
        "04" => [new("WeatherAssistant", "I'll look up the weather and local time using the two available tools."), new("Tool | Weather", "{\"sample\":true,\"location\":\"Pforzheim\",\"temperatureC\":14,\"conditions\":\"partly cloudy\"}"), new("Tool | LocalTime", "{\"sample\":true,\"timeZone\":\"Europe/Berlin\",\"localTime\":\"10:30\"}"), new("WeatherAssistant", "## Pforzheim - illustrative result\nThe sample weather is **14 C and partly cloudy**. The sample local time is **10:30 in Europe/Berlin**.\n\nIn a live run, the framework executes the C# tools and returns their current data to the agent.", true)],
        "05" => [new("ScopeAnalyst", "### Scope\nIntroduce passkey-based step-up authentication for B2B portal administrators. Keep the existing recovery path and define a small pilot."), new("SolutionDrafter", "### Delivery approach\nAdd the step-up challenge, persist audit events and protect the rollout with a tenant-level feature flag. Deliver the pilot before general availability."), new("ArchitectureReviewer", "### Review\nAddress shared-device behavior, recovery abuse and audit completeness. Define rollback criteria and measure sign-in success before expanding the pilot."), new("ExecutiveEditor", "## Passkey step-up - delivery brief\n**Objective:** strengthen sensitive administrator actions while preserving recovery access.\n\n1. Implement the challenge and auditable approval flow.\n2. Start with a small tenant pilot behind a feature flag.\n3. Track success rate, recovery requests and support tickets.\n4. Expand when the agreed acceptance criteria are met.\n\n**Open decisions:** shared-device policy, recovery safeguards and pilot ownership.", true)],
        "06" => [new("LeadershipBrief", "### Leadership perspective\n- Adoption and sign-in success improved.\n- Rising helpdesk demand needs investigation.\n- Expand only after recovery and audit concerns are addressed.", true), new("RiskLens", "### Risk perspective\nShared-device friction, recovery confusion and incomplete audit reporting could block wider adoption. Assign owners and acceptance criteria before expanding.", true), new("SignalSpotter", "### Signals\n| Metric | Pilot result |\n|---|---|\n| Adoption | 68% across 14 tenants |\n| Sign-in success | 91% -> 97% |\n| Support tickets | +23% |\n\nSegment the support data by device and recovery scenario.", true)],
        "07" => [new("IdentityLead", "Coordinate the recovery rollout. Start by asking the security specialist to define controls."), new("SecurityLead", "Require step-up authentication, tenant-aware authorization and immutable audit events. Apply dual control to high-risk tenants."), new("SupportLead", "Prepare operator guidance, recovery communications and an escalation path before the pilot begins."), new("RolloutLead", "Use three pilot tenants, a kill switch and explicit rollback thresholds. Monitor approvals, reset failures and helpdesk demand."), new("IdentityLead", "## Recovery rollout recommendation\nPilot a strongly audited recovery flow with tenant-aware controls, prepared support teams and a reversible rollout.\n\n**Owners:** security defines safeguards; support prepares guidance; rollout owns flags, monitoring and rollback.", true)],
        "08" => [new("BridgeLead", "Let's separate authentication-flow changes from infrastructure signals. Identity and reliability engineers, share the most useful next check."), new("IdentityEngineer", "The passkey regression and fallback timeouts followed an auth-gateway deployment. Compare token handling and the recovery path against the previous build."), new("ReliabilityEngineer", "Cache misses and session-store latency increased together. Check whether the deployment changed cache keys or amplified store writes; prepare a controlled rollback."), new("BridgeLead", "## Final incident update\nThe deployment is the leading hypothesis, with cache behavior and session-store pressure as the next checks.\n\n- **Containment:** validate a controlled rollback.\n- **Identity:** compare token and fallback changes.\n- **Reliability:** correlate cache misses with store latency.\n\nThe cause remains a hypothesis until verified.", true)],
        "09" => [new("DeliveryLead", "### Task plan\nAsk architecture for the API shape, risk for the control model, and rollout for a reversible pilot."), new("IdentityArchitect", "Use a step-up protected approval API, short-lived reset grants and an append-only audit log. Keep the approval check separate from the reset operation."), new("RiskAnalyst", "Enforce tenant boundaries, dual control for high-risk tenants, grant expiry and exportable audit records. Reject replayed or already-used grants."), new("RolloutEngineer", "Start with three pilot tenants. Add a kill switch, approval-latency monitoring and a rollback rehearsal."), new("DeliveryLead", "## Supervised recovery proposal\nBuild a **step-up protected approval service** issuing short-lived, single-use grants.\n\n- **Controls:** tenant authorization and dual control for high-risk tenants.\n- **Audit:** exportable, append-only approval and reset records.\n- **Performance:** measure the approval-check path against the 250 ms p95 target.\n- **Rollout:** three pilot tenants, a kill switch and verified rollback.\n\nThe manager combines the specialists' contributions into this final proposal.", true)],
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

}
