using System.Diagnostics.CodeAnalysis;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// Base for every test that drives the real desktop — launching the
/// application, reading the notification area, moving the pointer.
///
/// <para>
/// Two things come from inheriting it. The test takes its turn rather than
/// running alongside another one, because there is only one desktop; and it
/// skips on a development machine, because these are CI's job. Once written,
/// an integration test that seizes the desk for half a minute has no business
/// running every time somebody types <c>dotnet test</c>.
/// </para>
///
/// <para>
/// The line is what the test touches, not how slow it is. Anything that starts
/// the application as a process or reads what the shell is drawing belongs
/// here. The in-process tests that build a visual tree and inspect it do not —
/// they touch nothing outside their own thread, and gating them would cost
/// local coverage for nothing.
/// </para>
/// </summary>
[Collection(Desktop.Collection)]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification =
        "The test classes that inherit this are public, as xUnit requires, and a public "
        + "class cannot derive from an internal one. The rule does not recognise this as a "
        + "test type because it holds no facts of its own.")]
public abstract class DesktopTest
{
    /// <summary>
    /// Skips before the test body runs, and before anything is launched.
    /// </summary>
    protected DesktopTest()
    {
        Assert.SkipUnless(Desktop.IsAvailable, Desktop.NeedsAMachineNobodyIsUsing);
    }
}
