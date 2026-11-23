namespace MAFAgentLab.Core.Helpers;

/// <summary>Represents one response captured during a run.</summary>
public sealed record TraceResponse(string? Actor, string Text, long Id = 0, bool IsFinal = false, bool IsTool = false);
