using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using OpenMono.Session;

namespace OpenMono.Acp;






public sealed class AcpSession
{
    public required string Id { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime LastActivityAt { get; set; }
    public required string Model { get; init; }
    public int TurnCount { get; set; }
    public bool PlanMode { get; set; }
    public List<TodoItem> Todos { get; init; } = new();
    public List<Message> Messages { get; init; } = new();


    [JsonIgnore]
    public SemaphoreSlim TurnLock { get; } = new(1, 1);









    [JsonIgnore]
    private readonly ConcurrentDictionary<string, PendingPause> _pending = new();

    [JsonIgnore]
    private readonly ConcurrentDictionary<string, bool> _rememberedPermissions = new();

    [JsonIgnore]
    private readonly ConcurrentDictionary<string, string> _rememberedUserInputs = new();

    public TaskCompletionSource<AcpPauseResponse> RegisterPause(
        string id, PendingResponseKind kind, string contextKey)
    {
        var tcs = new TaskCompletionSource<AcpPauseResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, new PendingPause(kind, contextKey, tcs)))
            throw new InvalidOperationException($"Duplicate pause id: {id}");
        return tcs;
    }

    public bool TryResolvePause(string id, AcpPauseResponse response)
        => _pending.TryRemove(id, out var pp) && pp.Tcs.TrySetResult(response);

    public (PendingResponseKind Kind, string ContextKey)? LookupPauseContext(string id)
        => _pending.TryGetValue(id, out var pp) ? (pp.Kind, pp.ContextKey) : null;

    [JsonIgnore]
    public IReadOnlyCollection<string> PendingIds => _pending.Keys.ToArray();

    public void CancelAllPending()
    {
        foreach (var kv in _pending) kv.Value.Tcs.TrySetCanceled();
        _pending.Clear();
    }







    public void RememberPermission(string contextKey, bool allow)
        => _rememberedPermissions[contextKey] = allow;

    public bool? TryGetRememberedPermission(string contextKey)
        => _rememberedPermissions.TryGetValue(contextKey, out var v) ? v : null;

    public void RememberUserInput(string contextKey, string value)
        => _rememberedUserInputs[contextKey] = value;

    public string? TryGetRememberedUserInput(string contextKey)
        => _rememberedUserInputs.TryGetValue(contextKey, out var v) ? v : null;

    private sealed record PendingPause(
        PendingResponseKind Kind,
        string ContextKey,
        TaskCompletionSource<AcpPauseResponse> Tcs);
}






public abstract record AcpPauseResponse;

public sealed record AcpPermissionResponse(bool Allow) : AcpPauseResponse;

public sealed record AcpUserInputResponse(string Value) : AcpPauseResponse;

public sealed record AcpCancelledResponse() : AcpPauseResponse;
