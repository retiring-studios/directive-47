using System;
using System.Collections.Generic;

using D47.GameOverlay;
using D47.Render;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// What the application does when the machine cannot host an overlay, what it
/// does when the overlay is broken, and which way a toggle goes. Those are
/// three different things and this is the only place any of them is asserted.
///
/// <para>
/// Tier 1 and in process. Nothing here needs the game, a desktop, or a window
/// — the whole point of putting a seam in front of the adapter is that the
/// decisions can be tested without a machine that has been broken on purpose,
/// and against an overlay that is nothing but a boolean.
/// </para>
/// </summary>
public class OverlayTests
{
    /// <summary>
    /// Where this test's absences are written down. One per test, because xUnit
    /// builds the class afresh for each fact.
    /// </summary>
    private readonly List<string> _recorded = [];

    [Fact]
    public void GameOverlay_WhenItCannotBeCreated_IsAbsentNotFailed()
    {
        var machineThatCannot = new FactoryThatRefuses();

        Overlay overlay = Should.NotThrow(
            () => Overlay.From(machineThatCannot, Fixtures.HelpsAnswer(), _recorded.Add));

        // Asked, and not merely survived. Without this the test passes on any
        // machine with no game running, for a reason that has nothing to do
        // with what it claims to check.
        machineThatCannot.WasAsked.ShouldBeTrue(
            "the application should have asked for an overlay before deciding to do without one");

        // And it stays absent rather than failing later. Pressing the hotkey on
        // a machine that cannot draw an overlay is not an error to report.
        Should.NotThrow(overlay.Toggle);
        Should.NotThrow(overlay.Show);
    }

    [Fact]
    public void GameOverlay_WhenTheMachineCannotHostOne_LeavesARecord()
    {
        // Silent from the day the overlay was built until #104. Nothing was
        // wrong with the code: the Commander had no overlay and nothing
        // anywhere said so, which is indistinguishable from a defect and is the
        // whole reason that story exists.
        Overlay.From(new FactoryThatRefuses(), Fixtures.HelpsAnswer(), _recorded.Add);

        _recorded.ShouldHaveSingleItem().ShouldContain(
            "cannot host an overlay",
            customMessage: "an absence nobody wrote down is indistinguishable from a defect");
    }

    [Fact]
    public void GameOverlay_WhenTheMachineCanHostOne_SaysNothingAtAll()
    {
        // The other half, and the one that keeps the log worth reading. A line
        // written on every ordinary startup is a line nobody looks at twice.
        Overlay.From(
            new FactoryReturning(new OverlayThatRemembers()), Fixtures.HelpsAnswer(), _recorded.Add);

        _recorded.ShouldBeEmpty("nothing was carried on without");
    }

    [Fact]
    public void GameOverlay_WhenCreationFailsForAnyOtherReason_SaysSoRatherThanGoingQuiet()
    {
        // The criterion that matters more than the one above it. "Absent, not
        // failed" is one try/catch away from reporting our own defects as an
        // unsupported machine, and this is what stops that being written.
        var broken = new FactoryThatIsBroken();

        Should.Throw<StandInFailure>(() => Overlay.From(broken, Fixtures.HelpsAnswer(), _recorded.Add));
    }

    [Fact]
    public void GameOverlay_WhenToldToShowOrHide_TogglesByHotkey()
    {
        // The logic half of the criterion. That a keystroke arrives at all is
        // the hotkey's own business and is asserted where the hotkey is.
        var overlay = new OverlayThatRemembers();
        var toggling = Overlay.From(new FactoryReturning(overlay), Fixtures.HelpsAnswer(), _recorded.Add);

        toggling.Toggle();
        overlay.IsVisible.ShouldBeTrue("the first press should put it on screen");

        toggling.Toggle();
        overlay.IsVisible.ShouldBeFalse("the second press should take it away again");

        toggling.Toggle();
        overlay.IsVisible.ShouldBeTrue("and the third should bring it back");
    }

    [Fact]
    public void GameOverlay_WhenAlreadyShownAtStartup_IsHiddenByTheFirstPress()
    {
        // The case a toggle written as "show it" gets wrong. The overlay is
        // already on screen when the game was running at startup, so the first
        // thing the Commander presses the key for is to make it go away.
        var overlay = new OverlayThatRemembers();
        var toggling = Overlay.From(new FactoryReturning(overlay), Fixtures.HelpsAnswer(), _recorded.Add);

        toggling.Show();
        overlay.IsVisible.ShouldBeTrue();

        toggling.Toggle();

        overlay.IsVisible.ShouldBeFalse("the first press after startup should hide it");
    }

    [Fact]
    public void GameOverlay_WhileEliteHasFocus_PassesAllPointerInputThroughToTheGame()
    {
        var overlay = new OverlayThatRemembers();
        var following = Overlay.From(
            new FactoryReturning(overlay), Fixtures.HelpsAnswer(), _recorded.Add, IsTheGame);

        following.ForegroundIsNow(TheGame);

        overlay.PassesInputThrough.ShouldBeTrue(
            "a click over the overlay has to reach the cockpit underneath it");
    }

    [Fact]
    public void GameOverlay_WhenSomethingElseHasFocus_TakesTheMouseBack()
    {
        // The half that makes the chrome possible at all. An overlay that is
        // always click-through can never be grabbed and moved.
        var overlay = new OverlayThatRemembers();
        var following = Overlay.From(
            new FactoryReturning(overlay), Fixtures.HelpsAnswer(), _recorded.Add, IsTheGame);

        following.ForegroundIsNow(TheGame);
        following.ForegroundIsNow(SomethingElse);

        overlay.PassesInputThrough.ShouldBeFalse(
            "with the game not in front there is nothing to pass input through to");
    }

    [Fact]
    public void GameOverlay_WhenEliteLosesFocus_ShowsItsChromeForMovingAndResizing()
    {
        var overlay = new OverlayThatRemembers();
        var following = Overlay.From(
            new FactoryReturning(overlay), Fixtures.HelpsAnswer(), _recorded.Add, IsTheGame);

        following.ForegroundIsNow(TheGame);

        overlay.ShowsChrome.ShouldBeFalse(
            "with the game in front there is nothing to grab, and every pixel of chrome is a "
            + "pixel of cockpit covered");

        following.ForegroundIsNow(SomethingElse);

        overlay.ShowsChrome.ShouldBeTrue(
            "with the game not in front, the overlay has to be grabbable");
    }

    [Fact]
    public void GameOverlay_WhenTheMachineCannotHostOne_IgnoresTheForegroundEntirely()
    {
        // The absent case reaches this path too: the foreground changes on any
        // machine, whether or not there is an overlay to tell about it.
        var machineThatCannot = new FactoryThatRefuses();
        var following = Overlay.From(
            machineThatCannot, Fixtures.HelpsAnswer(), _recorded.Add, IsTheGame);

        Should.NotThrow(() => following.ForegroundIsNow(TheGame));
    }

    private static readonly IntPtr TheGame = new(1);
    private static readonly IntPtr SomethingElse = new(2);

    private static bool IsTheGame(IntPtr window) => window == TheGame;

    /// <summary>
    /// A machine that cannot composite, without breaking one.
    /// </summary>
    private sealed class FactoryThatRefuses : IGameOverlayFactory
    {
        internal bool WasAsked { get; private set; }

        public IGameOverlay? Create(Answer answer)
        {
            WasAsked = true;
            return null;
        }
    }

    /// <summary>
    /// An overlay with a defect in it, which is a different thing from a
    /// machine that cannot have one.
    /// </summary>
    private sealed class FactoryThatIsBroken : IGameOverlayFactory
    {
        public IGameOverlay? Create(Answer answer) => throw new StandInFailure();
    }

    private sealed class FactoryReturning : IGameOverlayFactory
    {
        private readonly IGameOverlay _overlay;

        internal FactoryReturning(IGameOverlay overlay)
        {
            _overlay = overlay;
        }

        public IGameOverlay? Create(Answer answer) => _overlay;
    }

    /// <summary>
    /// An overlay that is nothing but whether it is on screen — which is the
    /// entire reason <see cref="IGameOverlay"/> says nothing about windows or
    /// about Elite.
    /// </summary>
    private sealed class OverlayThatRemembers : IGameOverlay
    {
        public bool IsVisible { get; private set; }

        public bool PassesInputThrough { get; set; }

        public bool ShowsChrome { get; set; }

        public void Show() => IsVisible = true;

        public void Hide() => IsVisible = false;
    }

}
