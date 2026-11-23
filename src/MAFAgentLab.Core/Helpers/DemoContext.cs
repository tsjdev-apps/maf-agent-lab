using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace MAFAgentLab.Core.Helpers;

/// <summary>Contains configuration, cancellation, input, and trace state for a demo run.</summary>
public sealed record DemoContext(IConfiguration Configuration, CancellationToken CancellationToken, string? Prompt = null)
{
    /// <summary>Gets the trace populated during this run.</summary>
    public RunTrace Trace { get; init; } = new();

    /// <summary>Gets or initializes the optional follow-up prompt.</summary>
    public string? FollowUp { get; init; }

    /// <summary>Returns the configured prompt or the supplied example.</summary>
    public string GetPrompt(string example) => string.IsNullOrWhiteSpace(Prompt) ? example : Prompt;

    /// <summary>Creates and runs a workflow with the selected chat client.</summary>
    public async Task RunWorkflowAsync(Func<IChatClient, Workflow> createWorkflow, string example, bool collectAll = false)
    {
        string prompt = GetPrompt(example);
        using IChatClient client = ChatClientHelper.CreateAgentChatClient(Configuration);
        await WorkflowRunner.RunAsync(createWorkflow(client), prompt, this, collectAll);
    }

    /// <summary>Streams an agent response into the run trace.</summary>
    public async Task StreamAgentAsync(AIAgent agent, string prompt, AgentSession? session = null, bool isFinal = true)
    {
        string name = agent.Name ?? "Agent";
        System.Text.StringBuilder text = new();
        Trace.Start(name);
        try
        {
            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(prompt, session, cancellationToken: CancellationToken))
            {
                text.Append(update.Text);
                Trace.Append(name, update.Text);
                foreach (FunctionCallContent tool in update.Contents.OfType<FunctionCallContent>())
                    Trace.Event(name, $"Requested {tool.Name}: {System.Text.Json.JsonSerializer.Serialize(tool.Arguments)}", "tool");
            }
            string response = text.ToString();
            Trace.Complete(name, response);
            if (isFinal && text.Length > 0) Trace.SetFinalOutputs([new TraceResponse(name, response)]);
        }
        catch (Exception error)
        {
            Trace.AbortRunning(error is OperationCanceledException ? "Cancelled" : "Failed", cancelled: error is OperationCanceledException);
            throw;
        }
    }
}
