using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenMono.Acp;












public sealed class AcpLockFileWriter
{
    private readonly string _path;
    private readonly LockPayload _payload;
    private bool _written;

    public AcpLockFileWriter(AcpServerSettings settings)
        : this(settings, workspaceMount: "/workspace")
    {
    }





    public AcpLockFileWriter(AcpServerSettings settings, string workspaceMount)
    {
        ContainerWorkspace = workspaceMount;
        var dir = Path.Combine(workspaceMount, ".openmono");
        _path = Path.Combine(dir, "agent.lock");

        var hostPort = ParseIntOrDefault(Environment.GetEnvironmentVariable("HOST_ACP_PORT"), settings.Port);
        var hostWorkspace = Environment.GetEnvironmentVariable("HOST_WORKSPACE_PATH")
            ?? throw new InvalidOperationException(
                "HOST_WORKSPACE_PATH env var is required. The extension's DockerManager " +
                "always sets it; a user-managed docker-compose setup must declare it too.");
        var agentId = Environment.GetEnvironmentVariable("ACP_AGENT_ID") ?? GenerateAgentId();
        var containerId = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown";

        _payload = new LockPayload(
            version: "1.0.0",
            agent_id: agentId,
            host_workspace: hostWorkspace,
            port: hostPort,
            container_id: containerId,
            started_at: DateTime.UtcNow.ToString("o"));
    }

    public string LockFilePath => _path;
    public string AgentId => _payload.agent_id;
    public string HostWorkspace => _payload.host_workspace;
    public int HostPort => _payload.port;
    public string ContainerId => _payload.container_id;







    public string ContainerWorkspace { get; }


    public void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_payload, LockJsonOpts));
        _written = true;
    }





    public void TryRemove()
    {
        if (!_written) return;
        try { File.Delete(_path); } catch {  }
    }

    private static readonly JsonSerializerOptions LockJsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private sealed record LockPayload(
        string version,
        string agent_id,
        string host_workspace,
        int port,
        string container_id,
        string started_at);

    private static string GenerateAgentId()
        => "agt_" + Convert.ToHexString(Guid.NewGuid().ToByteArray())[..12].ToLowerInvariant();

    private static int ParseIntOrDefault(string? s, int @default)
        => int.TryParse(s, out var v) ? v : @default;
}
