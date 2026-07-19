using FluentAssertions;
using OpenMono.Utils;

namespace OpenMono.Tests.Utils;

public class AcpStartupErrorTests
{
    [Fact]
    public void Message_includes_the_port_and_the_underlying_cause()
    {
        var msg = AcpStartupError.Message(7475, new Exception("address already in use"));
        msg.Should().Contain("7475");
        msg.Should().Contain("address already in use");
    }

    [Fact]
    public void Message_does_not_falsely_blame_config()
    {
        var msg = AcpStartupError.Message(7475, new Exception("boom"));
        msg.Should().NotContain("Enabled is false");
    }

    [Fact]
    public void Message_handles_a_null_error()
    {
        var msg = AcpStartupError.Message(8080, null);
        msg.Should().Contain("8080");
    }
}
