using System;
using System.Windows;

using D47.Render;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.GameOverlay.Tests;

/// <summary>
/// The overlay over the real game. Everything here is Tier 3: it needs Elite
/// running in borderless windowed mode, and there is no stand-in for the game
/// that would make the claim mean anything.
/// </summary>
public class GameOverlayTests : GameTest
{
    private readonly ITestOutputHelper _output;

    public GameOverlayTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GameOverlay_WhenShown_DrawsTheSharedRenderOverTheGame()
    {
        using var overlay = RunningOverlay.ShownOver(Game, Fixtures.HelpsAnswer());

        // Read once, while the overlay is up. Asking again from inside a
        // failure message would describe the stack at the moment of the
        // assertion, which is a different desktop from the one being asserted
        // about if anything moved in between.
        string stack = Screen.DescribeStack();
        _output.WriteLine(stack);

        // The shared render. Help's top level, read off the visual tree the
        // overlay built — the same text the panel's own tests read off the
        // same control. One group, because help is currently the only
        // registered capability.
        overlay.VisibleText().ShouldBe(["Getting around"]);

        // Over the game, in both senses. Inside its rectangle, so it is drawn
        // on the game rather than beside it; and in front of it in the window
        // stack, which is Windows' opinion rather than the overlay's own.
        Game.Bounds.Contains(overlay.Bounds).ShouldBeTrue(
            $"The overlay is at {overlay.Bounds} and the game is at {Game.Bounds}.");

        Screen.DepthOf(overlay.Handle).ShouldBeLessThan(
            Screen.DepthOf(Game.Handle),
            $"The overlay should be in front of the game.{stack}");
    }

    [Fact]
    public void GameOverlay_WhileEliteHasFocus_PassesAllPointerInputThroughToTheGame()
    {
        // The effect half of the criterion. Whether the application decides
        // correctly when Elite comes forward is logic, asserted in CI against a
        // stand-in; whether the decision does anything is this, and it needs a
        // real window over a real game.
        //
        // Deliberately not driven by giving Elite the foreground. That would
        // mean the test performing a focus steal to prove the overlay does not
        // perform one, and it is the same manoeuvre that made an earlier
        // foreground test vacuous.
        using var overlay = RunningOverlay.ShownOver(Game, Fixtures.HelpsAnswer());

        Rect box = overlay.Bounds;
        int x = (int)(box.X + (box.Width / 2));
        int y = (int)(box.Y + (box.Height / 2));

        Screen.OwnerOf(x, y).ShouldBe(
            overlay.Handle,
            $"before passing input through, a click at ({x},{y}) belongs to the overlay");

        overlay.PassInputThrough(true);

        Screen.OwnerOf(x, y).ShouldNotBe(
            overlay.Handle,
            $"passing input through, the same pixel should belong to whatever is behind it{Screen.DescribeStack()}");

        overlay.PassInputThrough(false);

        Screen.OwnerOf(x, y).ShouldBe(
            overlay.Handle,
            "and it should switch back, on the same window, without being recreated");
    }

    [Fact]
    public void GameOverlay_WhenEliteLosesFocus_ShowsItsChromeForMovingAndResizing()
    {
        // The window half. That the application asks for chrome at the right
        // moment is logic and lives in CI; that asking produces furniture on a
        // real transparent, topmost window is this.
        using var overlay = RunningOverlay.ShownOver(Game, Fixtures.HelpsAnswer());

        IntPtr before = overlay.Handle;

        overlay.ChromeOnScreen().ShouldBeEmpty(
            "the overlay starts with the game in front, so nothing should be covering it");

        overlay.ShowChrome(true);

        overlay.ChromeOnScreen().ShouldBe(
            ["DragBar", "Grip"],
            ignoreOrder: true,
            customMessage: "with the game away there should be a bar to drag and a grip to pull");

        overlay.ShowChrome(false);

        overlay.ChromeOnScreen().ShouldBeEmpty("and it should go away again");

        overlay.Handle.ShouldBe(
            before,
            "showing and hiding chrome must not recreate the window");
    }

    [Fact]
    public void GameOverlay_AtAnySize_IsTheSizeAndShapeOfTheRender()
    {
        // Written after the maintainer found both symptoms of not doing this.
        // The window took its size from a render that had been measured but
        // never laid out, so it came out too narrow for its own title bar — and
        // the shape it took from that was wrong, so the render letterboxed
        // inside its own window and the chrome, which hangs off the window's
        // edges, was stranded away from anything visible.
        //
        // The window keeping the render's shape is what makes both impossible.
        Answer answer = Fixtures.HelpsAnswer();
        using var overlay = RunningOverlay.ShownOver(Game, answer);

        // The size first, worked out independently. The shape assertions below
        // pass whether or not this holds — the render is pinned to whatever the
        // overlay decided, so a window and a render that are both wrong agree
        // with each other perfectly. Two symptoms, two guards.
        Size wanted = overlay.WhatTheRenderWants(answer);
        Size used = overlay.NaturalSize();

        used.Width.ShouldBe(
            wanted.Width,
            tolerance: 0.5,
            customMessage: "the overlay should lay its render out at the size the render wants");

        used.Height.ShouldBe(wanted.Height, tolerance: 0.5);

        (double window, double render) = overlay.Shapes();

        window.ShouldBe(
            render,
            tolerance: 0.01,
            customMessage: "the window should open at the render's own shape");

        overlay.ResizeTo(600);

        (double wider, double stillTheRender) = overlay.Shapes();

        wider.ShouldBe(
            stillTheRender,
            tolerance: 0.01,
            customMessage: "and keep it after being resized, or the render floats inside its own window");
    }

    [Fact]
    public void GameOverlay_WhenResized_ScalesItsContentsRatherThanReflowingThem()
    {
        // The sibling of GameOverlay_AtAnySize_IsTheSizeAndShapeOfTheRender, and
        // not the same claim. That one guards the window's shape — that the
        // render is not letterboxed inside a box of the wrong proportions. This
        // one guards what happens to the contents when the grip is pulled, which
        // a window of the right shape can still get wrong by laying them out
        // again at the new size.
        //
        // The Viewbox is what makes the difference, so removing it is what this
        // test has to go red against — and it does.
        //
        // One number, deliberately. The obvious second assertion is that the
        // contents did not get laid out again, and there is no way to write it
        // that can fail: a Viewbox measures its child against infinity at every
        // window size, so nothing under one ever reflows, and an assertion that
        // it did not is true of every build including the broken ones. It was
        // written twice, run against both breakages, and dropped both times.
        // What is left says the same thing from the only side that moves.
        Answer answer = Fixtures.HelpsAnswer();
        using var overlay = RunningOverlay.ShownOver(Game, answer);

        Size drawn = overlay.LineOnTheWindow();

        double from = overlay.NaturalSize().Width;
        const double Factor = 2.0;

        overlay.ResizeTo(from * Factor);

        Size drawnBigger = overlay.LineOnTheWindow();

        // Twice the window is twice the line. Scaled, because a line that had
        // been laid out again at the new size would still be the width of its
        // own text — bigger box, same words, which is the failure this names.
        drawnBigger.Width.ShouldBe(
            drawn.Width * Factor,
            tolerance: 1.0,
            customMessage:
                $"the window doubled, so the line should have gone from {drawn.Width} to "
                + $"{drawn.Width * Factor} on screen, and it went to {drawnBigger.Width}");

        // And uniformly, in both directions, or it is stretched rather than
        // scaled and the contents no longer keep their proportions.
        drawnBigger.Height.ShouldBe(
            drawn.Height * Factor,
            tolerance: 1.0,
            customMessage:
                $"the line went from {drawn.Height} to {drawnBigger.Height} tall, "
                + $"where doubling uniformly would have made it {drawn.Height * Factor}");
    }

    // There is deliberately no test here for the overlay leaving the foreground
    // alone. One was written and deleted: it passed with ShowActivated="False"
    // and passed just as happily with it set to "True", which makes it a test
    // that reports confidence it never earned.
    //
    // The cause is the harness rather than the assertion. Windows will not let
    // a process that does not already hold the foreground take it, and the test
    // runner holds nothing — so the overlay could not have stolen focus here
    // even if it were built to. Proving this needs the composed application
    // launched as a process, which does hold the foreground when it shows the
    // panel, and that is a bigger change to how this project's tests are put
    // together than a story should take on its own. See the pull request.

}
