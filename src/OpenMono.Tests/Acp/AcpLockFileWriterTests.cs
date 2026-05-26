using System.Text.Json;
using FluentAssertions;
using OpenMono.Acp;
using Xunit;

namespace OpenMono.Tests.Acp;

public sealed class AcpLockFileWriterTests : IDisposable
{
    private readonly string _tempMount;
    private readonly EnvSnapshot _env;
    private readonly AcpServerSettings _settings = new() { Port = 7475 };

    public AcpLockFileWriterTests()
    {
        _tempMount = Path.Combine(Path.GetTempPath(), "openmono-lock-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempMount);
        _env = new EnvSnapshot("HOST_ACP_PORT", "HOST_WORKSPACE_PATH", "ACP_AGENT_ID", "HOSTNAME");
    }

    public void Dispose()
    {
        _env.Dispose();
        try { Directory.Delete(_tempMount, recursive: true); } catch {  }
    }

    [Fact]
    public void Write_emits_lock_file_with_all_required_fields()
    {
        Environment.SetEnvironmentVariable("HOST_WORKSPACE_PATH", "/Users/dev/my-project");
        Environment.SetEnvironmentVariable("HOST_ACP_PORT", "49213");
        Environment.SetEnvironmentVariable("ACP_AGENT_ID", "agt_5f8c4d");
        Environment.SetEnvironmentVariable("HOSTNAME", "openmono_myproject_a1b2");

        var writer = new AcpLockFileWriter(_settings, _tempMount);
        writer.Write();

        var path = Path.Combine(_tempMount, ".openmono", "agent.lock");
        File.Exists(path).Should().BeTrue();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        root.GetProperty("version").GetString().Should().Be("1.0.0");
        root.GetProperty("agent_id").GetString().Should().Be("agt_5f8c4d");
        root.GetProperty("host_workspace").GetString().Should().Be("/Users/dev/my-project");
        root.GetProperty("port").GetInt32().Should().Be(49213);
        root.GetProperty("container_id").GetString().Should().Be("openmono_myproject_a1b2");
        root.GetProperty("started_at").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T");
    }

    [Fact]
    public void Constructor_throws_when_HOST_WORKSPACE_PATH_missing()
    {
        Environment.SetEnvironmentVariable("HOST_WORKSPACE_PATH", null);

        Action act = () => new AcpLockFileWriter(_settings, _tempMount);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*HOST_WORKSPACE_PATH*");
    }

    [Fact]
    public void Constructor_falls_back_to_settings_Port_when_HOST_ACP_PORT_missing()
    {
        Environment.SetEnvironmentVariable("HOST_WORKSPACE_PATH", "/ws");
        Environment.SetEnvironmentVariable("HOST_ACP_PORT", null);

        var writer = new AcpLockFileWriter(_settings, _tempMount);

        writer.HostPort.Should().Be(_settings.Port);
    }

    [Fact]
    public void Constructor_generates_agent_id_when_ACP_AGENT_ID_missing()
    {
        Environment.SetEnvironmentVariable("HOST_WORKSPACE_PATH", "/ws");
        Environment.SetEnvironmentVariable("ACP_AGENT_ID", null);

        var writer = new AcpLockFileWriter(_settings, _tempMount);

        writer.AgentId.Should().StartWith("agt_");
        writer.AgentId.Length.Should().BeGreaterThan(4);
    }

    [Fact]
    public void Write_creates_dot_openmono_directory_if_missing()
    {
        Environment.SetEnvironmentVariable("HOST_WORKSPACE_PATH", "/ws");

        var dotOpenmono = Path.Combine(_tempMount, ".openmono");
        Directory.Exists(dotOpenmono).Should().BeFalse();

        var writer = new AcpLockFileWriter(_settings, _tempMount);
        writer.Write();

        Directory.Exists(dotOpenmono).Should().BeTrue();
    }

    [Fact]
    public void TryRemove_deletes_file_when_Write_was_called()
    {
        Environment.SetEnvironmentVariable("HOST_WORKSPACE_PATH", "/ws");
        var writer = new AcpLockFileWriter(_settings, _tempMount);
        writer.Write();
        var path = Path.Combine(_tempMount, ".openmono", "agent.lock");
        File.Exists(path).Should().BeTrue();

        writer.TryRemove();

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void TryRemove_is_a_noop_when_Write_was_never_called()
    {
        Environment.SetEnvironmentVariable("HOST_WORKSPACE_PATH", "/ws");
        var writer = new AcpLockFileWriter(_settings, _tempMount);

        var path = Path.Combine(_tempMount, ".openmono", "agent.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"stale\": true }");

        writer.TryRemove();

        File.Exists(path).Should().BeTrue("TryRemove must only remove a file the writer itself wrote");
    }



    private sealed class EnvSnapshot : IDisposable
    {
        private readonly Dictionary<string, string?> _saved = new();

        public EnvSnapshot(params string[] names)
        {
            foreach (var n in names)
            {
                _saved[n] = Environment.GetEnvironmentVariable(n);
                Environment.SetEnvironmentVariable(n, null);
            }
        }

        public void Dispose()
        {
            foreach (var (k, v) in _saved)
                Environment.SetEnvironmentVariable(k, v);
        }
    }
}
