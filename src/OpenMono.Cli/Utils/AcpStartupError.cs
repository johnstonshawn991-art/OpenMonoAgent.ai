namespace OpenMono.Utils;

public static class AcpStartupError
{
    public static string Message(int port, Exception? error)
    {
        var cause = error?.Message ?? "no underlying error was captured";
        return
            $"--acp-only requires the ACP server, but it failed to start on port {port}: {cause}. " +
            $"The port is most likely already in use (another agent or a leftover container) — " +
            $"stop it or pass a different --acp-port.";
    }
}
