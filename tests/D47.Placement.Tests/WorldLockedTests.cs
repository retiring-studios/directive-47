using System;
using System.Numerics;

using Shouldly;

using Xunit;

namespace D47.Placement.Tests;

/// <summary>
/// World-locked: the overlay you want to glance at, bolted to the cockpit
/// rather than pinned to your face. And the handover between the two modes,
/// which has to happen without the overlay appearing to move.
/// </summary>
public class WorldLockedTests
{
    private const float CloseEnough = 0.0001f;

    private static readonly Vector3 InFront = new(0, 0, -2);

    private static Pose Still => new(Vector3.Zero, Quaternion.Identity);

    private static Pose Elsewhere =>
        new(new Vector3(1, 0.5f, 3), Quaternion.CreateFromYawPitchRoll(0.7f, -0.3f, 0.1f));

    private static Pose SomewhereElseAgain =>
        new(new Vector3(-4, 2, 9), Quaternion.CreateFromYawPitchRoll(-2.2f, 1.1f, 0.4f));

    [Fact]
    public void WorldLocked_AtAPlace_PutsTheOverlayThere()
    {
        // The happy path. A world-locked overlay is wherever it was bolted, and
        // the transform says so without anybody having to ask where a head is.
        var bolted = new Pose(new Vector3(1, 2, -3), Quaternion.Identity);

        Matrix4x4 overlay = WorldLocked.At(bolted);

        overlay.Translation.X.ShouldBe(1, CloseEnough);
        overlay.Translation.Y.ShouldBe(2, CloseEnough);
        overlay.Translation.Z.ShouldBe(-3, CloseEnough);
    }

    [Fact]
    public void WorldLocked_WhenTheHeadMovesAfterwards_HoldsItsPlace()
    {
        // The criterion, and the half of it that can actually fail. That
        // WorldLocked.At ignores the head is true of its signature and proves
        // nothing on its own — what is worth asserting is that a placement
        // taken over from head-locked then stops following, which is the whole
        // difference between the two modes.
        Pose bolted = WorldLocked.TakingOver(Still, InFront);

        Matrix4x4 held = WorldLocked.At(bolted);
        Matrix4x4 wouldHaveFollowed = HeadLocked.Follows(Elsewhere, InFront);

        // Where it was when the head was still, and not where the head went.
        held.Translation.Z.ShouldBe(-2, CloseEnough);

        Vector3.Distance(held.Translation, wouldHaveFollowed.Translation).ShouldBeGreaterThan(
            0.5f,
            "a world-locked overlay that moved with the head would be head-locked");
    }

    [Theory]
    [MemberData(nameof(Heads))]
    public void Switching_ToTheWorld_DoesNotMoveTheOverlay(Pose head)
    {
        // "Switching between the two modes does not lose the placement", and
        // the strictest reading of it: at the instant of the handover the
        // overlay is in exactly the same place, so a Commander watching sees
        // nothing happen at all.
        Pose bolted = WorldLocked.TakingOver(head, InFront);

        Matrix4x4 before = HeadLocked.Follows(head, InFront);
        Matrix4x4 after = WorldLocked.At(bolted);

        Vector3.Distance(after.Translation, before.Translation).ShouldBe(
            0,
            CloseEnough,
            $"the overlay jumped when the mode changed, with the head at {head.Position}");

        Facing(after).ShouldBe(Facing(before), CloseEnough, "and it turned as well as jumped");
    }

    [Theory]
    [MemberData(nameof(Heads))]
    public void Switching_BackToTheHead_DoesNotMoveTheOverlay(Pose head)
    {
        // The same claim in the other direction. Both are needed: an
        // implementation that rotated one way round would pass one of these and
        // fail the other, which is the failure a single direction would hide.
        var bolted = new Pose(new Vector3(1, 2, -3), Quaternion.Identity);

        Vector3 inView = HeadLocked.TakingOver(head, bolted);

        Matrix4x4 before = WorldLocked.At(bolted);
        Matrix4x4 after = HeadLocked.Follows(head, inView);

        Vector3.Distance(after.Translation, before.Translation).ShouldBe(
            0,
            CloseEnough,
            $"the overlay jumped when the mode changed back, with the head at {head.Position}");
    }

    [Theory]
    [MemberData(nameof(Heads))]
    public void Switching_ThereAndBack_LeavesThePlacementWhereItWas(Pose head)
    {
        // A Commander who changes their mind twice should be where they
        // started, rather than a little further out each time.
        Pose bolted = WorldLocked.TakingOver(head, InFront);
        Vector3 returned = HeadLocked.TakingOver(head, bolted);

        returned.X.ShouldBe(InFront.X, CloseEnough);
        returned.Y.ShouldBe(InFront.Y, CloseEnough);
        returned.Z.ShouldBe(InFront.Z, CloseEnough);
    }

    [Fact]
    public void Switching_WhenTheHeadHasMovedSinceTheHandover_AttachesItWhereItIsNow()
    {
        // Going back to head-locked is not undoing the first switch. Whatever
        // the Commander has done in between, the overlay attaches where it
        // currently is relative to where they currently are — so this is not the
        // round trip above and does not answer the same offset.
        Pose bolted = WorldLocked.TakingOver(Still, InFront);

        Vector3 attached = HeadLocked.TakingOver(SomewhereElseAgain, bolted);

        Matrix4x4 after = HeadLocked.Follows(SomewhereElseAgain, attached);

        Vector3.Distance(after.Translation, bolted.Position).ShouldBe(
            0,
            CloseEnough,
            "it should still be exactly where it was bolted");
    }

    [Fact]
    public void WorldLocked_ForAPoseThatIsNotFinite_FailsLoudlyRatherThanGuessing()
    {
        var lost = new Pose(new Vector3(float.NaN, 0, 0), Quaternion.Identity);

        Should.Throw<ArgumentException>(() => WorldLocked.At(lost));
    }

    [Fact]
    public void WorldLocked_TakingOverFromAPoseThatIsNotFinite_FailsLoudlyRatherThanGuessing()
    {
        var lost = new Pose(Vector3.Zero, new Quaternion(0, float.NaN, 0, 1));

        Should.Throw<ArgumentException>(() => WorldLocked.TakingOver(lost, InFront));
    }

    [Fact]
    public void HeadLocked_TakingOverFromAPoseThatIsNotFinite_FailsLoudlyRatherThanGuessing()
    {
        var lost = new Pose(new Vector3(0, float.PositiveInfinity, 0), Quaternion.Identity);

        Should.Throw<ArgumentException>(() => HeadLocked.TakingOver(Still, lost));
    }

    public static TheoryData<Pose> Heads() => new(Still, Elsewhere, SomewhereElseAgain);

    /// <summary>
    /// How far the transform's rotation is from the identity, as one number, so
    /// two orientations can be compared without caring which of the two
    /// quaternion spellings each came out as.
    /// </summary>
    private static float Facing(Matrix4x4 transform) =>
        MathF.Abs(Quaternion.Dot(
            Quaternion.CreateFromRotationMatrix(transform),
            Quaternion.Identity));
}
