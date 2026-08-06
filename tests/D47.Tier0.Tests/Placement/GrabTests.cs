using System;
using System.Numerics;

using D47.Placement;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Placement;

/// <summary>
/// Moving the overlay by dragging the bar under it.
///
/// <para>
/// Tier 0, and it is the whole of the gesture bar the runtime: a grab is two
/// poses at the moment the trigger went down and one more every time the hand
/// moves. Where the overlay ends up is arithmetic, which is the invariant
/// [#235](https://github.com/retiring-studios/directive-47/issues/235) states and
/// the reason this is testable with no headset attached.
/// </para>
/// </summary>
public class GrabTests
{
    private const float CloseEnough = 0.0001f;

    /// <summary>
    /// The overlay where it starts: a metre and a half ahead, facing back.
    /// </summary>
    private static readonly Pose Overlay =
        new(new Vector3(0, 0, -1.5f), Quaternion.Identity);

    /// <summary>
    /// A hand somewhere off to one side of it, which is where a Commander's
    /// actually is — the arithmetic must not assume the hand is at the overlay.
    /// </summary>
    private static readonly Pose Hand =
        new(new Vector3(0.3f, -0.4f, -0.6f), Quaternion.Identity);

    [Fact]
    public void AGrab_WithTheHandUnmoved_LeavesTheOverlayExactlyWhereItWas()
    {
        var held = Grab.Started(Hand, Overlay);

        Pose still = held.Follows(Hand);

        // The one that catches an arithmetic error no other case would. Every
        // assertion below is about a difference, and a transform that is wrong
        // by a constant satisfies all of them while putting the overlay
        // somewhere else the instant it is grabbed.
        still.Position.X.ShouldBe(Overlay.Position.X, CloseEnough);
        still.Position.Y.ShouldBe(Overlay.Position.Y, CloseEnough);
        still.Position.Z.ShouldBe(Overlay.Position.Z, CloseEnough);
    }

    [Fact]
    public void Grab_OnTheBar_MovesTheOverlay()
    {
        var held = Grab.Started(Hand, Overlay);

        var moved = new Vector3(0.2f, 0.1f, -0.3f);

        Pose now = held.Follows(Hand with { Position = Hand.Position + moved });

        // The overlay goes exactly where the hand went. Not proportionally, not
        // along the laser — a grabbed thing moves with the hand holding it, and
        // anything else feels like dragging something on a string.
        Vector3 expected = Overlay.Position + moved;

        now.Position.X.ShouldBe(expected.X, CloseEnough);
        now.Position.Y.ShouldBe(expected.Y, CloseEnough);
        now.Position.Z.ShouldBe(expected.Z, CloseEnough);
    }

    [Fact]
    public void AHand_ThatTurns_SwingsTheOverlayAroundItRatherThanSpinningInPlace()
    {
        var held = Grab.Started(Hand, Overlay);

        // A quarter turn about the world's up axis, hand staying put.
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

        Pose now = held.Follows(Hand with { Orientation = quarter });

        // The overlay is held out in front of the hand, so turning the wrist
        // carries it round on that arm rather than rotating it where it stands.
        // Rotating in place is what happens if the offset is applied in the
        // world's frame instead of the hand's, and it is the difference between
        // a panel that follows your hand and one that swings away from it.
        Vector3 armAtGrab = Overlay.Position - Hand.Position;
        Vector3 expected = Hand.Position + Vector3.Transform(armAtGrab, quarter);

        now.Position.X.ShouldBe(expected.X, CloseEnough);
        now.Position.Y.ShouldBe(expected.Y, CloseEnough);
        now.Position.Z.ShouldBe(expected.Z, CloseEnough);

        // And it is turned by as much as the hand turned.
        var facing = Quaternion.Normalize(now.Orientation);

        facing.Y.ShouldBe(quarter.Y, CloseEnough);
        facing.W.ShouldBe(quarter.W, CloseEnough);
    }

    [Fact]
    public void AGrab_FollowedTwice_IsMeasuredFromWhereItStartedRatherThanFromLastTime()
    {
        var held = Grab.Started(Hand, Overlay);

        var once = new Vector3(0.1f, 0, 0);

        held.Follows(Hand with { Position = Hand.Position + once });

        Pose second = held.Follows(Hand with { Position = Hand.Position + once });

        // Asking twice about the same hand gives the same answer. A grab that
        // accumulated would send the overlay off at a speed set by the frame
        // rate, which is the bug that looks like the tracking being broken.
        second.Position.X.ShouldBe(Overlay.Position.X + once.X, CloseEnough);
    }

    [Fact]
    public void AGrab_StartedFromNonsense_SaysSoRatherThanAnsweringNonsense()
    {
        var nowhere = new Pose(new Vector3(float.NaN, 0, 0), Quaternion.Identity);

        Should.Throw<ArgumentException>(() => Grab.Started(nowhere, Overlay));
        Should.Throw<ArgumentException>(() => Grab.Started(Hand, nowhere));

        Should.Throw<ArgumentException>(() => Grab.Started(Hand, Overlay).Follows(nowhere));
    }

    [Fact]
    public void AGrab_OnAnOrientationOfNothing_IsNotARotation()
    {
        var unturned = new Pose(Vector3.Zero, new Quaternion(0, 0, 0, 0));

        Should.Throw<ArgumentException>(() => Grab.Started(unturned, Overlay));
    }
}
