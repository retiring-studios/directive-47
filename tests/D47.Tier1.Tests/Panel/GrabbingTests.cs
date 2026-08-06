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
/// Watching the Commander's hands and telling the chrome what they are on.
///
/// <para>
/// Tier 1: a thread and a clock, no runtime. What a pose means is
/// <c>Pointing</c> and is Tier 0; where a pose comes from is the adapter and is
/// Tier 2. This is the loop in between, and it is the part with the timing bug
/// in it if there is one.
/// </para>
/// </summary>
public class GrabbingTests
{
    private static readonly Board Panel =
        new(new Pose(new Vector3(0, 0, -1.5f), Quaternion.Identity), 0.5f, 0.3f);

    private static readonly TimeSpan LongEnough = TimeSpan.FromSeconds(5);

    [Fact]
    public void Grabbing_WhenAHandIsOnTheBar_TellsTheChromeSo()
    {
        var hands = new HandsThatAre(OnThe(Grabbed.Bar));
        using var chrome = new ChromeThatRemembers();

        using (Grabbing.Watching(hands, chrome, Panel, Nowhere))
        {
            Eventually.True(() => chrome.Lit == Grabbed.Bar, LongEnough)
                .ShouldBeTrue($"the chrome was told {chrome.Lit}");
        }
    }

    [Fact]
    public void Grabbing_WhenTheHandMovesOff_TellsTheChromeThatToo()
    {
        var hands = new HandsThatAre(OnThe(Grabbed.Bar));
        using var chrome = new ChromeThatRemembers();

        using (Grabbing.Watching(hands, chrome, Panel, Nowhere))
        {
            Eventually.True(() => chrome.Lit == Grabbed.Bar, LongEnough).ShouldBeTrue();

            hands.Are([]);

            Eventually.True(() => chrome.Lit == Grabbed.Nothing, LongEnough).ShouldBeTrue(
                "letting go should stop lighting the bar");
        }
    }

    [Fact]
    public void Grabbing_WithNoHandsAtAll_StillAsksButLessOften()
    {
        // The idle case, which is most of a session. It must keep asking — a
        // controller waking up is exactly what it is waiting for — but it must
        // not spin, because both controllers on the desk is the state the
        // application spends hours in.
        var hands = new HandsThatAre([]);
        using var chrome = new ChromeThatRemembers();

        using (Grabbing.Watching(hands, chrome, Panel, Nowhere))
        {
            Eventually.True(() => hands.Asked >= 2, LongEnough).ShouldBeTrue(
                "it should keep looking for a controller that wakes up");
        }

        // Two looks in five seconds is the floor; the point is that it is not
        // hundreds. At the resting interval it should be nowhere near what a
        // tracked hand would produce in the same time.
        hands.Asked.ShouldBeLessThan(
            60, $"idling should not spin, and it asked {hands.Asked} times");
    }

    [Fact]
    public void Grabbing_WhenAskingThrows_KeepsWatchingAndSaysSo()
    {
        // The runtime going away underneath is a fact about the machine rather
        // than a defect, and it must not take the thread with it — a watcher
        // that died on the first hiccup would leave the chrome frozen on
        // whatever it happened to be showing.
        List<string> recorded = [];
        var hands = new HandsThatThrowOnce();
        using var chrome = new ChromeThatRemembers();

        using (Grabbing.Watching(hands, chrome, Panel, recorded.Add))
        {
            Eventually.True(() => chrome.Lit == Grabbed.Content, LongEnough).ShouldBeTrue(
                "it should have carried on to the next look");
        }

        recorded.ShouldNotBeEmpty("a failure nobody wrote down is one nobody can diagnose");
    }

    [Fact]
    public void Grabbing_WhenItStops_LeavesTheChromeAlone()
    {
        var hands = new HandsThatAre(OnThe(Grabbed.Bar));
        using var chrome = new ChromeThatRemembers();

        var watching = Grabbing.Watching(hands, chrome, Panel, Nowhere);

        Eventually.True(() => chrome.Lit == Grabbed.Bar, LongEnough).ShouldBeTrue();

        watching.Dispose();

        int asked = hands.Asked;

        Thread.Sleep(300);

        hands.Asked.ShouldBe(asked, "disposing should have stopped the looking");
    }

    /// <summary>
    /// Where this test's absences go when it does not care about them.
    /// </summary>
    private static Action<string> Nowhere => _ => { };

    /// <summary>
    /// A hand placed so that it is pointing at a given part of the panel.
    /// </summary>
    private static IReadOnlyList<Pose> OnThe(Grabbed part)
    {
        // Worked out from the panel rather than hard-coded, so the fixture does
        // not quietly stop pointing at what it says it does if the chrome's
        // proportions move.
        float up = part == Grabbed.Bar
            ? (-Panel.Height / 2) - (Panel.Height * 0.05f)
            : 0;

        return [new Pose(new Vector3(0, up, -0.5f), Quaternion.Identity)];
    }

    private sealed class HandsThatAre(IReadOnlyList<Pose> held) : IControllers
    {
        private volatile IReadOnlyList<Pose> _held = held;
        private int _asked;

        internal int Asked => Volatile.Read(ref _asked);

        internal void Are(IReadOnlyList<Pose> held) => _held = held;

        public IReadOnlyList<Pose> Tracked()
        {
            Interlocked.Increment(ref _asked);
            return _held;
        }
    }

    private sealed class HandsThatThrowOnce : IControllers
    {
        private int _asked;

        public IReadOnlyList<Pose> Tracked()
        {
            if (Interlocked.Increment(ref _asked) == 1)
            {
                throw new InvalidOperationException("the runtime went away");
            }

            return [new Pose(new Vector3(0, 0, -0.5f), Quaternion.Identity)];
        }
    }

    private sealed class ChromeThatRemembers : IGrabChrome
    {
        private int _lit = (int)Grabbed.Nothing;

        internal Grabbed Lit => (Grabbed)Volatile.Read(ref _lit);

        public void Showing(Grabbed lit) => Volatile.Write(ref _lit, (int)lit);

        public void Dispose()
        {
        }
    }
}
