using System.Text;

namespace MAFAgentLab.Core.Helpers;

/// <summary>Stores mutable execution state for one actor lane.</summary>
internal sealed class Lane
{
    /// <summary>Gets or sets the current lane state.</summary>
    public string State { get; set; } = "waiting";

    /// <summary>Gets or sets the number of started turns.</summary>
    public int Turns { get; set; }

    /// <summary>Gets or sets the elapsed time at which the active turn started.</summary>
    public TimeSpan StartedAt { get; set; }

    /// <summary>Gets or sets accumulated execution time.</summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>Gets the buffer for the active response.</summary>
    public StringBuilder Text { get; } = new();
}
