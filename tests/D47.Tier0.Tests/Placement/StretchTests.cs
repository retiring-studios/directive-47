using System;
using System.Numerics;

using D47.Placement;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Placement;

/// <summary>
/// Scaling the overlay by dragging one of its corner handles.
///
/// <para>
/// Tier 0, for the reason
/// [#235](https://github.com/retiring-studios/directive-47/issues/235) states as
/// an invariant: where the overlay lands is arithmetic, and arithmetic is
/// asserted with no headset attached. A stretch is a corner, a hand at the
/// moment the trigger went down, and one more hand position every time it moves.
/// </para>
/// </summary>
public class StretchTests
{
    private const float CloseEnough = 0.0001f;

    /// <summary>
    /// The shipped quad's proportions, a metre and a half ahead and facing back.
    /// Half a metre by three tenths, so its corners are at
    /// (±0.25, ±0.15) from the middle.
    /// </summary>
    private static readonly Board Overlay =
        new(new Pose(new Vector3(0, 0, -1.5f), Quaternion.Identity), 0.5f, 0.3f);

    /// <summary>
    /// The corner opposite the top right one, in the world.
    /// </summary>
    private static readonly Vector3 BottomLeft = new(-0.25f, -0.15f, -1.5f);

    /// <summary>
    /// A hand on the top right corner. Distance from the pinned corner is what
    /// drives the scale, and starting exactly on the corner makes that distance
    /// the panel's own diagonal — so pulling to twice it is a factor of two with
    /// nothing to round.
    /// </summary>
    private static readonly Vector3 OnTheCorner = new(0.25f, 0.15f, -1.5f);

    /// <summary>
    /// A hand off the panel entirely, which is where a Commander's actually is —
    /// out in front and above, reaching. Clear of all four corners, so it can be
    /// used whichever one is being pulled.
    /// </summary>
    private static readonly Vector3 Hand = new(0.4f, 0.35f, -1.1f);

    [Fact]
    public void AStretch_WithTheHandUnmoved_LeavesTheOverlayExactlyAsItWas()
    {
        var held = Stretch.Started(Grabbed.TopRightCorner, OnTheCorner, Overlay);

        Board still = held.Follows(OnTheCorner);

        // The case that catches an arithmetic error no other one here would.
        // Everything below is about a ratio or a difference, and a factor that is
        // wrong by a constant satisfies all of them while jumping the overlay to
        // another size the instant it is grabbed.
        still.Width.ShouldBe(Overlay.Width, CloseEnough);
        still.Height.ShouldBe(Overlay.Height, CloseEnough);

        still.Where.Position.X.ShouldBe(Overlay.Where.Position.X, CloseEnough);
        still.Where.Position.Y.ShouldBe(Overlay.Where.Position.Y, CloseEnough);
        still.Where.Position.Z.ShouldBe(Overlay.Where.Position.Z, CloseEnough);
    }

    [Fact]
    public void Grab_OnACorner_ScalesTheOverlay()
    {
        var held = Stretch.Started(Grabbed.TopRightCorner, OnTheCorner, Overlay);

        // Straight out along the diagonal, to twice the distance from the corner
        // that stays put.
        Vector3 twiceAsFar = BottomLeft + ((OnTheCorner - BottomLeft) * 2);

        Board now = held.Follows(twiceAsFar);

        now.Width.ShouldBe(1.0f, CloseEnough);
        now.Height.ShouldBe(0.6f, CloseEnough);
    }

    [Fact]
    public void ACornerPulledAlongItsDiagonal_StaysUnderTheHand()
    {
        var held = Stretch.Started(Grabbed.TopRightCorner, OnTheCorner, Overlay);

        Vector3 twiceAsFar = BottomLeft + ((OnTheCorner - BottomLeft) * 2);

        Board now = held.Follows(twiceAsFar);

        // The handle the Commander took hold of is still where their hand is.
        // Nothing else in this file asserts the position and the size together,
        // and a scale about the wrong point satisfies every one of them
        // separately while sliding the handle out from under the laser.
        Vector3 corner = now.Where.Position + new Vector3(now.Width / 2, now.Height / 2, 0);

        corner.X.ShouldBe(twiceAsFar.X, CloseEnough);
        corner.Y.ShouldBe(twiceAsFar.Y, CloseEnough);
        corner.Z.ShouldBe(twiceAsFar.Z, CloseEnough);
    }

    [Theory]
    [InlineData(Grabbed.TopRightCorner, -0.25f, -0.15f)]
    [InlineData(Grabbed.TopLeftCorner, 0.25f, -0.15f)]
    [InlineData(Grabbed.BottomRightCorner, -0.25f, 0.15f)]
    [InlineData(Grabbed.BottomLeftCorner, 0.25f, 0.15f)]
    public void AStretch_ByAnyCorner_LeavesTheOppositeOneWhereItWas(
        Grabbed corner, float acrossOfPinned, float upOfPinned)
    {
        var pinned = new Vector3(acrossOfPinned, upOfPinned, -1.5f);

        var held = Stretch.Started(corner, Hand, Overlay);

        Board now = held.Follows(Hand + new Vector3(0.4f, 0.1f, -0.2f));

        // Where the pinned corner is once the overlay has been resized. It is
        // read back out of the new board rather than assumed, because the whole
        // claim is that this one point did not move.
        Vector3 after = now.Where.Position + new Vector3(
            MathF.CopySign(now.Width / 2, acrossOfPinned),
            MathF.CopySign(now.Height / 2, upOfPinned),
            0);

        after.X.ShouldBe(pinned.X, CloseEnough);
        after.Y.ShouldBe(pinned.Y, CloseEnough);
        after.Z.ShouldBe(pinned.Z, CloseEnough);
    }

    [Fact]
    public void AStretch_KeepsTheOverlaysProportions()
    {
        var held = Stretch.Started(Grabbed.BottomLeftCorner, Hand, Overlay);

        Board now = held.Follows(Hand + new Vector3(0.7f, -0.2f, 0.3f));

        // #235's invariant: scaling scales, it does not reflow. One factor drives
        // both extents, so the contents are never laid out again.
        (now.Width / now.Height).ShouldBe(Overlay.Width / Overlay.Height, CloseEnough);
    }

    [Fact]
    public void AHandMovedSideways_ScalesByHowFarItIsRatherThanByWhichWayItWent()
    {
        var held = Stretch.Started(Grabbed.TopRightCorner, OnTheCorner, Overlay);

        // Same distance from the pinned corner, a completely different direction.
        Vector3 reach = OnTheCorner - BottomLeft;

        Vector3 elsewhere = BottomLeft + new Vector3(0, 0, reach.Length());

        Board now = held.Follows(elsewhere);

        // A ratio of distances, not a drag projected onto the panel's plane. Pull
        // as far as you started and the overlay is the size it started, whichever
        // way your hand went to get there.
        now.Width.ShouldBe(Overlay.Width, CloseEnough);
        now.Height.ShouldBe(Overlay.Height, CloseEnough);
    }

    [Fact]
    public void AStretch_PulledFurtherThanTheOverlayGoes_StopsAtTwoMetres()
    {
        var held = Stretch.Started(Grabbed.TopRightCorner, OnTheCorner, Overlay);

        Vector3 milesAway = BottomLeft + ((OnTheCorner - BottomLeft) * 40);

        Board now = held.Follows(milesAway);

        now.Width.ShouldBe(2f, CloseEnough);

        // And the proportions survive the limit. Clamping a width without its
        // height is how a ceiling turns into a reflow.
        now.Height.ShouldBe(1.2f, CloseEnough);
    }

    [Fact]
    public void AStretch_PulledInPastTheAnchor_StopsAtAnEighthOfAMetre()
    {
        var held = Stretch.Started(Grabbed.TopRightCorner, OnTheCorner, Overlay);

        // Right up against the corner that is holding still, which without a
        // floor is an overlay of no size — and a Board of no size is what
        // Chrome refuses to measure at all.
        Board now = held.Follows(BottomLeft);

        now.Width.ShouldBe(0.125f, CloseEnough);
        now.Height.ShouldBe(0.075f, CloseEnough);
    }

    [Fact]
    public void AStretch_HeldAtALimit_StillLeavesTheOppositeCornerWhereItWas()
    {
        var held = Stretch.Started(Grabbed.TopRightCorner, OnTheCorner, Overlay);

        Board now = held.Follows(BottomLeft + ((OnTheCorner - BottomLeft) * 40));

        // The pinned corner is pinned whether or not the size was allowed. A
        // clamp applied to the extents and not to the position would slide the
        // overlay off its anchor exactly when it stopped growing.
        Vector3 after = now.Where.Position - new Vector3(now.Width / 2, now.Height / 2, 0);

        after.X.ShouldBe(BottomLeft.X, CloseEnough);
        after.Y.ShouldBe(BottomLeft.Y, CloseEnough);
    }

    [Fact]
    public void AStretch_FollowedTwice_IsMeasuredFromWhereItStartedRatherThanFromLastTime()
    {
        var held = Stretch.Started(Grabbed.TopRightCorner, OnTheCorner, Overlay);

        Vector3 twiceAsFar = BottomLeft + ((OnTheCorner - BottomLeft) * 2);

        held.Follows(twiceAsFar);

        Board second = held.Follows(twiceAsFar);

        // Asking twice about the same hand gives the same answer. One that
        // accumulated would grow the overlay at a speed set by how often the
        // watching loop runs.
        second.Width.ShouldBe(1.0f, CloseEnough);
    }

    [Fact]
    public void AStretch_TurnedWithThePanel_PinsTheCornerTheCommanderCanSee()
    {
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

        Board rolled = Overlay with
        {
            Where = Overlay.Where with { Orientation = quarter },
        };

        // The top right corner in the panel's own frame, which a quarter turn has
        // put somewhere else entirely in the world.
        Vector3 pinned = rolled.Where.Position
            + Vector3.Transform(new Vector3(-0.25f, -0.15f, 0), quarter);

        Vector3 onTheCorner = rolled.Where.Position
            + Vector3.Transform(new Vector3(0.25f, 0.15f, 0), quarter);

        var held = Stretch.Started(Grabbed.TopRightCorner, onTheCorner, rolled);

        Board now = held.Follows(pinned + ((onTheCorner - pinned) * 2));

        Vector3 after = now.Where.Position
            + Vector3.Transform(new Vector3(-now.Width / 2, -now.Height / 2, 0), quarter);

        // Corners are the panel's, not the world's. A stretch that read them off
        // the world axes would pin a point in mid-air beside a panel that is not
        // square to the cockpit — and the shipped one never is.
        after.X.ShouldBe(pinned.X, CloseEnough);
        after.Y.ShouldBe(pinned.Y, CloseEnough);
        after.Z.ShouldBe(pinned.Z, CloseEnough);

        now.Where.Orientation.ShouldBe(quarter);
    }

    [Theory]
    [InlineData(Grabbed.Nothing)]
    [InlineData(Grabbed.Content)]
    [InlineData(Grabbed.Bar)]
    public void AStretch_BySomethingThatIsNotACorner_SaysSo(Grabbed notACorner)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => Stretch.Started(notACorner, Hand, Overlay));
    }

    [Fact]
    public void AStretch_StartedFromNonsense_SaysSoRatherThanAnsweringNonsense()
    {
        var nowhere = new Vector3(float.NaN, 0, 0);

        Should.Throw<ArgumentException>(
            () => Stretch.Started(Grabbed.TopRightCorner, nowhere, Overlay));

        Should.Throw<ArgumentException>(
            () => Stretch.Started(Grabbed.TopRightCorner, Hand, Overlay).Follows(nowhere));
    }

    [Fact]
    public void AStretch_OnABoardWithNoSize_SaysSo()
    {
        Board flat = Overlay with { Height = 0 };

        Should.Throw<ArgumentOutOfRangeException>(
            () => Stretch.Started(Grabbed.TopRightCorner, Hand, flat));
    }

    [Fact]
    public void AStretch_StartedWithTheHandOnThePinnedCorner_SaysSo()
    {
        // Nothing to measure from: every later hand position would be some
        // multiple of nought away, so the first twitch would slam the overlay
        // into whichever limit it was heading for. Refusing to start is a grab
        // that does nothing, which is what a Commander with their hand inside the
        // anchor expects anyway.
        Should.Throw<ArgumentException>(
            () => Stretch.Started(Grabbed.TopRightCorner, BottomLeft, Overlay));
    }
}
