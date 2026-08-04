using System;
using System.Numerics;

using Shouldly;

using Xunit;

namespace D47.Placement.Tests;

/// <summary>
/// Head-locked: the overlay you want to read right now, wherever you happen to
/// be looking. It goes where the head goes.
/// </summary>
public class HeadLockedTests
{
    private const float CloseEnough = 0.0001f;

    /// <summary>
    /// Two metres forward. Forward is negative Z, which is the convention every
    /// tracking runtime this will meet already uses.
    /// </summary>
    private static readonly Vector3 InFront = new(0, 0, -2);

    private static Pose Still => new(Vector3.Zero, Quaternion.Identity);

    [Fact]
    public void HeadLocked_WithTheHeadStill_PutsTheOverlayWhereTheOffsetSays()
    {
        // The happy path. Nothing rotated and nothing moved, so the overlay is
        // exactly where it was asked to be.
        Matrix4x4 overlay = HeadLocked.Follows(Still, InFront);

        overlay.Translation.X.ShouldBe(0, CloseEnough);
        overlay.Translation.Y.ShouldBe(0, CloseEnough);
        overlay.Translation.Z.ShouldBe(-2, CloseEnough);
    }

    [Fact]
    public void HeadLocked_WhenTheHeadTurns_TheOverlayComesRound()
    {
        // A quarter turn to the left. What was two metres ahead is now two
        // metres to the left in the world, because the overlay is pinned to the
        // view rather than to the room — which is the whole of this story.
        var turned = new Pose(
            Vector3.Zero,
            Quaternion.CreateFromYawPitchRoll(MathF.PI / 2, 0, 0));

        Matrix4x4 overlay = HeadLocked.Follows(turned, InFront);

        overlay.Translation.X.ShouldBe(-2, CloseEnough);
        overlay.Translation.Y.ShouldBe(0, CloseEnough);
        overlay.Translation.Z.ShouldBe(0, CloseEnough);
    }

    [Fact]
    public void HeadLocked_WhenTheCommanderLeansAcross_TheOverlayGoesWithThem()
    {
        // Rotation is not the only way a head moves. A pose that only followed
        // orientation would leave the overlay behind the moment somebody leaned
        // in to look at a panel.
        var leaned = new Pose(new Vector3(1, 0.5f, 3), Quaternion.Identity);

        Matrix4x4 overlay = HeadLocked.Follows(leaned, InFront);

        overlay.Translation.X.ShouldBe(1, CloseEnough);
        overlay.Translation.Y.ShouldBe(0.5f, CloseEnough);
        overlay.Translation.Z.ShouldBe(1, CloseEnough);
    }

    [Fact]
    public void HeadLocked_AtAnyHeadPose_KeepsTheOverlayTheSameDistanceAway()
    {
        // The invariant underneath the three cases above, and the one that
        // would catch a transform composed the wrong way round. Multiplying in
        // the other order still moves the overlay when the head moves, so every
        // "it moved" assertion passes — what it gets wrong is how far away the
        // overlay ends up, which is the thing a Commander would actually notice.
        Pose[] poses =
        [
            Still,
            new Pose(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(MathF.PI / 2, 0, 0)),
            new Pose(new Vector3(1, 0.5f, 3), Quaternion.CreateFromYawPitchRoll(0.7f, -0.3f, 0.1f)),
            new Pose(new Vector3(-4, 2, 9), Quaternion.CreateFromYawPitchRoll(-2.2f, 1.1f, 0.4f)),
        ];

        foreach (Pose head in poses)
        {
            Matrix4x4 overlay = HeadLocked.Follows(head, InFront);

            Vector3.Distance(overlay.Translation, head.Position).ShouldBe(
                InFront.Length(),
                CloseEnough,
                $"the overlay should stay {InFront.Length()}m from a head at {head.Position}");
        }
    }

    [Fact]
    public void HeadLocked_WhereverTheHeadIsLooking_KeepsTheOverlaySquareToTheView()
    {
        // Following the view is not only about where. An overlay that held the
        // room's orientation while tracking the head's position would be edge-on
        // the moment the Commander turned, which is a surface you cannot read
        // rather than one in the wrong place.
        var looking = Quaternion.CreateFromYawPitchRoll(0.7f, -0.3f, 0.1f);
        var head = new Pose(new Vector3(1, 0.5f, 3), looking);

        Matrix4x4 overlay = HeadLocked.Follows(head, InFront);

        var facing = Quaternion.CreateFromRotationMatrix(overlay);

        // Quaternions double-cover rotations, so q and -q are the same turn.
        // The absolute dot product is 1 for both spellings and nothing else.
        MathF.Abs(Quaternion.Dot(facing, looking)).ShouldBe(1, CloseEnough);
    }

    [Fact]
    public void HeadLocked_GivenAnOrientationThatHasDrifted_StillPlacesItTheDistanceAsked()
    {
        // Quaternions out of a tracking runtime drift off unit length over a
        // session. Left alone, a quaternion 1% long scales the whole transform,
        // so the overlay creeps away from the Commander the longer they play —
        // which reads as the overlay being wrong rather than the pose it was
        // given. Normalised here for the same reason Anchor normalises a gaze
        // direction.
        var drifted = new Pose(Vector3.Zero, new Quaternion(0, 0.4f, 0, 0.4f));

        Matrix4x4 overlay = HeadLocked.Follows(drifted, InFront);

        overlay.Translation.Length().ShouldBe(InFront.Length(), CloseEnough);
    }

    [Fact]
    public void HeadLocked_ForAPoseThatIsNotFinite_FailsLoudlyRatherThanGuessing()
    {
        // Same reasoning and same answer as Anchor: a dropped tracking frame
        // hands back NaN, and an infinite transform is an overlay that vanishes.
        var lost = new Pose(new Vector3(float.NaN, 0, 0), Quaternion.Identity);

        Should.Throw<ArgumentException>(() => HeadLocked.Follows(lost, InFront));
    }

    [Fact]
    public void HeadLocked_ForAnOrientationThatIsNotFinite_FailsLoudlyRatherThanGuessing()
    {
        var lost = new Pose(Vector3.Zero, new Quaternion(0, float.NaN, 0, 1));

        Should.Throw<ArgumentException>(() => HeadLocked.Follows(lost, InFront));
    }

    [Fact]
    public void HeadLocked_ForAnOrientationOfNothing_FailsLoudlyRatherThanGuessing()
    {
        // A zero quaternion is not a rotation and normalising it yields NaN.
        // Loud, rather than an overlay that silently goes nowhere.
        var nothing = new Pose(Vector3.Zero, new Quaternion(0, 0, 0, 0));

        Should.Throw<ArgumentException>(() => HeadLocked.Follows(nothing, InFront));
    }

    [Fact]
    public void HeadLocked_ForAnOffsetThatIsNotFinite_FailsLoudlyRatherThanGuessing()
    {
        Should.Throw<ArgumentException>(
            () => HeadLocked.Follows(Still, new Vector3(0, float.PositiveInfinity, 0)));
    }
}
