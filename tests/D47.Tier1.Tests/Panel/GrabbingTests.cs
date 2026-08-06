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
/// Keeping the chrome following the laser.
///
/// <para>
/// Tier 1: a thread and a clock, no runtime. Where the laser is and what it is
/// on are SteamVR's and <c>Chrome.On</c>'s respectively; this is the loop in
/// between, and it is the part with the timing bug in it if there is one.
/// </para>
/// </summary>
public class GrabbingTests
{
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(5);

    [Fact]
    public void Grabbing_KeepsAskingTheChromeWhatIsUnderThePointer()
    {
        using var chrome = new ChromeThatIs(Grabbed.Bar);

        using (Watch(chrome, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue(
                $"it should keep following, and it asked {chrome.Followed} times");
        }
    }

    [Fact]
    public void Grabbing_WhenNothingIsPointedAt_LooksLessOften()
    {
        // Pointing at nothing is the state a Commander spends hours in, and
        // #235 asks for it to cost nothing. It must keep looking — a laser
        // arriving is what it is waiting for — but it must not spin.
        using var chrome = new ChromeThatIs(Grabbed.Nothing);

        using (Watch(chrome, Nowhere))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue(
                "it should keep looking for a laser that arrives");
        }

        chrome.Followed.ShouldBeLessThan(
            60, $"idling should not spin, and it looked {chrome.Followed} times");
    }

    [Fact]
    public void Grabbing_WhenSomethingIsPointedAt_LooksMoreOften()
    {
        // The other half of the throttle, and the one that says the two
        // intervals are not the same number written twice.
        using var busy = new ChromeThatIs(Grabbed.Bar);
        using var idle = new ChromeThatIs(Grabbed.Nothing);

        using (Watch(busy, Nowhere))
        using (Watch(idle, Nowhere))
        {
            Thread.Sleep(1000);
        }

        busy.Followed.ShouldBeGreaterThan(
            idle.Followed,
            "a laser on the chrome should be looked at more often than an empty cockpit");
    }

    [Fact]
    public void Grabbing_WhenFollowingThrows_KeepsWatchingAndSaysSo()
    {
        // The runtime going away underneath is a fact about the machine rather
        // than a defect, and it must not take the thread with it — a watcher
        // that died on the first hiccup would leave the chrome frozen on
        // whatever it happened to be showing.
        List<string> recorded = [];
        using var chrome = new ChromeThatThrowsOnce();

        using (Watch(chrome, recorded.Add))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue(
                "it should have carried on to the next look");
        }

        recorded.ShouldNotBeEmpty("a failure nobody wrote down is one nobody can diagnose");
    }

    [Fact]
    public void Grabbing_WhenItStops_StopsLooking()
    {
        using var chrome = new ChromeThatIs(Grabbed.Bar);

        Grabbing watching = Watch(chrome, Nowhere);

        Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

        watching.Dispose();

        int looked = chrome.Followed;

        Thread.Sleep(300);

        chrome.Followed.ShouldBe(looked, "disposing should have stopped the looking");
    }

    /// <summary>
    /// Where this test's absences go when it does not care about them.
    /// </summary>
    private static Action<string> Nowhere => _ => { };

    /// <summary>
    /// Watching, with stand-ins for the two things these tests say nothing
    /// about. Nothing here holds a trigger, so no drag ever starts and the
    /// overlay is never moved.
    /// </summary>
    private static Grabbing Watch(IGrabChrome chrome, Action<string> record) =>
        Grabbing.Watching(chrome, NoOverlay, new NoHands(), record);

    /// <summary>
    /// One of them, shared. It holds nothing and does nothing, so a fresh one
    /// per test would be an object to dispose for no reason — and disposing it
    /// is not <c>Grabbing</c>'s job, since it did not create it.
    /// </summary>
    private static readonly OverlayGoingNowhere NoOverlay = new();

    private sealed class NoHands : IControllers
    {
        public IReadOnlyList<Pose> Tracked() => [];

        public Pose? At(uint device) => null;
    }

    private sealed class OverlayGoingNowhere : IHeadsetOverlay
    {
        public bool IsVisible => true;

        public Board Placed { get; } =
            new(new Pose(new Vector3(0, 0, -1.5f), Quaternion.Identity), 0.5f, 0.3f);

        public void MoveTo(Pose where) =>
            throw new InvalidOperationException("nothing in here should move the overlay");

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

    private sealed class ChromeThatIs(Grabbed under) : IGrabChrome
    {
        private int _followed;

        internal int Followed => Volatile.Read(ref _followed);

        public void Showing(Grabbed lit)
        {
        }

        // No trigger. These tests are about the looking, and a held one would
        // start a drag they say nothing about — GrabbingTheBarTests is where
        // that belongs.
        public Grip Follow()
        {
            Interlocked.Increment(ref _followed);
            return new Grip(under, Held: false, Hand: 0);
        }

        public void Frames(Board panel)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ChromeThatThrowsOnce : IGrabChrome
    {
        private int _followed;

        internal int Followed => Volatile.Read(ref _followed);

        public void Showing(Grabbed lit)
        {
        }

        public Grip Follow()
        {
            if (Interlocked.Increment(ref _followed) == 1)
            {
                throw new InvalidOperationException("the runtime went away");
            }

            return new Grip(Grabbed.Content, Held: false, Hand: 0);
        }

        public void Frames(Board panel)
        {
        }

        public void Dispose()
        {
        }
    }
}
