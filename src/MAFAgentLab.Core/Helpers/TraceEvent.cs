namespace MAFAgentLab.Core.Helpers;

/// <summary>Represents one timestamped event captured during a run.</summary>
public sealed record TraceEvent(long Id, TimeSpan Elapsed, string Actor, string Detail, string Kind);
