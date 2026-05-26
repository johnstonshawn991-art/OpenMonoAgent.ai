namespace OpenMono.Acp;













public sealed class AcpUserInteractionForwarder : IAcpUserInteraction
{
    private readonly AcpSession _session;
    private readonly SseWriter _writer;
    private readonly TimeSpan _timeout;

    public AcpUserInteractionForwarder(AcpSession session, SseWriter writer, TimeSpan timeout)
    {
        _session = session;
        _writer = writer;
        _timeout = timeout;
    }

    public async Task<bool> RequestPermissionAsync(string toolName, string summary, bool dangerous, CancellationToken ct)
    {
        var contextKey = PermissionContextKey(toolName, summary);




        if (_session.TryGetRememberedPermission(contextKey) is bool cached)
            return cached;

        var id = "perm_" + Guid.NewGuid().ToString("N")[..12];
        _session.RegisterPause(id, PendingResponseKind.Permission, contextKey);

        await _writer.WriteEventAsync("permission_request", new
        {
            id,
            tool = toolName,
            summary,
            dangerous,
        });

        throw new PendingUserResponseException(id, PendingResponseKind.Permission);
    }

    public async Task<string?> RequestUserInputAsync(string question, CancellationToken ct)
    {



        if (_session.TryGetRememberedUserInput(question) is { } cached)
            return cached;

        var id = "ask_" + Guid.NewGuid().ToString("N")[..12];
        _session.RegisterPause(id, PendingResponseKind.UserInput, question);

        await _writer.WriteEventAsync("user_input_request", new
        {
            id,
            question,
        });

        throw new PendingUserResponseException(id, PendingResponseKind.UserInput);
    }


    public static string PermissionContextKey(string toolName, string summary)
        => $"{toolName}|{summary}";
}
