using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Core.Helpers;

/// <summary>Executes Agent Framework workflows and records their observable events.</summary>
internal static class WorkflowRunner
{
    /// <summary>Runs a workflow, records its events, and returns its final output.</summary>
    public static async Task<WorkflowResult> RunAsync(Workflow workflow, string prompt, DemoContext context, bool collectAll = false)
    {
        RunTrace trace = context.Trace;
        string? finalOutput = null;
        IReadOnlyList<TraceResponse> finalResponses = [];
        int responseCount = 0;
        using CancellationTokenSource execution = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        // Pass the token to execution as well as observation: stopping the UI alone does not cancel a workflow.
        await using StreamingRun run = await InProcessExecution.Concurrent.RunStreamingAsync(
            workflow, new List<ChatMessage> { new(ChatRole.User, prompt) }, Guid.NewGuid().ToString(), execution.Token);
        if (!await run.TrySendMessageAsync(new TurnToken(emitEvents: true)))
            throw new InvalidOperationException("The workflow did not accept its initial turn token.");

        try
        {
            await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(context.CancellationToken))
            {
                switch (workflowEvent)
                {
                    case WorkflowStartedEvent:
                        trace.Event("Workflow", "started");
                        break;
                    case ExecutorInvokedEvent invoked:
                        // Executors also receive setup/history messages. Only a TurnToken starts inference.
                        if (invoked.Data is TurnToken && IsAgentExecutor(invoked.ExecutorId))
                            trace.Start(AgentName(invoked.ExecutorId));
                        break;
                    case AgentResponseUpdateEvent update:
                        trace.Append(AgentName(update.ExecutorId), update.Update.Text);
                        foreach (FunctionCallContent tool in update.Update.Contents.OfType<FunctionCallContent>())
                            trace.Event(AgentName(update.ExecutorId), $"Requested {tool.Name}: {System.Text.Json.JsonSerializer.Serialize(tool.Arguments)}", "tool");
                        break;
                    case AgentResponseEvent response:
                        trace.Complete(AgentName(response.ExecutorId), response.Response.Text);
                        if (++responseCount > 24)
                            throw new InvalidOperationException("Stopped after 24 agent responses. Refine the request to limit routing loops.");
                        break;
                    case ExecutorCompletedEvent completed when IsAgentExecutor(completed.ExecutorId):
                        // Sequential/concurrent builders can emit deltas without a full AgentResponseEvent.
                        trace.CompleteActive(AgentName(completed.ExecutorId));
                        break;
                    case ExecutorFailedEvent failed:
                        context.CancellationToken.ThrowIfCancellationRequested();
                        trace.Fail(AgentName(failed.ExecutorId), "executor failed");
                        if (failed.Data is Exception cause)
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cause).Throw();
                        throw new InvalidOperationException($"Executor {AgentName(failed.ExecutorId)} failed.");
                    case MagenticPlanCreatedEvent plan:
                        trace.Event("Magentic | plan", plan.FullTaskLedger.Text, "plan");
                        break;
                    case MagenticReplannedEvent replan:
                        trace.Event("Magentic | replan", replan.FullTaskLedger.Text, "plan");
                        break;
                    case MagenticProgressLedgerUpdatedEvent progress:
                        trace.Event("Magentic | progress", $"Next: {progress.ProgressLedger.NextSpeaker}. {progress.ProgressLedger.InstructionOrQuestion} Satisfied: {progress.ProgressLedger.IsRequestSatisfied}", "progress");
                        break;
                    case WorkflowOutputEvent output:
                        (string? text, IReadOnlyList<TraceResponse> responses) = ExtractOutput(output.Data, collectAll);
                        if (text is not null)
                        {
                            finalOutput = text;
                            finalResponses = responses;
                        }
                        trace.Event("Workflow", "output received");
                        break;
                    case WorkflowErrorEvent error:
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                            error.Exception ?? new InvalidOperationException("The workflow reported an error.")).Throw();
                        break;
                    case RequestInfoEvent:
                        throw new InvalidOperationException("This demo received an unexpected external input request.");
                }
            }
            context.CancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(finalOutput))
                throw new InvalidOperationException("The workflow ended without a final text output. Any intermediate responses are incomplete.");
            trace.Event("Workflow", "finished");
        }
        catch (Exception error)
        {
            await execution.CancelAsync();
            trace.AbortRunning(error is OperationCanceledException ? "cancelled" : "failed", cancelled: error is OperationCanceledException);
            trace.Event("Workflow", error is OperationCanceledException ? "cancelled" : error.Message, error is OperationCanceledException ? "cancelled" : "error");
            throw;
        }
        trace.SetFinalOutputs(finalResponses);
        return new WorkflowResult(finalOutput!, trace);
    }

    /// <summary>Determines whether an executor identifier represents an agent executor.</summary>
    private static bool IsAgentExecutor(string id) => !id.Contains('/') && id.Length > 33 && id[^33] == '_' && Guid.TryParseExact(id[^32..], "N", out _);

    /// <summary>Extracts the display name from an executor identifier.</summary>
    internal static string AgentName(string id) => IsAgentExecutor(id) ? id[..^33] : id;

    /// <summary>Extracts final text and trace responses from a workflow payload.</summary>
    internal static (string? Text, IReadOnlyList<TraceResponse> Responses) ExtractOutput(object? value, bool collectAll) => value switch
    {
        string text when !string.IsNullOrWhiteSpace(text) => (text, [new TraceResponse(null, text)]),
        AgentResponse response => (response.Text, [new TraceResponse(response.Messages.LastOrDefault(message => message.Role == ChatRole.Assistant)?.AuthorName, response.Text)]),
        ChatMessage message when message.Role == ChatRole.Assistant => (message.Text, [new TraceResponse(message.AuthorName, message.Text)]),
        IEnumerable<ChatMessage> messages => ExtractMessageOutput(messages, collectAll),
        _ => (null, [])
    };

    /// <summary>Selects and formats assistant messages that represent the final output.</summary>
    private static (string? Text, IReadOnlyList<TraceResponse> Responses) ExtractMessageOutput(IEnumerable<ChatMessage> messages, bool collectAll)
    {
        IEnumerable<ChatMessage> assistantMessages = messages.Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text));
        ChatMessage[] selected = (collectAll ? assistantMessages : assistantMessages.TakeLast(1)).ToArray();
        if (selected.Length == 0) return (null, []);
        string text = collectAll
            ? string.Join("\n\n", selected.Select(message => $"{message.AuthorName ?? "Agent"}\n{message.Text}"))
            : selected[^1].Text;
        return (text, selected.Select(message => new TraceResponse(message.AuthorName, message.Text)).ToArray());
    }
}
