using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace MAFAgentLab.Tests;

/// <summary>Provides deterministic scripted responses for Agent Framework tests.</summary>
internal sealed class ScriptedChatClient(Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> respond) : IChatClient
{
    /// <summary>Gets the requests submitted to this client.</summary>
    public ConcurrentQueue<ChatMessage[]> Requests { get; } = new();

    /// <summary>Gets a completion source signaled when the first request starts.</summary>
    public TaskCompletionSource RequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets or initializes an artificial response delay.</summary>
    public TimeSpan ResponseDelay { get; init; }

    /// <summary>Returns a scripted response for the submitted messages.</summary>
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ChatMessage[] history = messages.ToArray();
        Requests.Enqueue(history);
        RequestStarted.TrySetResult();
        if (ResponseDelay != TimeSpan.Zero) await Task.Delay(ResponseDelay, cancellationToken);
        return respond(history, options);
    }

    /// <summary>Returns the scripted response as a stream of updates.</summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }
    }

    /// <summary>Returns supported chat client services.</summary>
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IChatClient) ? this : null;

    /// <summary>Releases resources owned by the scripted client.</summary>
    public void Dispose() { }

    /// <summary>Creates a simple assistant response containing the supplied text.</summary>
    public static ChatResponse Answer(string text) => new(new ChatMessage(ChatRole.Assistant, text));
}
