using FluentAssertions;
using OpenMono.Utils;

namespace OpenMono.Tests.Utils;

public class AcpOnlyArgTests
{
    [Fact]
    public void BareFlag_EnablesAcpOnly_WithoutConsumingNext()
    {
        var (acpOnly, consumed) = AcpOnlyArg.Parse(null);
        acpOnly.Should().BeTrue();
        consumed.Should().BeFalse();
    }

    [Fact]
    public void ExplicitFalse_DisablesAcpOnly_AndConsumesTheValue()
    {
        // The whole point of the fix: `--acp-only false` must run interactively,
        // not silently force ACP-only on (the old toggle behaviour).
        var (acpOnly, consumed) = AcpOnlyArg.Parse("false");
        acpOnly.Should().BeFalse();
        consumed.Should().BeTrue();
    }

    [Fact]
    public void ExplicitTrue_EnablesAcpOnly_AndConsumesTheValue()
    {
        var (acpOnly, consumed) = AcpOnlyArg.Parse("true");
        acpOnly.Should().BeTrue();
        consumed.Should().BeTrue();
    }

    [Fact]
    public void FollowingFlag_IsNotConsumed_AndKeepsAcpOnlyOn()
    {
        // A non-boolean token (e.g. the next flag) is left for the parser loop.
        var (acpOnly, consumed) = AcpOnlyArg.Parse("--workdir");
        acpOnly.Should().BeTrue();
        consumed.Should().BeFalse();
    }
}
