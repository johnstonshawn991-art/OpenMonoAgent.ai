namespace OpenMono.Utils;

public static class AcpOnlyArg
{
    public static (bool acpOnly, bool consumedNext) Parse(string? next)
    {
        if (next is "true" or "false") return (next == "true", true);
        return (true, false);
    }
}
