namespace MAFAgentLab.Core.Helpers;

/// <summary>Contains the final workflow text and the associated execution trace.</summary>
internal sealed record WorkflowResult(string Text, RunTrace Trace);
