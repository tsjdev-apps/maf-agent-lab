using MAFAgentLab.Core.Helpers;
using Microsoft.Extensions.Configuration;

namespace MAFAgentLab.Console;

/// <summary>Coordinates validation and execution of console demo runs.</summary>
public static class ConsoleDemoRunner
{
    /// <summary>Runs one live or replayed demo and records its terminal state.</summary>
    public static async Task RunAsync(
        DemoDefinition demo,
        IConfiguration configuration,
        RunTrace trace,
        string? prompt = null,
        string? followUp = null,
        bool replay = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (replay)
            {
                await GuidedReplay.RunAsync(demo, trace, cancellationToken);
            }
            else
            {
                IReadOnlyList<string> missing = demo.GetMissingRequirements(configuration);
                if (missing.Count > 0)
                    throw new InvalidOperationException($"Missing configuration: {string.Join(", ", missing)}. Configure Visual Studio User Secrets or environment variables.");
                await demo.Run(new DemoContext(configuration, cancellationToken, prompt) { Trace = trace, FollowUp = followUp });
            }
            trace.Finish();
        }
        catch (OperationCanceledException)
        {
            trace.AbortRunning("cancelled", cancelled: true);
            trace.Finish("cancelled", "Run cancelled");
            throw;
        }
        catch (Exception error)
        {
            trace.AbortRunning("failed");
            trace.Finish("failed", error.Message);
            throw;
        }
    }
}
