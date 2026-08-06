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
/// Dragging the bar, as a loop rather than as arithmetic.
///
/// <para>
/// Tier 1: a thread and three stand-ins, no runtime. Where the overlay lands
/// given a hand is <c>Grab</c> in <c>D47.Placement</c> and is Tier 0. What is
/// here is when a grab starts, when it stops, and what moves while it lasts.
/// </para>
///
/// <para>
/// The stand-ins are shared with <c>GrabbingTests</c> and live in
/// <c>GrabbingDoubles</c>.
/// </para>
/// </summary>
public class GrabbingTheBarTests
{
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(5);

    private const uint TheHand = 3;

    /// <summary>
    /// Where the hand starts: off to one side of the overlay, which is where a
    /// Commander's actually is.
    /// </summary>
    private static readonly Vector3 Beside = new(0.3f, -0.4f, -0.6f);

    [Fact]
    public void Grab_OnTheBar_MovesTheOverlay()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.Bar);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            // The hand moves while the trigger is still down.
            hand.MoveTo(Beside + new Vector3(0.2f, 0, 0));

            Eventually.True(
                () => overlay.Placed.Where.Position.X > 0.1f, LongEnough).ShouldBeTrue(
                $"the overlay should have followed the hand, and it is at "
                + $"{overlay.Placed.Where.Position}");
        }

        // Twenty centimetres of hand is twenty centimetres of overlay. The
        // arithmetic is Grab's and is asserted at Tier 0; what this catches is
        // the loop handing it the wrong hand, or the wrong starting pose.
        overlay.Placed.Where.Position.X.ShouldBe(0.2f, 0.001f);
    }

    [Fact]
    public void Grab_WhileItLasts_KeepsTheChromeAroundTheOverlayItIsMoving()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.Bar);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            hand.MoveTo(Beside + new Vector3(0.2f, 0, 0));

            // The chrome is a second quad and does not move because the panel
            // did. Left out, the bar stays where the Commander first grabbed it
            // while the panel slides away from it.
            Eventually.True(
                () => chrome.Framed is { } around && around.Where.Position.X > 0.1f,
                LongEnough).ShouldBeTrue("the chrome should have been moved too");
        }
    }

    [Fact]
    public void APointerOnTheBar_WithNoTriggerPulled_MovesNothing()
    {
        var hand = HandThatMoves.At(Beside);
        using var chrome = ChromeThatReports.PointingAt(Grabbed.Bar);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue();

            hand.MoveTo(Beside + new Vector3(0.6f, 0, 0));

            Thread.Sleep(200);
        }

        // Aiming at the bar is what a Commander does on the way to reading the
        // panel. Moving on hover would make the overlay impossible to look at.
        overlay.Moves.ShouldBe(0);
    }

    [Fact]
    public void AGrab_OnTheContent_MovesNothingEither()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.Content);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue();

            hand.MoveTo(Beside + new Vector3(0.6f, 0, 0));

            Thread.Sleep(200);
        }

        // The decision recorded in docs/decisions.md: pointing at the panel
        // itself grabs nothing, so it can be read without being shoved out of
        // the way by somebody who only wanted to look at it.
        overlay.Moves.ShouldBe(0);
    }

    [Fact]
    public void AGrab_WhenTheTriggerIsLetGo_LeavesTheOverlayWhereItWasPut()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.Bar);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            hand.MoveTo(Beside + new Vector3(0.2f, 0, 0));

            Eventually.True(
                () => overlay.Placed.Where.Position.X > 0.1f, LongEnough).ShouldBeTrue();

            chrome.Reports(new Grip(Grabbed.Bar, Held: false, TheHand));

            Thread.Sleep(200);

            float wherePut = overlay.Placed.Where.Position.X;

            // Let go, then move the hand a long way. A grab that was not ended
            // would drag the overlay across the cockpit while the Commander
            // reaches for the stick.
            hand.MoveTo(Beside + new Vector3(1.7f, 0, 0));

            Thread.Sleep(200);

            overlay.Placed.Where.Position.X.ShouldBe(wherePut, 0.001f);
        }
    }

    [Fact]
    public void AGrab_WhenTheHandStopsBeingTracked_EndsRatherThanFollowingNothing()
    {
        var hand = HandThatMoves.At(Beside);
        using ChromeThatReports chrome = Holding(Grabbed.Bar);
        using var overlay = OverlayThatMoves.Somewhere();

        List<string> recorded = [];

        using (Grabbing.Watching(chrome, overlay, hand, recorded.Add))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            // A controller going to sleep mid-grab, which is what happens if a
            // Commander puts it down without letting go. The overlay stays where
            // it was rather than snapping to the origin, which is where an
            // untracked slot reads as being.
            hand.Vanish();

            Thread.Sleep(200);
        }

        overlay.Placed.Where.Position.Length().ShouldBeGreaterThan(
            1.0f, "the overlay should not have been dragged to the Commander's feet");
    }

    /// <summary>
    /// A laser on something with the trigger down, which is what a grab looks
    /// like from the adapter.
    /// </summary>
    private static ChromeThatReports Holding(Grabbed under) =>
        new(new Grip(under, Held: true, TheHand));

    private static Action<string> Nowhere => _ => { };
}
