using OpenMono.Session;

namespace OpenMono.Tools;

public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(ToolCall call, ITool? tool, ToolContext ctx, CancellationToken ct);







    bool PausesAfterEmit => false;
}
