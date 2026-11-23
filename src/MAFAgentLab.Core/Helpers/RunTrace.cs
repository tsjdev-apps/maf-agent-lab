using System.Diagnostics;

namespace MAFAgentLab.Core.Helpers;

/// <summary>Thread-safe execution state shared by providers, framework events and the UI. Contains no rendering code.</summary>
public sealed class RunTrace
{
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, Lane> _lanes = [];
    private readonly List<TraceEvent> _events = [];
    private readonly List<TraceResponse> _responses = [];
    private long _sequence;
    private string _state = "running";
    private string? _error;

    /// <summary>Gets the recorded responses.</summary>
    public IReadOnlyList<TraceResponse> Responses { get { lock (_gate) return _responses.ToArray(); } }

    /// <summary>Gets a value indicating whether any participant is still running.</summary>
    public bool HasRunningLanes { get { lock (_gate) return _lanes.Values.Any(lane => lane.State == "running"); } }

    /// <summary>Creates a thread-safe snapshot of the current run state.</summary>
    public RunSnapshot Snapshot()
    {
        lock (_gate)
            return new RunSnapshot(_state, _clock.Elapsed,
                _lanes.Select(entry => new AgentSnapshot(entry.Key, entry.Value.State, entry.Value.Turns,
                    entry.Value.Elapsed + (entry.Value.State == "running" ? _clock.Elapsed - entry.Value.StartedAt : TimeSpan.Zero)))
                    .ToArray(), _events.ToArray(), _responses.ToArray(), _error);
    }

    /// <summary>Records an execution event for an actor.</summary>
    public void Event(string actor, string detail, string kind = "info")
    {
        lock (_gate) _events.Add(new TraceEvent(++_sequence, _clock.Elapsed, actor, detail, kind));
    }

    /// <summary>Marks an actor as running and starts a new turn.</summary>
    public void Start(string actor)
    {
        lock (_gate)
        {
            Lane lane = GetLane(actor);
            if (lane.State == "running") return;
            lane.State = "running";
            lane.StartedAt = _clock.Elapsed;
            lane.Turns++;
            lane.Text.Clear();
            Event(actor, IsTool(actor) ? "Tool execution started" : "Agent turn started", "started");
        }
    }

    /// <summary>Appends streaming text to an actor's active lane.</summary>
    public void Append(string actor, string text)
    {
        lock (_gate)
        {
            Start(actor);
            GetLane(actor).Text.Append(text);
        }
    }

    /// <summary>Completes an actor turn and records its response.</summary>
    public void Complete(string actor, string? text = null)
    {
        lock (_gate)
        {
            Lane lane = GetLane(actor);
            if (lane.State != "running" && string.IsNullOrWhiteSpace(text)) return;
            if (lane.State == "running") lane.Elapsed += _clock.Elapsed - lane.StartedAt;
            lane.State = "done";
            string response = text ?? lane.Text.ToString();
            if (!string.IsNullOrWhiteSpace(response))
                _responses.Add(new TraceResponse(actor, response, ++_sequence, IsTool: IsTool(actor)));
            lane.Text.Clear();
            Event(actor, IsTool(actor) ? "Tool result returned" : "Agent turn completed", "completed");
        }
    }

    /// <summary>Completes an actor only when its lane is currently running.</summary>
    public void CompleteActive(string actor)
    {
        lock (_gate)
            if (_lanes.TryGetValue(actor, out Lane? lane) && lane.State == "running") Complete(actor);
    }

    /// <summary>Marks matching workflow contributions as final outputs.</summary>
    public void SetFinalOutputs(IReadOnlyList<TraceResponse> outputs)
    {
        lock (_gate)
        {
            HashSet<long> marked = [];
            foreach (TraceResponse output in outputs)
            {
                int match = _responses.FindLastIndex(response => !marked.Contains(response.Id)
                    && !response.IsTool && response.Text == output.Text
                    && (output.Actor is null || response.Actor == output.Actor));
                if (match >= 0)
                {
                    _responses[match] = _responses[match] with { IsFinal = true };
                    marked.Add(_responses[match].Id);
                }
                else
                    _responses.Add(output with { Id = ++_sequence, IsFinal = true });
            }
        }
    }

    /// <summary>Marks an actor as failed and records the failure.</summary>
    public void Fail(string actor, string detail)
    {
        lock (_gate)
        {
            Lane lane = GetLane(actor);
            if (lane.State == "running") lane.Elapsed += _clock.Elapsed - lane.StartedAt;
            lane.State = "failed";
            Event(actor, detail, "error");
        }
    }

    /// <summary>Marks an actor as cancelled and records the cancellation.</summary>
    public void Cancel(string actor, string detail)
    {
        lock (_gate)
        {
            Lane lane = GetLane(actor);
            if (lane.State == "running") lane.Elapsed += _clock.Elapsed - lane.StartedAt;
            lane.State = "cancelled";
            Event(actor, detail, "cancelled");
        }
    }

    /// <summary>Aborts every actor lane that is still running.</summary>
    public void AbortRunning(string detail, bool cancelled = false)
    {
        lock (_gate)
            foreach (string actor in _lanes.Where(entry => entry.Value.State == "running").Select(entry => entry.Key).ToArray())
                if (cancelled) Cancel(actor, detail); else Fail(actor, detail);
    }

    /// <summary>Finishes the run and records its terminal state.</summary>
    public void Finish(string state = "completed", string? error = null)
    {
        lock (_gate)
        {
            _state = state;
            _error = error;
            _clock.Stop();
            Event("Workflow", error ?? (state == "completed" ? "Run completed" : "Run cancelled"), state);
        }
    }

    /// <summary>Determines whether an actor represents a tool.</summary>
    private static bool IsTool(string actor) => actor.StartsWith("Tool | ", StringComparison.Ordinal);

    /// <summary>Gets or creates the trace lane for an actor.</summary>
    private Lane GetLane(string actor)
    {
        if (!_lanes.TryGetValue(actor, out Lane? lane)) _lanes[actor] = lane = new Lane();
        return lane;
    }
}
