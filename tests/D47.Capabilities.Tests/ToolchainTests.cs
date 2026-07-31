using Shouldly;

using Xunit;

namespace D47.Capabilities.Tests;

/// <summary>
/// Proves the test toolchain itself works before anything depends on it:
/// xUnit v3 discovers and runs tests, Shouldly is wired up and actually fails
/// when an assertion does not hold, and the underscored naming convention
/// survives CA1707 under TreatWarningsAsErrors.
///
/// Delete this file once D47.Capabilities has real tests. It asserts nothing
/// about the product.
/// </summary>
public class ToolchainTests
{
    [Fact]
    public void Shouldly_ReportsAFailure_WhenAnAssertionDoesNotHold()
    {
        Should.Throw<ShouldAssertException>(() => 1.ShouldBe(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(47)]
    public void Theories_ReceiveTheirInlineData(int value)
    {
        value.ShouldBeGreaterThanOrEqualTo(0);
    }
}
