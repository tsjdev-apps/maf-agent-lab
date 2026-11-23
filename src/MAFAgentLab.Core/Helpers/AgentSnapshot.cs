namespace MAFAgentLab.Core.Helpers;

/// <summary>Represents the current state of one agent or tool lane.</summary>
public sealed record AgentSnapshot(string Name, string State, int Turns, TimeSpan Duration);
