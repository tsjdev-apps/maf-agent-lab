namespace MAFAgentLab.Console;

/// <summary>Defines the available actions in the console main menu.</summary>
internal enum MainAction
{
    /// <summary>Starts a demo with configured external services.</summary>
    Live,

    /// <summary>Starts a demo using illustrative offline data.</summary>
    Replay,

    /// <summary>Displays the configured service readiness.</summary>
    Configuration,

    /// <summary>Closes the application.</summary>
    Exit
}
