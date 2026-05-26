namespace OpenMono.Acp;






public sealed class AcpTurnRunnerFactory
{
    private readonly ConversationLoopFactory _loopFactory;
    private readonly AcpServerSettings _settings;

    public AcpTurnRunnerFactory(ConversationLoopFactory loopFactory, AcpServerSettings settings)
    {
        _loopFactory = loopFactory;
        _settings = settings;
    }

    public AcpTurnRunner Create(AcpSession session, SseWriter writer)
        => new AcpTurnRunner(session, writer, _loopFactory, _settings);
}
