using MAFAgentLab.Console;
using MAFAgentLab.Core.Helpers;
using Microsoft.Extensions.Configuration;

namespace MAFAgentLab.Tests;

/// <summary>Verifies trace state, replay execution, and console run behavior.</summary>
public sealed class RunStateTests
{
    /// <summary>Verifies that a final answer is marked without being appended twice.</summary>
    [Fact]
    public void FinalAnswerIsMarkedInsteadOfAppendedAgain()
    {
        RunTrace trace = new();
        trace.Start("Tool | Weather");
        trace.Complete("Tool | Weather", "weather-result");
        trace.Start("Assistant");
        trace.Complete("Assistant", "final-answer");
        trace.SetFinalOutputs([new TraceResponse("Assistant", "final-answer")]);
        trace.Finish();

        RunSnapshot snapshot = trace.Snapshot();
        Assert.Equal(2, snapshot.Responses.Count);
        Assert.Single(snapshot.Responses, response => response.IsFinal && response.Text == "final-answer");
        Assert.Single(snapshot.Responses, response => response.IsTool);
        Assert.Equal("completed", snapshot.State);
    }

    /// <summary>Verifies final marking preserves authors and earlier duplicate turns.</summary>
    [Fact]
    public void FinalMarkingPreservesOtherAuthorsAndEarlierTurns()
    {
        RunTrace trace = new();
        foreach (string actor in new[] { "Worker", "Lead", "Lead" })
        {
            trace.Start(actor);
            trace.Complete(actor, "same text");
        }
        trace.SetFinalOutputs([new TraceResponse("Lead", "same text")]);
        Assert.Equal(3, trace.Responses.Count);
        Assert.Single(trace.Responses, response => response.IsFinal);
        Assert.True(trace.Responses[^1].IsFinal);
    }

    [Fact]
    public void StreamingResponsesRemainSeparateAcrossTurns()
    {
        RunTrace trace = new();
        trace.Append("Assistant", "first ");
        trace.Append("Assistant", "turn");
        trace.CompleteActive("Assistant");
        trace.Append("Assistant", "second");
        trace.CompleteActive("Assistant");

        Assert.Equal(["first turn", "second"], trace.Responses.Select(response => response.Text));
        Assert.Equal(2, Assert.Single(trace.Snapshot().Agents).Turns);
    }

    /// <summary>Verifies every demo has a prompt and completes its guided replay.</summary>
    [Theory]
    [InlineData("01")]
    [InlineData("02")]
    [InlineData("03")]
    [InlineData("04")]
    [InlineData("05")]
    [InlineData("06")]
    [InlineData("07")]
    [InlineData("08")]
    [InlineData("09")]
    public async Task EveryDemoHasAPromptAndCompletesItsGuidedReplay(string id)
    {
        DemoDefinition demo = DemoCatalog.Find(id)!;
        Assert.False(string.IsNullOrWhiteSpace(demo.ExamplePrompt));
        RunTrace trace = new();
        await GuidedReplay.RunAsync(demo, trace, TestContext.Current.CancellationToken, TimeSpan.Zero);
        Assert.False(trace.HasRunningLanes);
        Assert.Contains(trace.Responses, response => response.IsFinal);
        Assert.Contains(trace.Snapshot().Events, entry => entry.Kind == "replay");
    }

    /// <summary>Verifies missing live configuration produces an actionable failed trace.</summary>
    [Fact]
    public async Task MissingLiveConfigurationProducesAnActionableFailure()
    {
        RunTrace trace = new();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ConsoleDemoRunner.RunAsync(
            DemoCatalog.Find("01")!,
            new ConfigurationBuilder().Build(),
            trace,
            "request",
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("failed", trace.Snapshot().State);
        Assert.Contains("AzureOpenAI:Endpoint", trace.Snapshot().Error!);
        Assert.Empty(trace.Responses);
    }

    /// <summary>Verifies provider comparison replay follows the configured provider order.</summary>
    [Fact]
    public async Task ProviderComparisonReplaysInTheConfiguredOrder()
    {
        RunTrace trace = new();
        await ConsoleDemoRunner.RunAsync(
            DemoCatalog.Find("03")!,
            new ConfigurationBuilder().Build(),
            trace,
            replay: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["Azure OpenAI", "Ollama", "OpenAI"],
            trace.Responses.Where(response => response.IsFinal).Select(response => response.Actor));
        Assert.Equal("completed", trace.Snapshot().State);
    }

    /// <summary>Verifies cancellation stops a guided replay and its active lanes.</summary>
    [Fact]
    public async Task RunnerCanStopAReplay()
    {
        RunTrace trace = new();
        using CancellationTokenSource cancellation = new();
        Task run = ConsoleDemoRunner.RunAsync(
            DemoCatalog.Find("05")!,
            new ConfigurationBuilder().Build(),
            trace,
            "request",
            replay: true,
            cancellationToken: cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal("cancelled", trace.Snapshot().State);
        Assert.False(trace.HasRunningLanes);
        Assert.All(trace.Snapshot().Agents, lane => Assert.NotEqual("failed", lane.State));
    }
}
