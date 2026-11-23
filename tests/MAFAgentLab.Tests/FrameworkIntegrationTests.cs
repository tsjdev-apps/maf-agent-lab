using System.Text.Json;
using MAFAgentLab.Core.Demos;
using MAFAgentLab.Core.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace MAFAgentLab.Tests;

/// <summary>Verifies demos through the real Agent Framework execution pipeline.</summary>
public sealed class FrameworkIntegrationTests
{
    /// <summary>Creates an empty demo context for a test run.</summary>
    private static DemoContext Context(CancellationToken cancellationToken) =>
        new(new ConfigurationBuilder().Build(), cancellationToken);

    [Fact]
    public async Task StreamingAgentRecordsIntermediateAndFinalResponses()
    {
        int turn = 0;
        using ScriptedChatClient client = new((_, _) => ScriptedChatClient.Answer(++turn == 1 ? "first" : "second"));
        AIAgent agent = client.AsAIAgent(name: "Assistant");
        DemoContext context = Context(TestContext.Current.CancellationToken);

        await context.StreamAgentAsync(agent, "first prompt", isFinal: false);
        await context.StreamAgentAsync(agent, "second prompt");

        Assert.Collection(context.Trace.Responses,
            response => { Assert.Equal("first", response.Text); Assert.False(response.IsFinal); },
            response => { Assert.Equal("second", response.Text); Assert.True(response.IsFinal); });
    }

    /// <summary>Verifies that a sequential workflow forwards each earlier response.</summary>
    [Fact]
    public async Task SequentialWorkflowForwardsEarlierAgentResponses()
    {
        using ScriptedChatClient client = new((messages, _) => ScriptedChatClient.Answer($"stage-{messages.Count(message => message.Text.StartsWith("stage-", StringComparison.Ordinal)) + 1}"));
        WorkflowResult result = await WorkflowRunner.RunAsync(MultiAgentWorkflowSequential.CreateWorkflow(client), "initial request", Context(TestContext.Current.CancellationToken));
        Assert.Equal("stage-4", result.Text);
        Assert.Equal(4, client.Requests.Count);
        Assert.Contains(client.Requests.Last(), message => message.Text == "stage-3");
        Assert.Equal(4, result.Trace.Responses.Count);
        Assert.Single(result.Trace.Responses, response => response.IsFinal && response.Text == "stage-4");
        Assert.False(result.Trace.HasRunningLanes);
    }

    /// <summary>Verifies that a concurrent workflow retains every specialist response.</summary>
    [Fact]
    public async Task ConcurrentWorkflowCollectsEveryAgentResponse()
    {
        using ScriptedChatClient client = new((_, options) => ScriptedChatClient.Answer(
            options?.Instructions?.Contains("exactly 3", StringComparison.Ordinal) == true ? "leadership perspective" :
            options?.Instructions?.Contains("risks, blockers", StringComparison.Ordinal) == true ? "risk perspective" : "metrics perspective"));
        WorkflowResult result = await WorkflowRunner.RunAsync(MultiAgentWorkflowConcurrent.CreateWorkflow(client), "rollout update", Context(TestContext.Current.CancellationToken), collectAll: true);
        Assert.Contains("leadership perspective", result.Text);
        Assert.Contains("risk perspective", result.Text);
        Assert.Contains("metrics perspective", result.Text);
        Assert.Equal(3, client.Requests.Count);
        Assert.Equal(3, result.Trace.Responses.Count);
        Assert.All(result.Trace.Responses, response => Assert.True(response.IsFinal));
        Assert.False(result.Trace.HasRunningLanes);
    }

    /// <summary>Verifies that a serialized session restores conversation context.</summary>
    [Fact]
    public async Task SessionCanBeSerializedRestoredAndUsedForFollowUp()
    {
        using ScriptedChatClient client = new((messages, _) => ScriptedChatClient.Answer(
            messages.Any(message => message.Role == ChatRole.User && message.Text == "Remember Fabrikam") ? "Fabrikam" : "missing context"));
        AIAgent agent = client.AsAIAgent(name: "Memory");
        AgentSession session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
        await agent.RunAsync("Remember Fabrikam", session, cancellationToken: TestContext.Current.CancellationToken);
        JsonElement state = await agent.SerializeSessionAsync(session, cancellationToken: TestContext.Current.CancellationToken);
        AgentSession restored = await agent.DeserializeSessionAsync(state, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotSame(session, restored);
        AgentResponse result = await agent.RunAsync("Which customer?", restored, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Fabrikam", result.Text);
        Assert.Contains(client.Requests.Last(), message => message.Role == ChatRole.User && message.Text == "Remember Fabrikam");
    }

    /// <summary>Verifies framework function selection and result round-tripping.</summary>
    [Fact]
    public async Task ToolCallsAreExecutedByFrameworkAndReturnedToModel()
    {
        int calls = 0;
        AIFunction tool = AIFunctionFactory.Create(() => { calls++; return "tool-result"; }, "Lookup");
        using ScriptedChatClient client = new((messages, _) => messages.Any(message => message.Role == ChatRole.Tool)
            ? ScriptedChatClient.Answer("grounded answer")
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "Lookup")])));
        AIAgent agent = client.AsAIAgent(name: "ToolAgent", tools: [tool]);
        AgentResponse result = await agent.RunAsync("Use Lookup", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
        Assert.Equal("grounded answer", result.Text);
        Assert.Contains(client.Requests.Last().SelectMany(message => message.Contents).OfType<FunctionResultContent>(), content => content.Result?.ToString() == "tool-result");
    }

    /// <summary>Verifies group chat visits both engineers before accepting a final update.</summary>
    [Fact]
    public async Task GroupChatVisitsEngineersBeforeLeadCanTerminate()
    {
        using ScriptedChatClient client = new((_, _) => ScriptedChatClient.Answer("FINAL UPDATE: concise incident summary"));
        WorkflowResult result = await WorkflowRunner.RunAsync(MultiAgentWorkflowGroupChat.CreateWorkflow(client), "incident", Context(TestContext.Current.CancellationToken));
        Assert.Contains("concise incident summary", result.Text);
        Assert.Equal(4, client.Requests.Count);
        Assert.Equal(4, result.Trace.Responses.Count);
        Assert.False(result.Trace.HasRunningLanes);
    }

    /// <summary>Verifies that a handoff tool routes work to a specialist.</summary>
    [Fact]
    public async Task HandoffUsesFrameworkTransferToolToReachSpecialist()
    {
        using ScriptedChatClient client = new((_, options) =>
        {
            if (options?.Instructions?.Contains("lead coordinator", StringComparison.Ordinal) == true)
            {
                AIFunctionDeclaration route = options.Tools!.OfType<AIFunctionDeclaration>()
                    .Single(tool => tool.Description.Contains("abuse prevention", StringComparison.Ordinal));
                return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("handoff-1", route.Name, new Dictionary<string, object?> { ["reasonForHandoff"] = "Audit controls need specialist review" })]));
            }
            return ScriptedChatClient.Answer("security specialist recommendation");
        });
        WorkflowResult result = await WorkflowRunner.RunAsync(MultiAgentWorkflowHandoff.CreateWorkflow(client), "review audit controls", Context(TestContext.Current.CancellationToken));
        Assert.Contains("security specialist recommendation", result.Text);
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains(result.Trace.Responses, response => response.Actor == "SecurityLead");
        Assert.False(result.Trace.HasRunningLanes);
    }

    /// <summary>Verifies native Magentic planning, delegation, and synthesis.</summary>
    [Fact]
    public async Task NativeMagenticPlansDelegatesAndSynthesizes()
    {
        int call = 0;
        using ScriptedChatClient client = new((_, _) => ScriptedChatClient.Answer(Interlocked.Increment(ref call) switch
        {
            1 => "Known facts: supervised recovery requires audited approval.",
            2 => "Plan: ask IdentityArchitect for the API, then synthesize the proposal.",
            3 => Ledger(false),
            4 => "Use a step-up protected approval API and an append-only audit log.",
            5 => Ledger(true),
            6 => "Final delivery proposal: audited approval API with pilot rollout.",
            _ => throw new InvalidOperationException($"Unexpected Magentic model call {call}.")
        }));
        WorkflowResult result = await WorkflowRunner.RunAsync(MagenticPlannerWorkflow.CreateWorkflow(client), "Design supervised recovery", Context(TestContext.Current.CancellationToken));
        Assert.Contains("Final delivery proposal", result.Text);
        Assert.Equal(6, call);
        Assert.Contains(result.Trace.Responses, response => response.Actor == "IdentityArchitect");
        Assert.False(result.Trace.HasRunningLanes);
    }

    /// <summary>Creates a scripted Magentic progress ledger.</summary>
    private static string Ledger(bool satisfied) => $$"""
        {
          "is_request_satisfied": {"reason": "{{(satisfied ? "Architecture is available" : "Architecture needed")}}", "answer": {{satisfied.ToString().ToLowerInvariant()}}},
          "is_in_loop": {"reason": "No repetition", "answer": false},
          "is_progress_being_made": {"reason": "Working through plan", "answer": true},
          "next_speaker": {"reason": "Architecture first", "answer": "IdentityArchitect"},
          "instruction_or_question": {"reason": "Need API shape", "answer": "Describe the approval API and audit log."}
        }
        """;

    /// <summary>Verifies that provider failures propagate instead of returning partial success.</summary>
    [Fact]
    public async Task WorkflowErrorsPropagateInsteadOfReportingPartialSuccess()
    {
        using ScriptedChatClient client = new((_, _) => throw new HttpRequestException("provider unavailable"));
        await Assert.ThrowsAnyAsync<Exception>(() => WorkflowRunner.RunAsync(MultiAgentWorkflowSequential.CreateWorkflow(client), "request", Context(TestContext.Current.CancellationToken)));
    }

    /// <summary>Verifies that a pre-cancelled workflow does not report success.</summary>
    [Fact]
    public async Task CancelledWorkflowDoesNotReturnSuccess()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        using ScriptedChatClient client = new((_, _) => ScriptedChatClient.Answer("unexpected"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WorkflowRunner.RunAsync(MultiAgentWorkflowSequential.CreateWorkflow(client), "request", Context(cancellation.Token)));
    }

    /// <summary>Verifies cancellation reaches an in-flight model request.</summary>
    [Fact]
    public async Task CancellationReachesAnInFlightModelCall()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using ScriptedChatClient client = new((_, _) => ScriptedChatClient.Answer("unexpected")) { ResponseDelay = Timeout.InfiniteTimeSpan };
        Task<WorkflowResult> run = WorkflowRunner.RunAsync(MultiAgentWorkflowSequential.CreateWorkflow(client), "request", Context(cancellation.Token));
        await client.RequestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }
}
