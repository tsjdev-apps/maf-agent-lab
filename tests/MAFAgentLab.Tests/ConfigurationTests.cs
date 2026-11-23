using MAFAgentLab.Core.Demos;
using MAFAgentLab.Core.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace MAFAgentLab.Tests;

/// <summary>Verifies provider configuration and framework output behavior.</summary>
public sealed class ConfigurationTests
{
    /// <summary>Verifies that Ollama-only configuration supports provider comparison and agent runs.</summary>
    [Fact]
    public void ProviderComparisonRunsWithOnlyOllamaConfigured()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ollama:Endpoint"] = "http://localhost:11434",
            ["Ollama:Deployment"] = "local-model"
        }).Build();
        Assert.Empty(DemoCatalog.Find("3")!.GetMissingRequirements(config));
        Assert.Empty(DemoCatalog.Find("1")!.GetMissingRequirements(config));
        Assert.Equal("Ollama", ConfigurationHelper.GetAgentProvider(config));
        config["Agent:Provider"] = "AzureOpenAI";
        Assert.NotEmpty(DemoCatalog.Find("1")!.GetMissingRequirements(config));
    }

    /// <summary>Verifies that official OpenAI configuration does not require a custom endpoint.</summary>
    [Fact]
    public void OfficialOpenAIUsesItsDefaultEndpoint()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "test-key",
            ["OpenAI:Model"] = "gpt-4.1-mini"
        }).Build();

        Assert.Empty(ConfigurationHelper.GetMissingOpenAISettings(config));
        Assert.Equal("OpenAI", ConfigurationHelper.GetAgentProvider(config));
    }

    /// <summary>Verifies independent readiness reporting for the weather API key.</summary>
    [Fact]
    public void WeatherKeyHasAnIndependentConfigurationStatus()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        Assert.Equal(["OpenWeatherMap:ApiKey"], ConfigurationHelper.GetMissingWeatherSettings(config));

        config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenWeatherMap:ApiKey"] = "weather-key"
        }).Build();
        Assert.Empty(ConfigurationHelper.GetMissingWeatherSettings(config));
    }

    /// <summary>Verifies that non-HTTP endpoint values are rejected.</summary>
    [Theory]
    [InlineData("file:///etc/hosts")]
    [InlineData("relative/path")]
    public void RejectsNonHttpEndpoints(string endpoint)
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:Endpoint"] = endpoint }).Build();
        Assert.Throws<InvalidOperationException>(() => ConfigurationHelper.GetEndpoint(config, "OpenAI:Endpoint"));
    }

    /// <summary>Verifies that group chat termination only honors an eligible bridge lead update.</summary>
    [Fact]
    public void GroupChatDoesNotStopOnUserOrWorkerMarkers()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "FINAL UPDATE: stop"),
            new(ChatRole.Assistant, "analysis") { AuthorName = "IdentityEngineer" },
            new(ChatRole.Assistant, "FINAL UPDATE: worker") { AuthorName = "ReliabilityEngineer" }
        ];
        Assert.False(MultiAgentWorkflowGroupChat.ShouldTerminate(messages));
        Assert.True(MultiAgentWorkflowGroupChat.ShouldTerminate([.. messages, new(ChatRole.Assistant, "FINAL UPDATE: summary") { AuthorName = "BridgeLead" }]));
    }

    /// <summary>Verifies final output selection for concurrent workflow responses.</summary>
    [Fact]
    public void OutputExtractionKeepsAllConcurrentPerspectivesAndSkipsUserEcho()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "request"),
            new(ChatRole.Assistant, "risk finding") { AuthorName = "Risk" },
            new(ChatRole.Assistant, "metric finding") { AuthorName = "Metrics" }
        ];
        (string? lastText, IReadOnlyList<TraceResponse> lastResponses) = WorkflowRunner.ExtractOutput(messages, false);
        Assert.Equal("metric finding", lastText);
        Assert.Collection(lastResponses, response =>
        {
            Assert.Equal("Metrics", response.Actor);
            Assert.Equal("metric finding", response.Text);
        });

        (string? allText, IReadOnlyList<TraceResponse> allResponses) = WorkflowRunner.ExtractOutput(messages, true);
        Assert.Equal("Risk\nrisk finding\n\nMetrics\nmetric finding", allText);
        Assert.Equal(["Risk", "Metrics"], allResponses.Select(response => response.Actor));
        Assert.Null(WorkflowRunner.ExtractOutput(messages.Take(1), true).Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutputExtractionEnumeratesMessagesOnce(bool collectAll)
    {
        int enumerations = 0;
        IEnumerable<ChatMessage> Messages()
        {
            if (++enumerations > 1) throw new InvalidOperationException("Messages were enumerated more than once.");
            yield return new ChatMessage(ChatRole.User, "request");
            yield return new ChatMessage(ChatRole.Assistant, "first") { AuthorName = "One" };
            yield return new ChatMessage(ChatRole.Assistant, "second") { AuthorName = "Two" };
        }

        (string? text, IReadOnlyList<TraceResponse> responses) = WorkflowRunner.ExtractOutput(Messages(), collectAll);

        Assert.Equal(1, enumerations);
        Assert.Equal(collectAll ? "One\nfirst\n\nTwo\nsecond" : "second", text);
        Assert.Equal(collectAll ? new[] { "One", "Two" } : ["Two"], responses.Select(response => response.Actor));
    }

    [Fact]
    public void OutputExtractionHandlesIndividualMessagesAndInvalidOutput()
    {
        (string? text, IReadOnlyList<TraceResponse> responses) =
            WorkflowRunner.ExtractOutput(new ChatMessage(ChatRole.Assistant, "answer") { AuthorName = "Lead" }, false);
        Assert.Equal("answer", text);
        Assert.Equal(new TraceResponse("Lead", "answer"), Assert.Single(responses));
        Assert.Null(WorkflowRunner.ExtractOutput(new ChatMessage(ChatRole.User, "request"), false).Text);
        Assert.Null(WorkflowRunner.ExtractOutput("  ", false).Text);
        Assert.Equal("answer", WorkflowRunner.ExtractOutput("answer", false).Text);
    }
}
