using System;
using System.Collections.Generic;
using System.Windows;

using D47.Data;
using D47.GameOverlay;
using D47.Panel;
using D47.Render;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// What the application does when the machine cannot host an overlay, what it
/// does when the overlay is broken, which way a toggle goes, and how see-through
/// the overlay starts. Those are four different things and this is the only
/// place any of them is asserted.
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
        using var remembered = new TemporaryStore();
        var machineThatCannot = new FactoryThatRefuses();

        Overlay overlay = Should.NotThrow(
            () => Overlay.From(
                machineThatCannot,
                    Presentation.Of(Fixtures.HelpsAnswer()),
                    remembered.Open(),
                    remembered.Settings(),
                    _recorded.Add));

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
        using var remembered = new TemporaryStore();

        Overlay.From(
            new FactoryThatRefuses(),
                Presentation.Of(Fixtures.HelpsAnswer()),
                remembered.Open(),
                remembered.Settings(),
                _recorded.Add);

        _recorded.ShouldHaveSingleItem().ShouldContain(
            "cannot host an overlay",
            customMessage: "an absence nobody wrote down is indistinguishable from a defect");
    }

    [Fact]
    public void GameOverlay_WhenTheMachineCanHostOne_SaysNothingAtAll()
    {
        // The other half, and the one that keeps the log worth reading. A line
        // written on every ordinary startup is a line nobody looks at twice.
        using var remembered = new TemporaryStore();

        Overlay.From(
            new FactoryReturning(new OverlayThatRemembers()),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        _recorded.ShouldBeEmpty("nothing was carried on without");
    }

    [Fact]
    public void GameOverlay_WhenCreationFailsForAnyOtherReason_SaysSoRatherThanGoingQuiet()
    {
        // The criterion that matters more than the one above it. "Absent, not
        // failed" is one try/catch away from reporting our own defects as an
        // unsupported machine, and this is what stops that being written.
        using var remembered = new TemporaryStore();
        var broken = new FactoryThatIsBroken();

        Should.Throw<StandInFailure>(
            () => Overlay.From(broken,
                Presentation.Of(Fixtures.HelpsAnswer()),
                remembered.Open(),
                remembered.Settings(),
                _recorded.Add));
    }

    [Fact]
    public void GameOverlay_WhenToldToShowOrHide_TogglesByHotkey()
    {
        // The logic half of the criterion. That a keystroke arrives at all is
        // the hotkey's own business and is asserted where the hotkey is.
        using var remembered = new TemporaryStore();
        var overlay = new OverlayThatRemembers();
        var toggling = Overlay.From(
            new FactoryReturning(overlay),
                Presentation.Of(Fixtures.HelpsAnswer()),
                remembered.Open(),
                remembered.Settings(),
                _recorded.Add);

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
        using var remembered = new TemporaryStore();
        var overlay = new OverlayThatRemembers();
        var toggling = Overlay.From(
            new FactoryReturning(overlay),
                Presentation.Of(Fixtures.HelpsAnswer()),
                remembered.Open(),
                remembered.Settings(),
                _recorded.Add);

        toggling.Show();
        overlay.IsVisible.ShouldBeTrue();

        toggling.Toggle();

        overlay.IsVisible.ShouldBeFalse("the first press after startup should hide it");
    }

    [Fact]
    public void GameOverlay_WhileEliteHasFocus_PassesAllPointerInputThroughToTheGame()
    {
        using var remembered = new TemporaryStore();
        var overlay = new OverlayThatRemembers();
        var following = Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add,
            IsTheGame);

        following.ForegroundIsNow(TheGame);

        overlay.PassesInputThrough.ShouldBeTrue(
            "a click over the overlay has to reach the cockpit underneath it");
    }

    [Fact]
    public void GameOverlay_WhenSomethingElseHasFocus_TakesTheMouseBack()
    {
        // The half that makes the chrome possible at all. An overlay that is
        // always click-through can never be grabbed and moved.
        using var remembered = new TemporaryStore();
        var overlay = new OverlayThatRemembers();
        var following = Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add,
            IsTheGame);

        following.ForegroundIsNow(TheGame);
        following.ForegroundIsNow(SomethingElse);

        overlay.PassesInputThrough.ShouldBeFalse(
            "with the game not in front there is nothing to pass input through to");
    }

    [Fact]
    public void GameOverlay_WhenEliteLosesFocus_ShowsItsChromeForMovingAndResizing()
    {
        using var remembered = new TemporaryStore();
        var overlay = new OverlayThatRemembers();
        var following = Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add,
            IsTheGame);

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
        using var remembered = new TemporaryStore();
        var machineThatCannot = new FactoryThatRefuses();
        var following = Overlay.From(
            machineThatCannot,
                Presentation.Of(Fixtures.HelpsAnswer()),
                remembered.Open(),
                remembered.Settings(),
                _recorded.Add, IsTheGame);

        Should.NotThrow(() => following.ForegroundIsNow(TheGame));
    }

    [Fact]
    public void GameOverlay_WhenNothingWasEverRemembered_StartsSeeThroughRatherThanSolid()
    {
        // A solid dark box over the cockpit is what this story is about, so the
        // setting has to ship doing something visible. 0.75 is where it starts,
        // not a finding about where it belongs — see
        // [#95](https://github.com/retiring-studios/directive-47/issues/95).
        using var remembered = new TemporaryStore();
        var overlay = new OverlayThatRemembers();

        Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        overlay.Opacity.ShouldBe(0.75, "what is under the overlay has to stay readable");
    }

    [Fact]
    public void GameOverlay_OnRestart_IsAsSeeThroughAsItWasLeft()
    {
        using var remembered = new TemporaryStore();

        remembered.Settings().Write("game overlay opacity", "0.4");

        var overlay = new OverlayThatRemembers();

        // A second open is the next run. The setting is only worth having if it
        // survives one.
        Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        overlay.Opacity.ShouldBe(0.4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("half")]
    [InlineData("0,4")]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public void GameOverlay_WhenWhatWasRememberedIsNotAnOpacity_StartsAtTheDefault(
        string nonsense)
    {
        // The file is indented JSON in a folder the Commander can open, so
        // somebody editing it by hand is the expected way this value changes
        // until there is a settings page. Anything but a number from 0 to 1 is
        // not an opacity — including one written in a language whose decimal
        // separator is a comma, because the file has to mean the same thing on
        // every machine.
        using var remembered = new TemporaryStore();

        remembered.Settings().Write("game overlay opacity", nonsense);

        var overlay = new OverlayThatRemembers();

        Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        overlay.Opacity.ShouldBe(0.75, "an unusable setting is no answer, not a failure");

        _recorded.ShouldHaveSingleItem().ShouldContain(
            "game overlay opacity",
            customMessage: "a setting nobody could use is exactly the kind of absence to record");
    }

    [Fact]
    public void GameOverlay_WhenWhatWasRememberedIsNotAnOpacity_QuotesWhatItFound()
    {
        // The fix is a line in a file, and nobody can correct a line they cannot
        // identify.
        using var remembered = new TemporaryStore();

        remembered.Settings().Write("game overlay opacity", "half");

        Overlay.From(
            new FactoryReturning(new OverlayThatRemembers()),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        _recorded.ShouldHaveSingleItem().ShouldContain("\"half\"");
    }

    [Fact]
    public void GameOverlay_WhenTheMachineCannotHostOne_SaysNothingAboutTheSetting()
    {
        // The absent case reaches this path too, and it has nothing to apply an
        // opacity to. One absence is worth reporting — there is no overlay — and
        // a second line about a setting that would have gone onto a surface
        // nobody has is noise on top of it.
        using var remembered = new TemporaryStore();

        remembered.Settings().Write("game overlay opacity", "half");

        Should.NotThrow(
            () => Overlay.From(
                new FactoryThatRefuses(),
                Presentation.Of(Fixtures.HelpsAnswer()),
                remembered.Open(),
                remembered.Settings(),
                _recorded.Add));

        _recorded.ShouldHaveSingleItem().ShouldContain("cannot host an overlay");
    }

    [Fact]
    public void GameOverlay_WhenRepositioned_RemembersItsPlaceAcrossRestart()
    {
        // The story's whole criterion, both halves of it in one test because
        // they are one gesture: an overlay dragged somewhere and pulled to a
        // size is left like that, and an overlay that forgets either half is
        // one more thing to set up before every session.
        using var remembered = new TemporaryStore();
        var moved = new OverlayThatRemembers();

        Overlay.From(
            new FactoryReturning(moved),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add,
            whereTheScreensAre: Screens);

        moved.DraggedTo(new Point(640, 360), 1.5);

        // A second open is the next run. Written down as the Commander let go,
        // not on the way out — there is no way out to hook, and a value that
        // only survives a clean exit is one that does not survive a crash.
        var restarted = new OverlayThatRemembers();

        Overlay.From(
            new FactoryReturning(restarted),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add,
            whereTheScreensAre: Screens);

        restarted.Position.ShouldBe(new Point(640, 360), "where it was left");
        restarted.Scale.ShouldBe(1.5, "and the size it was left at");

        _recorded.ShouldBeEmpty("nothing was carried on without");
    }

    [Fact]
    public void GameOverlay_WhenNothingWasEverRemembered_IsLeftToStartOverTheGame()
    {
        // The fallback, and the reason it has to be one. Placing the overlay
        // over the game is the right answer exactly once — on a machine that
        // has never been told anywhere else — and it was the unconditional
        // answer until this story.
        using var remembered = new TemporaryStore();
        var overlay = new OverlayThatRemembers();

        Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        overlay.WasPlaced.ShouldBeFalse(
            "with nothing remembered, the overlay's own answer — the game's corner — is the one "
            + "to leave alone");

        _recorded.ShouldBeEmpty("a first run is not an event");
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("left a bit", "360", "1.5")]
    [InlineData("640", "down a bit", "1.5")]
    [InlineData("640", "360", "half")]
    [InlineData("640", "360", "0")]
    [InlineData("640", "360", "-1.5")]
    [InlineData("6,40", "360", "1.5")]
    [InlineData("640", "", "1.5")]
    public void GameOverlay_WhenWhatWasRememberedIsNotAPlace_StartsOverTheGame(
        string left, string top, string scale)
    {
        // Same trade as the opacity beside it. remembered.json is a file
        // somebody opens and edits, so anything at all can be in it, and
        // refusing to start over a hand-typed number would be a worse answer
        // than putting the overlay back where a first run would have put it.
        //
        // A scale of zero or less is in here because it parses perfectly and is
        // not a size. So is a comma for a decimal point, because the file has
        // to mean the same thing on every machine that opens it.
        using var remembered = new TemporaryStore();
        DataStore store = remembered.Open();

        store.Write("game overlay left", left);
        store.Write("game overlay top", top);
        store.Write("game overlay scale", scale);

        var overlay = new OverlayThatRemembers();

        Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        overlay.WasPlaced.ShouldBeFalse("an unusable place is no answer, not a failure");

        _recorded.ShouldHaveSingleItem().ShouldContain(
            "game overlay left",
            customMessage: "the fix is a line in a file, and nobody can correct one they cannot "
            + "identify");
    }

    [Fact]
    public void GameOverlay_WhenOnlySomeOfThePlaceWasRemembered_StartsOverTheGame()
    {
        // Two thirds of a place is not a place. It is also not a first run, so
        // it says so — a file somebody has half-edited is exactly the case
        // where silence looks like the setting being ignored.
        using var remembered = new TemporaryStore();

        remembered.Open().Write("game overlay left", "640");

        var overlay = new OverlayThatRemembers();

        Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        overlay.WasPlaced.ShouldBeFalse();
        _recorded.ShouldHaveSingleItem().ShouldContain("game overlay top");
    }

    [Fact]
    public void GameOverlay_WhenWhereItWasLeftIsNoLongerOnAnyScreen_StartsOverTheGame()
    {
        // The monitor that is not there any more. A position remembered on a
        // second screen is a perfectly good pair of numbers and nowhere at all
        // once that screen is unplugged, and an overlay restored onto it is one
        // the Commander can neither see nor grab — so the failure is invisible
        // and looks like the overlay never came back.
        using var remembered = new TemporaryStore();
        DataStore store = remembered.Open();

        store.Write("game overlay left", "5000");
        store.Write("game overlay top", "300");
        store.Write("game overlay scale", "1");

        var overlay = new OverlayThatRemembers();

        Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add,
            whereTheScreensAre: Screens);

        overlay.WasPlaced.ShouldBeFalse("a place nobody can see is not a place to restore to");

        _recorded.ShouldHaveSingleItem().ShouldContain(
            "5000",
            customMessage: "an overlay that silently did not come back where it was left is "
            + "indistinguishable from one that did not come back");
    }

    private static readonly IntPtr TheGame = new(1);
    private static readonly IntPtr SomethingElse = new(2);

    private static bool IsTheGame(IntPtr window) => window == TheGame;

    /// <summary>
    /// The screens this machine is pretending to have. Fixed, so that a test
    /// about a position being off the edge of the desktop says the same thing
    /// on a laptop, on the dev PC's two monitors, and on a hosted runner.
    /// </summary>
    private static Rect Screens() => new(0, 0, 3840, 2160);

    /// <summary>
    /// A machine that cannot composite, without breaking one.
    /// </summary>
    private sealed class FactoryThatRefuses : IGameOverlayFactory
    {
        internal bool WasAsked { get; private set; }

        public IGameOverlay? Create(Presentation presented)
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
        public IGameOverlay? Create(Presentation presented) => throw new StandInFailure();
    }

    private sealed class FactoryReturning : IGameOverlayFactory
    {
        private readonly IGameOverlay _overlay;

        internal FactoryReturning(IGameOverlay overlay)
        {
            _overlay = overlay;
        }

        public IGameOverlay? Create(Presentation presented) => _overlay;
    }

    /// <summary>
    /// An overlay that is nothing but whether it is on screen and where it is —
    /// which is the entire reason <see cref="IGameOverlay"/> says nothing about
    /// windows or about Elite.
    /// </summary>
    private sealed class OverlayThatRemembers : IGameOverlay
    {
        public bool IsVisible { get; private set; }

        public bool PassesInputThrough { get; set; }

        public bool ShowsChrome { get; set; }

        public double Opacity { get; set; }

        public Point Position { get; private set; }

        public double Scale { get; private set; } = 1;

        public event EventHandler? MovedOrResized;

        /// <summary>
        /// Whether anything ever told it where to go.
        ///
        /// <para>
        /// The distinction the real window turns into "over the game or where
        /// it was left", and the reason a position of nowhere in particular is
        /// not enough to assert on: <c>(0, 0)</c> is a place somebody could
        /// genuinely have left it.
        /// </para>
        /// </summary>
        internal bool WasPlaced { get; private set; }

        public void PlaceAt(Point corner, double scale)
        {
            Position = corner;
            Scale = scale;
            WasPlaced = true;
        }

        /// <summary>
        /// The Commander finishing a drag or a pull on the grip.
        /// </summary>
        /// <param name="corner">Where they left it.</param>
        /// <param name="scale">How big they left it.</param>
        internal void DraggedTo(Point corner, double scale)
        {
            Position = corner;
            Scale = scale;

            MovedOrResized?.Invoke(this, EventArgs.Empty);
        }

        public void Show() => IsVisible = true;

        public void Hide() => IsVisible = false;
    }
    [Fact]
    public void GameOverlay_WhenTheSeeThroughAmountChanges_TakesItAtOnce()
    {
        using var remembered = new TemporaryStore();
        var overlay = new OverlayThatRemembers();

        var showing = Overlay.From(
            new FactoryReturning(overlay),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        showing.SeeThrough(0.25);

        // At once, not at the next launch. A settings page whose effect a
        // Commander cannot see is one they change twice and then stop trusting.
        overlay.Opacity.ShouldBe(0.25);
    }

    [Fact]
    public void GameOverlay_WhenThereIsNoOverlay_ShrugsAtASeeThroughChange()
    {
        using var remembered = new TemporaryStore();

        var none = Overlay.From(
            new FactoryThatRefuses(),
            Presentation.Of(Fixtures.HelpsAnswer()),
            remembered.Open(),
            remembered.Settings(),
            _recorded.Add);

        // Absent, not failed, the same as everything else here. A machine with
        // no overlay still has a settings page.
        Should.NotThrow(() => none.SeeThrough(0.25));
    }
}
