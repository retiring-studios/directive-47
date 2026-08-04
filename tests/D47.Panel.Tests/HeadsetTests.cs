using System.Collections.Generic;

using D47.Capabilities;
using D47.Render;
using D47.TestSupport;
using D47.VrOverlay;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// What the application does when there is no SteamVR to put an overlay in.
///
/// <para>
/// Most machines running Directive 47 have no headset attached, which is a
/// machine that cannot give us what the overlay needs rather than a defect. The
/// twin of <see cref="OverlayTests"/>'s first four facts, and asserted the same
/// way and for the same reason.
/// </para>
///
/// <para>
/// Tier 1 and in process. Nothing here needs a headset, a runtime, or a
/// desktop — which is the whole point of a seam in front of the adapter, since
/// a real SteamVR cannot be asked to be absent on a machine where it is
/// running.
/// </para>
/// </summary>
public class HeadsetTests
{
    /// <summary>
    /// Where this test's absences are written down. One per test, because xUnit
    /// builds the class afresh for each fact.
    /// </summary>
    private readonly List<string> _recorded = [];

    [Fact]
    public void HeadsetOverlay_WhenItCannotBeCreated_IsAbsentNotFailed()
    {
        var machineThatCannot = new HeadsetFactoryThatRefuses();

        Headset headset = Should.NotThrow(
            () => Headset.From(machineThatCannot, Fixtures.HelpsAnswer(), _recorded.Add));

        // Asked, and not merely survived. Without this the test passes on any
        // machine with no SteamVR, for a reason that has nothing to do with
        // what it claims to check.
        machineThatCannot.WasAsked.ShouldBeTrue(
            "the application should have asked for an overlay before doing without one");

        // And it stays absent rather than failing later. Nothing that happens
        // to a headset overlay that was never created is an error to report.
        Should.NotThrow(headset.Show);
        Should.NotThrow(headset.Hide);
        Should.NotThrow(headset.Dispose);
    }

    [Fact]
    public void HeadsetOverlay_WhenTheMachineCannotHostOne_LeavesARecord()
    {
        // An absence nobody wrote down is indistinguishable from a defect. On a
        // machine with no headset that is every single run, so the one line
        // saying why is the only evidence there will ever be.
        using var headset = Headset.From(
            new HeadsetFactoryThatRefuses(), Fixtures.HelpsAnswer(), _recorded.Add);

        _recorded.ShouldHaveSingleItem().ShouldContain(
            "SteamVR",
            customMessage: "the record should name what was missing, not merely that something was");
    }

    [Fact]
    public void HeadsetOverlay_WhenTheMachineCanHostOne_SaysNothingAtAll()
    {
        // The other half, and the one that keeps the log worth reading. A line
        // written on every ordinary startup is a line nobody looks at twice.
        using var headset = Headset.From(
            new HeadsetFactoryReturning(new HeadsetOverlayThatRemembers()),
            Fixtures.HelpsAnswer(),
            _recorded.Add);

        _recorded.ShouldBeEmpty("nothing was carried on without");
    }

    [Fact]
    public void HeadsetOverlay_WhenCreationFailsForAnyOtherReason_SaysSoRatherThanGoingQuiet()
    {
        // The criterion that matters more than the one above it. "Absent, not
        // failed" is one try/catch away from reporting our own defects as a
        // machine without a headset, and this is what stops that being written.
        Should.Throw<StandInFailure>(
            () => Headset.From(
                new HeadsetFactoryThatIsBroken(), Fixtures.HelpsAnswer(), _recorded.Add));
    }

    [Fact]
    public void HeadsetOverlay_WhenTheMachineCanHostOne_IsShownRatherThanMerelyMade()
    {
        // A quad SteamVR knows about but is not showing is an overlay the
        // Commander cannot see, which is the same outcome as not having one and
        // a great deal more code.
        var inTheHeadset = new HeadsetOverlayThatRemembers();

        using var headset = Headset.From(
            new HeadsetFactoryReturning(inTheHeadset), Fixtures.HelpsAnswer(), _recorded.Add);

        headset.Show();

        inTheHeadset.IsVisible.ShouldBeTrue("the overlay should be up in the headset");
    }

    [Fact]
    public void HeadsetOverlay_WhenTheApplicationExits_GivesTheQuadBack()
    {
        // The compositor's, not ours. A quad nobody gave back outlives the
        // process that made it — a rectangle floating in somebody's cockpit
        // with nothing running to close it, and a slot held in SteamVR's list of
        // applications. The application's own Dispose reaches this.
        var inTheHeadset = new HeadsetOverlayThatRemembers();

        var headset = Headset.From(
            new HeadsetFactoryReturning(inTheHeadset), Fixtures.HelpsAnswer(), _recorded.Add);

        headset.Dispose();

        inTheHeadset.WasGivenBack.ShouldBeTrue("the quad should have gone back to the compositor");
    }

    [Fact]
    public void HeadsetOverlay_WhenTheAnswerChanges_RepaintsTheTexture()
    {
        // An overlay showing a stale frame is worse than one that is not there.
        // It is confidently wrong about what the Commander is looking at.
        var inTheHeadset = new HeadsetOverlayThatRemembers();

        using var headset = Headset.From(
            new HeadsetFactoryReturning(inTheHeadset), Fixtures.HelpsAnswer(), _recorded.Add);

        Answer somethingElse = Fixtures.HelpsAnswer() with
        {
            Result = new ListResult { CapabilityId = "help", Items = ["a different answer"] },
        };

        headset.Showing(somethingElse);

        inTheHeadset.Paints.ShouldBe(1, "a changed answer should have reached the headset");
        inTheHeadset.Showing.ShouldBe(somethingElse);
    }

    [Fact]
    public void HeadsetOverlay_WhenTheAnswerIsUnchanged_DoesNotRepaint()
    {
        // The other half of the same decision. Repainting a texture nothing has
        // changed spends a headset's frame budget on nothing, and the headset is
        // where that budget is scarcest.
        //
        // A separately built answer with identical content, not the same object
        // — which only tells the two apart because answers now compare as
        // values. Handing back the very same instance would prove nothing but
        // reference equality.
        var inTheHeadset = new HeadsetOverlayThatRemembers();

        using var headset = Headset.From(
            new HeadsetFactoryReturning(inTheHeadset), Fixtures.HelpsAnswer(), _recorded.Add);

        headset.Showing(Fixtures.HelpsAnswer());

        inTheHeadset.Paints.ShouldBe(
            0, "the answer was the same one, so nothing needed drawing again");
    }

    [Fact]
    public void HeadsetOverlay_WhenTheAnswerChangesTwice_RepaintsEachTime()
    {
        // Because "does not repaint" must not be implemented as "repaints once
        // and then never again", which passes both facts above.
        var inTheHeadset = new HeadsetOverlayThatRemembers();

        using var headset = Headset.From(
            new HeadsetFactoryReturning(inTheHeadset), Fixtures.HelpsAnswer(), _recorded.Add);

        headset.Showing(Answering("first"));
        headset.Showing(Answering("second"));

        inTheHeadset.Paints.ShouldBe(2);
    }

    [Fact]
    public void HeadsetOverlay_WithNoOverlayAtAll_TakesANewAnswerQuietly()
    {
        // On a machine with no SteamVR there is nothing to repaint, and a new
        // answer arriving is not an error to report.
        using var headset = Headset.From(
            new HeadsetFactoryThatRefuses(), Fixtures.HelpsAnswer(), _recorded.Add);

        Should.NotThrow(() => headset.Showing(Answering("something new")));
    }

    /// <summary>
    /// Help's answer with different lines in it, so two answers can differ by
    /// content rather than by identity.
    /// </summary>
    /// <param name="line">What the answer should say.</param>
    /// <returns>An answer saying that.</returns>
    private static Answer Answering(string line) =>
        Fixtures.HelpsAnswer() with
        {
            Result = new ListResult { CapabilityId = "help", Items = [line] },
        };

    /// <summary>
    /// A machine with no SteamVR, without needing one to be absent.
    /// </summary>
    private sealed class HeadsetFactoryThatRefuses : IHeadsetOverlayFactory
    {
        internal bool WasAsked { get; private set; }

        public IHeadsetOverlay? Create(Answer answer)
        {
            WasAsked = true;
            return null;
        }
    }

    /// <summary>
    /// An overlay with a defect in it, which is a different thing from a machine
    /// that cannot have one.
    /// </summary>
    private sealed class HeadsetFactoryThatIsBroken : IHeadsetOverlayFactory
    {
        public IHeadsetOverlay? Create(Answer answer) => throw new StandInFailure();
    }

    private sealed class HeadsetFactoryReturning : IHeadsetOverlayFactory
    {
        private readonly IHeadsetOverlay _overlay;

        internal HeadsetFactoryReturning(IHeadsetOverlay overlay)
        {
            _overlay = overlay;
        }

        public IHeadsetOverlay? Create(Answer answer) => _overlay;
    }

    /// <summary>
    /// An overlay that is nothing but whether it is up and what it was last
    /// asked to show — which is as much as <see cref="IHeadsetOverlay"/>
    /// promises anybody outside the adapter.
    /// </summary>
    private sealed class HeadsetOverlayThatRemembers : IHeadsetOverlay
    {
        public bool IsVisible { get; private set; }

        internal bool WasGivenBack { get; private set; }

        /// <summary>
        /// How many times it was asked to paint. Counted rather than flagged,
        /// because "did not repaint" and "repainted twice" are both failures and
        /// a boolean can only tell you about one of them.
        /// </summary>
        internal int Paints { get; private set; }

        internal Answer? Showing { get; private set; }

        public void Show() => IsVisible = true;

        public void Hide() => IsVisible = false;

        public void Paint(Answer answer)
        {
            Paints++;
            Showing = answer;
        }

        public void Dispose()
        {
            WasGivenBack = true;
            IsVisible = false;
        }
    }
}
