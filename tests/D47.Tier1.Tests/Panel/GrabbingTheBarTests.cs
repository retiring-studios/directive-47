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
/// </summary>
public class GrabbingTheBarTests
{
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(5);

    private const uint TheHand = 3;

    [Fact]
    public void Grab_OnTheBar_MovesTheOverlay()
    {
        var hand = new HandAt(new Vector3(0.3f, -0.4f, -0.6f));
        using var chrome = new ChromeReporting(new Grip(Grabbed.Bar, Held: true, TheHand));
        using var overlay = new OverlayAt(new Vector3(0, 0, -1.5f));

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            // The hand moves while the trigger is still down.
            hand.MoveTo(new Vector3(0.5f, -0.4f, -0.6f));

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
        var hand = new HandAt(new Vector3(0.3f, -0.4f, -0.6f));
        using var chrome = new ChromeReporting(new Grip(Grabbed.Bar, Held: true, TheHand));
        using var overlay = new OverlayAt(new Vector3(0, 0, -1.5f));

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            hand.MoveTo(new Vector3(0.5f, -0.4f, -0.6f));

            // The chrome is a second quad and does not move because the panel
            // did. Left out, the bar stays where the Commander first grabbed it
            // while the panel slides away from it.
            Eventually.True(
                () => chrome.Framed is { } around && around.Where.Position.X > 0.1f,
                LongEnough).ShouldBeTrue("the chrome should have been moved too");
        }
    }

    [Fact]
    public void ApointerOnTheBar_WithNoTriggerPulled_MovesNothing()
    {
        var hand = new HandAt(new Vector3(0.3f, -0.4f, -0.6f));
        using var chrome = new ChromeReporting(new Grip(Grabbed.Bar, Held: false, TheHand));
        using var overlay = new OverlayAt(new Vector3(0, 0, -1.5f));

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue();

            hand.MoveTo(new Vector3(0.9f, -0.4f, -0.6f));

            Thread.Sleep(200);
        }

        // Aiming at the bar is what a Commander does on the way to reading the
        // panel. Moving on hover would make the overlay impossible to look at.
        overlay.Moves.ShouldBe(0);
    }

    [Fact]
    public void AGrab_OnTheContent_MovesNothingEither()
    {
        var hand = new HandAt(new Vector3(0.3f, -0.4f, -0.6f));
        using var chrome = new ChromeReporting(new Grip(Grabbed.Content, Held: true, TheHand));
        using var overlay = new OverlayAt(new Vector3(0, 0, -1.5f));

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue();

            hand.MoveTo(new Vector3(0.9f, -0.4f, -0.6f));

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
        var hand = new HandAt(new Vector3(0.3f, -0.4f, -0.6f));
        using var chrome = new ChromeReporting(new Grip(Grabbed.Bar, Held: true, TheHand));
        using var overlay = new OverlayAt(new Vector3(0, 0, -1.5f));

        using (Grabbing.Watching(chrome, overlay, hand, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

            hand.MoveTo(new Vector3(0.5f, -0.4f, -0.6f));

            Eventually.True(
                () => overlay.Placed.Where.Position.X > 0.1f, LongEnough).ShouldBeTrue();

            chrome.Reports(new Grip(Grabbed.Bar, Held: false, TheHand));

            Thread.Sleep(200);

            float wherePut = overlay.Placed.Where.Position.X;

            // Let go, then move the hand a long way. A grab that was not ended
            // would drag the overlay across the cockpit while the Commander
            // reaches for the stick.
            hand.MoveTo(new Vector3(2.0f, -0.4f, -0.6f));

            Thread.Sleep(200);

            overlay.Placed.Where.Position.X.ShouldBe(wherePut, 0.001f);
        }
    }

    [Fact]
    public void AGrab_WhenTheHandStopsBeingTracked_EndsRatherThanFollowingNothing()
    {
        var hand = new HandAt(new Vector3(0.3f, -0.4f, -0.6f));
        using var chrome = new ChromeReporting(new Grip(Grabbed.Bar, Held: true, TheHand));
        using var overlay = new OverlayAt(new Vector3(0, 0, -1.5f));

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

    private static Action<string> Nowhere => _ => { };

    /// <summary>
    /// One controller, wherever the test last put it.
    /// </summary>
    ///
    /// <remarks>
    /// Locked rather than volatile, here and below. Everything being passed
    /// between the test and the watching thread is a struct, and
    /// <c>Volatile</c> does not take those — a lock is the cheap correct answer
    /// and none of this is on a hot path.
    /// </remarks>
    private sealed class HandAt(Vector3 where) : IControllers
    {
        private readonly Lock _guard = new();

        private Pose? _at = new(where, Quaternion.Identity);

        internal void MoveTo(Vector3 moved)
        {
            lock (_guard)
            {
                _at = new Pose(moved, Quaternion.Identity);
            }
        }

        internal void Vanish()
        {
            lock (_guard)
            {
                _at = null;
            }
        }

        public IReadOnlyList<Pose> Tracked() => At(0) is { } at ? [at] : [];

        public Pose? At(uint device)
        {
            lock (_guard)
            {
                return _at;
            }
        }
    }

    /// <summary>
    /// Chrome that reports whatever the test tells it to.
    /// </summary>
    private sealed class ChromeReporting(Grip reported) : IGrabChrome
    {
        private readonly Lock _guard = new();

        private Grip _reported = reported;
        private Board? _framed;
        private int _followed;

        internal int Followed => Volatile.Read(ref _followed);

        internal Board? Framed
        {
            get
            {
                lock (_guard)
                {
                    return _framed;
                }
            }
        }

        internal void Reports(Grip next)
        {
            lock (_guard)
            {
                _reported = next;
            }
        }

        public void Showing(Grabbed lit)
        {
        }

        public Grip Follow()
        {
            Interlocked.Increment(ref _followed);

            lock (_guard)
            {
                return _reported;
            }
        }

        public void Frames(Board panel)
        {
            lock (_guard)
            {
                _framed = panel;
            }
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A headset overlay that only remembers where it was put.
    /// </summary>
    private sealed class OverlayAt : IHeadsetOverlay
    {
        private readonly Lock _guard = new();

        private Board _placed;
        private int _moves;

        internal OverlayAt(Vector3 where)
        {
            _placed = new Board(new Pose(where, Quaternion.Identity), 0.5f, 0.3f);
        }

        public bool IsVisible => true;

        public Board Placed
        {
            get
            {
                lock (_guard)
                {
                    return _placed;
                }
            }
        }

        internal int Moves => Volatile.Read(ref _moves);

        public void MoveTo(Pose where)
        {
            Interlocked.Increment(ref _moves);

            lock (_guard)
            {
                _placed = _placed with { Where = where };
            }
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Paint(D47.Render.Presentation presented)
        {
        }

        public void Dispose()
        {
        }
    }
}
