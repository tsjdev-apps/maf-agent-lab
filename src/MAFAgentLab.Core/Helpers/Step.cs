namespace MAFAgentLab.Core.Helpers;

/// <summary>Represents one illustrative action in a guided replay.</summary>
internal sealed record Step(string Actor, string Text, bool Final = false);
