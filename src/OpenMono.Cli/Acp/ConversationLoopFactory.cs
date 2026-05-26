using OpenMono.Config;
using OpenMono.Llm;
using OpenMono.Permissions;
using OpenMono.Rendering;
using OpenMono.Session;
using OpenMono.Tools;

namespace OpenMono.Acp;







public sealed class ConversationLoopFactory
{
    private readonly ILlmClient _llm;
    private readonly ToolRegistry _tools;
    private readonly AppConfig _config;
    private readonly IOutputSink _output;
    private readonly IInputReader _input;
    private readonly ILiveFeedback? _liveFeedback;

    public ConversationLoopFactory(
        ILlmClient llm,
        ToolRegistry tools,
        AppConfig config,
        IOutputSink output,
        IInputReader input,
        ILiveFeedback? liveFeedback = null)
    {
        _llm = llm;
        _tools = tools;
        _config = config;
        _output = output;
        _input = input;
        _liveFeedback = liveFeedback;
    }








    public ConversationLoop Create(SessionState session, IAcpEventSink sink, IAcpUserInteraction interaction)
    {
        var placeholderPermissions = new PermissionEngine(_config, _output, _input);
        return new ConversationLoop(
            _llm,
            _tools,
            placeholderPermissions,
            _output,
            _input,
            _liveFeedback,
            _config,
            session,
            sink: sink,
            interaction: interaction);
    }
}
