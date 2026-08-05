using System;

using Shouldly;

using Xunit;

namespace D47.Tier3.Tests.GameOverlay;

/// <summary>
/// Directive 47 as the Commander actually runs it: one process, composing an
/// answer and putting it on two surfaces at once.
///
/// <para>
/// Everything else in this repo tests a part. <c>App</c> builds an answer, asks
/// for an overlay, shows it, follows the foreground and claims a hotkey — each
/// of those is asserted somewhere, and the wiring between them by nobody, so
/// rewiring it could break completely and the suite would stay green. That gap
/// has widened twice already, once when
/// [#104](https://github.com/retiring-studios/directive-47/issues/104) changed
/// three signatures in the composed path and once when
/// [#148](https://github.com/retiring-studios/directive-47/issues/148) changed
/// one of them again.
/// </para>
///
/// <para>
/// Tier 3, and for two reasons rather than one. It needs the game, like
/// everything else here; and it needs a real window in the foreground for an
/// overlay to be capable of taking, which a test runner never provides and only
/// a real process showing a real panel does.
/// </para>
/// </summary>
public class EndToEndTests : GameTest
{
    private readonly ITestOutputHelper _output;

    public EndToEndTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EndToEnd_WithTheGameRunning_DrivesTheComposedApplication()
    {
        using var app = RunningApplication.Launch();

        // Read once, while both surfaces are up. Asking again from inside a
        // failure message would describe a desktop that has moved on.
        string stack = Screen.DescribeStack();
        _output.WriteLine(stack);

        // One answer on two surfaces. Both sides are read the same way, through
        // automation, so this is what a screen reader would be told rather than
        // what either visual tree was built from — and it is the same text the
        // panel's own tests and the overlay's own tests each assert separately
        // against their own halves.
        //
        // Help's top level, because help is the only registered capability.
        RunningApplication.TextIn(app.Panel).ShouldContain(
            "Getting around",
            customMessage: $"the panel should present the composed answer.{stack}");

        RunningApplication.TextIn(app.Overlay).ShouldContain(
            "Getting around",
            customMessage:
                $"the overlay should present the same answer, from the same composition.{stack}");

        // Over the game, in Windows' opinion rather than the overlay's own.
        // Deliberately not "inside the game's rectangle": the overlay now opens
        // where the Commander last left it, so its position on this machine is
        // whatever they chose, and a test asserting otherwise would fail for a
        // reason that is not a defect.
        Screen.DepthOf(app.Overlay).ShouldBeLessThan(
            Screen.DepthOf(Game.Handle),
            $"the overlay should be in front of the game.{stack}");

        // And the claim only this test can make. The application takes the
        // foreground when it shows the panel, and then shows an overlay — so
        // there is something to steal here, which is exactly what a test runner
        // could never provide. An earlier attempt at this from inside the runner
        // passed just as happily against an overlay built to steal focus, and
        // was deleted.
        //
        // Stated as "the overlay does not have it" rather than "the panel does".
        // Those are the same claim only on a machine nobody is using. The panel
        // takes the foreground when it opens and nothing keeps it there: any
        // window that raises itself in the second this takes holds it instead,
        // and on a development machine that is the ordinary case rather than the
        // exception.
        //
        // The stronger form was tried and reported the maintainer's own editor
        // as an overlay stealing focus, on an idle machine with the game up and
        // nothing else running — while the window stack in the same message
        // showed the overlay sitting quietly four places behind it. A test that
        // calls somebody clicking a defect is a test that gets ignored, and a
        // defect this one would catch then goes with it.
        //
        // Compared as text rather than as handles. Run against an overlay built
        // to steal focus, the handle form reported "should not be <blank>" —
        // Shouldly renders an IntPtr as nothing — so the one line somebody reads
        // first said nothing at all. Naming them is what turns that into a
        // diagnosis.
        Describe(Screen.Foreground()).ShouldNotBe(
            Describe(app.Overlay),
            $"showing the overlay must not take the foreground — the panel is "
            + $"{Describe(app.Panel)}.{stack}");
    }

    /// <summary>
    /// A window, in the terms a failure message needs: which one it is, and its
    /// handle for telling two windows of the same name apart.
    /// </summary>
    /// <param name="window">The window to name.</param>
    /// <returns>Its title and handle.</returns>
    private static string Describe(IntPtr window) =>
        window == IntPtr.Zero
            ? "nothing at all"
            : $"\"{Screen.TitleOf(window)}\" ({window})";
}
