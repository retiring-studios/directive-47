using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using D47.Panel;
using D47.Placement;
using D47.VrOverlay;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Dragging a corner handle, as a loop rather than as arithmetic.
///
/// <para>
/// Tier 1: a thread and three stand-ins, no runtime. How big the overlay gets
/// given a hand is <c>Stretch</c> in <c>D47.Placement</c> and is Tier 0. What is
/// here is when a scale starts, when it stops, and what changes while it lasts.
/// </para>
///
/// <para>
/// The stand-ins are shared with <c>GrabbingTests</c> and
/// <c>GrabbingTheBarTests</c>, and live in <c>GrabbingDoubles</c>.
/// </para>
/// </summary>
public class GrabbingACornerTests
{
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(5);

    private const uint TheHand = 3;

    /// <summary>
    /// Where the hand starts: off to one side of the overlay, which is where a
    /// Commander's actually is.
    /// </summary>
    private static readonly Vector3 Beside = new(0.3f, -0.4f, -0.6f);

    /// <summary>
    /// The bottom left corner of the overlay the stand-in starts as — half a
    /// metre by three tenths, a metre and a half ahead. It is what stays put
    /// while the top right one is pulled.
    /// </summary>
    private static readonly Vector3 Pinned = new(-0.25f, -0.15f, -1.5f);

    [Fact]
    public void Grab_OnACorner_ScalesTheOverlay()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.TopRightCorner);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            // Out to twice the distance from the corner that is holding still.
            hand.MoveTo(Pinned + ((Beside - Pinned) * 2));

            Eventually.True(
                () => overlay.Placed.Width > 0.6f, LongEnough).ShouldBeTrue(
                $"the overlay should have grown, and it is {overlay.Placed.Width} across");
        }

        // Twice the reach is twice the overlay. The arithmetic is Stretch's and
        // is asserted at Tier 0; what this catches is the loop handing it the
        // wrong hand, the wrong corner, or the wrong starting board.
        overlay.Placed.Width.ShouldBe(1.0f, 0.001f);
        overlay.Placed.Height.ShouldBe(0.6f, 0.001f);
    }

    [Fact]
    public void AScale_WhileItLasts_KeepsTheChromeAroundTheOverlayItIsSizing()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.TopRightCorner);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            hand.MoveTo(Pinned + ((Beside - Pinned) * 2));

            // The chrome is a second quad and does not grow because the panel
            // did. Left out, the handles stay the size and the place they were
            // while the panel swells out from behind them.
            Eventually.True(
                () => chrome.Framed is { } around && around.Width > 0.6f,
                LongEnough).ShouldBeTrue("the chrome should have been resized too");
        }
    }

    [Fact]
    public void AScale_MovesTheOverlayRatherThanGrowingItAboutItsMiddle()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.TopRightCorner);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            hand.MoveTo(Pinned + ((Beside - Pinned) * 2));

            Eventually.True(
                () => overlay.Placed.Width > 0.6f, LongEnough).ShouldBeTrue();
        }

        // The pinned corner is still where it was, which is only true if the
        // resize carried a new position with it. An overlay that grew about its
        // own middle would leave this corner half a panel away.
        Vector3 corner = overlay.Placed.Where.Position
            - new Vector3(overlay.Placed.Width / 2, overlay.Placed.Height / 2, 0);

        corner.X.ShouldBe(Pinned.X, 0.001f);
        corner.Y.ShouldBe(Pinned.Y, 0.001f);
    }

    [Fact]
    public void APointerOnACorner_WithNoTriggerPulled_ScalesNothing()
    {
        var hand = HandThatMoves.At(Beside);
        using var chrome = ChromeThatReports.PointingAt(Grabbed.BottomLeftCorner);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue();

            hand.MoveTo(Beside + new Vector3(0.6f, 0, 0));

            Thread.Sleep(200);
        }

        // Aiming at a handle is what brings it out of hiding in the first place.
        // Growing on hover would make the chrome impossible to look at.
        overlay.Resizes.ShouldBe(0);
        overlay.Moves.ShouldBe(0);
    }

    [Fact]
    public void AScale_WhenTheTriggerIsLetGo_LeavesTheOverlayTheSizeItWasPulledTo()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.TopRightCorner);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            hand.MoveTo(Pinned + ((Beside - Pinned) * 2));

            Eventually.True(
                () => overlay.Placed.Width > 0.6f, LongEnough).ShouldBeTrue();

            chrome.Reports(new Grip(Grabbed.TopRightCorner, Held: false, TheHand));

            Thread.Sleep(200);

            float pulledTo = overlay.Placed.Width;

            // Let go, then move the hand a long way. A scale that was not ended
            // would keep swelling the overlay while the Commander reaches for
            // the stick.
            hand.MoveTo(Pinned + ((Beside - Pinned) * 8));

            Thread.Sleep(200);

            overlay.Placed.Width.ShouldBe(pulledTo, 0.001f);
        }
    }

    [Fact]
    public void AScale_WhenTheHandStopsBeingTracked_EndsRatherThanFollowingNothing()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.TopRightCorner);
        using var overlay = OverlayThatMoves.Somewhere();

        List<string> recorded = [];

        using (Grabbing.Watching(chrome, overlay, hand, recorded.Add))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            // A controller put down mid-scale. An untracked slot reads as the
            // origin, which is a long way from the pinned corner — so following
            // it would blow the overlay up to its ceiling.
            hand.Vanish();

            Thread.Sleep(200);
        }

        overlay.Placed.Width.ShouldBeLessThan(
            1.0f, "the overlay should not have been stretched by a hand nobody has");
    }

    [Fact]
    public void AScale_PulledPastWhatIsAllowed_StopsAtTheLimitRatherThanFailing()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.TopRightCorner);
        using var overlay = OverlayThatMoves.Somewhere();

        List<string> recorded = [];

        using (Grabbing.Watching(chrome, overlay, hand, recorded.Add))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            hand.MoveTo(Pinned + ((Beside - Pinned) * 40));

            Eventually.True(
                () => overlay.Placed.Width > 1.9f, LongEnough).ShouldBeTrue();
        }

        overlay.Placed.Width.ShouldBe(2f, 0.001f);

        // And quietly. A limit reached by throwing would be a log line per look
        // for as long as the Commander held the trigger.
        recorded.ShouldBeEmpty();
    }

    /// <summary>
    /// A laser on something with the trigger down, which is what a grab looks
    /// like from the adapter.
    /// </summary>
    private static ChromeThatReports Holding(Grabbed under) =>
        new(new Grip(under, Held: true, TheHand));

    private static Action<string> Nowhere => _ => { };
}
