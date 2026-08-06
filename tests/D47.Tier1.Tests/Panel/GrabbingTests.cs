using System;
using System.Collections.Generic;
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
///
/// <para>
/// Nothing here pulls a trigger, so no drag ever starts. That is
/// <c>GrabbingTheBarTests</c>, and the doubles both use are in
/// <c>GrabbingDoubles</c>.
/// </para>
/// </summary>
public class GrabbingTests
{
    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(5);

    [Fact]
    public void Grabbing_KeepsAskingTheChromeWhatIsUnderThePointer()
    {
        using var chrome = ChromeThatReports.PointingAt(Grabbed.Bar);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Watch(chrome, overlay))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue(
                $"it should keep following, and it asked {chrome.Followed} times");
        }

        // Pointing is not grabbing. Worth asserting here rather than only in the
        // drag tests, because this is the loop that runs for hours while a
        // Commander reads the panel.
        overlay.Moves.ShouldBe(0);
    }

    [Fact]
    public void Grabbing_WhenNothingIsPointedAt_LooksLessOften()
    {
        // Pointing at nothing is the state a Commander spends hours in, and
        // #235 asks for it to cost nothing. It must keep looking — a laser
        // arriving is what it is waiting for — but it must not spin.
        using var chrome = ChromeThatReports.PointingAt(Grabbed.Nothing);
        using var overlay = OverlayThatMoves.Somewhere();

        using (Watch(chrome, overlay))
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
        using var busy = ChromeThatReports.PointingAt(Grabbed.Bar);
        using var idle = ChromeThatReports.PointingAt(Grabbed.Nothing);
        using var one = OverlayThatMoves.Somewhere();
        using var another = OverlayThatMoves.Somewhere();

        using (Watch(busy, one))
        using (Watch(idle, another))
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
        using var overlay = OverlayThatMoves.Somewhere();

        using (Watch(chrome, overlay, recorded.Add))
        {
            Eventually.True(() => chrome.Followed >= 2, LongEnough).ShouldBeTrue(
                "it should have carried on to the next look");
        }

        recorded.ShouldNotBeEmpty("a failure nobody wrote down is one nobody can diagnose");
    }

    [Fact]
    public void Grabbing_WhenItStops_StopsLooking()
    {
        using var chrome = ChromeThatReports.PointingAt(Grabbed.Bar);
        using var overlay = OverlayThatMoves.Somewhere();

        Grabbing watching = Watch(chrome, overlay);

        Eventually.True(() => chrome.Followed >= 1, LongEnough).ShouldBeTrue();

        watching.Dispose();

        int looked = chrome.Followed;

        Thread.Sleep(300);

        chrome.Followed.ShouldBe(looked, "disposing should have stopped the looking");
    }

    /// <summary>
    /// Watching, with a hand nobody is holding — none of this is about where the
    /// controllers are.
    /// </summary>
    private static Grabbing Watch(
        IGrabChrome chrome, IHeadsetOverlay overlay, Action<string>? record = null) =>
        Grabbing.Watching(chrome, overlay, HandThatMoves.Asleep(), record ?? (_ => { }));

    /// <summary>
    /// Fault injection rather than a duplicate of <see cref="ChromeThatReports"/>,
    /// which is why this one stayed local.
    /// </summary>
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
