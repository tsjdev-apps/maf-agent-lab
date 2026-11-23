namespace MAFAgentLab.Core.Helpers;

/// <summary>Represents an immutable point-in-time view of a run.</summary>
public sealed record RunSnapshot(
    string State, TimeSpan Elapsed, IReadOnlyList<AgentSnapshot> Agents,
    IReadOnlyList<TraceEvent> Events, IReadOnlyList<TraceResponse> Responses, string? Error);
