using System;
using System.Numerics;

using D47.Placement;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Placement;

/// <summary>
/// What a controller is pointing at: the panel itself, the bar beneath it that
/// moves it, one of the four corners that scale it, or nothing at all.
///
/// <para>
/// A pose, a ray and a rectangle — the same arithmetic as
/// <see cref="Anchor.Along"/> and Tier 0 for the same reason. Whether a real
/// controller is read at all needs SteamVR and is asserted in
/// <c>D47.Tier2.Tests</c>; whether the answer is right needs nothing but
/// numbers.
/// </para>
///
/// <para>
/// OpenVR's seated space is X right, Y up, Z back. A controller points along its
/// own negative Z and an overlay quad faces along its own positive Z — so two
/// things with the same orientation are aimed at each other, and a controller at
/// positive Z with no rotation is pointing straight at a board at the origin
/// with no rotation.
/// </para>
/// </summary>
public class PointingTests
{
    /// <summary>
    /// Half a metre wide and a little over half as tall — the quad's shipped
    /// width at roughly the render's proportions.
    ///
    /// <para>
    /// So the content runs to 0.25 either side and 0.15 above and below. The bar
    /// hangs from -0.15 down to -0.195, and each corner handle is a six
    /// centimetre square straddling a content corner: the top right one covers x
    /// from 0.22 to 0.28 and y from 0.12 to 0.18.
    /// </para>
    /// </summary>
    private static readonly Board Facing =
        new(new Pose(Vector3.Zero, Quaternion.Identity), 0.5f, 0.3f);

    [Fact]
    public void Pointing_AtTheMiddle_IsTheContent()
    {
        // The panel itself grabs nothing. Moving is the bar's job and scaling is
        // a corner's, which is what makes a Commander able to point at what the
        // overlay is showing without moving it by accident.
        Pointing.At(From(0, 0), Facing).ShouldBe(Grabbed.Content);
    }

    [Theory]
    [InlineData(0.2f, 0f)]
    [InlineData(-0.2f, 0f)]
    [InlineData(0f, 0.1f)]
    [InlineData(0f, -0.1f)]
    public void Pointing_AnywhereOnThePanel_IsStillTheContent(float across, float up)
    {
        Pointing.At(From(across, up), Facing).ShouldBe(Grabbed.Content);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.15f)]
    [InlineData(-0.15f)]
    public void Pointing_AtTheBarBeneathIt_IsTheBar(float across)
    {
        Pointing.At(From(across, -0.17f), Facing).ShouldBe(Grabbed.Bar);
    }

    [Fact]
    public void TheBar_IsBeneathThePanel_AndNotAboveIt()
    {
        // The same distance above the content is off the top and grabs nothing.
        // Without this the bar is a band either side of the panel and the
        // arithmetic would not say which.
        Pointing.At(From(0, 0.17f), Facing).ShouldBe(Grabbed.Nothing);
    }

    [Theory]
    [InlineData(-0.25f, 0.15f, Grabbed.TopLeftCorner)]
    [InlineData(0.25f, 0.15f, Grabbed.TopRightCorner)]
    [InlineData(-0.25f, -0.15f, Grabbed.BottomLeftCorner)]
    [InlineData(0.25f, -0.15f, Grabbed.BottomRightCorner)]
    public void Pointing_AtACorner_IsThatCorner(float across, float up, Grabbed expected)
    {
        Pointing.At(From(across, up), Facing).ShouldBe(expected);
    }

    [Fact]
    public void ACorner_BeatsTheContentAndTheBar_WhereTheyOverlap()
    {
        // A handle straddles its corner, so half of it is over the panel and the
        // bottom pair reach into the bar. The more specific target has to win or
        // the corners are unreachable from three sides.
        Pointing.At(From(0.235f, 0.135f), Facing).ShouldBe(Grabbed.TopRightCorner);
        Pointing.At(From(0.235f, -0.165f), Facing).ShouldBe(Grabbed.BottomRightCorner);
    }

    [Theory]
    [InlineData(0.4f, 0f)]
    [InlineData(-0.4f, 0f)]
    [InlineData(0f, 0.25f)]
    [InlineData(0f, -0.25f)]
    public void Pointing_PastAllOfIt_IsNothing(float across, float up)
    {
        // Aiming at nothing is a first-class answer, not a failure. A Commander
        // waving a controller around the cockpit is pointing at nothing most of
        // the time, and that has to be cheap and quiet.
        Pointing.At(From(across, up), Facing).ShouldBe(Grabbed.Nothing);
    }

    [Fact]
    public void Pointing_AwayFromIt_IsNothing()
    {
        // Behind the controller is not in front of it. Without this the ray is a
        // line rather than a ray, and a Commander pointing directly away from
        // the overlay would be told they were holding it.
        var backwards = new Pose(
            new Vector3(0, 0, 1),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI));

        Pointing.At(backwards, Facing).ShouldBe(Grabbed.Nothing);
    }

    [Fact]
    public void Pointing_AlongTheBoardsOwnPlane_IsNothing()
    {
        // A ray parallel to the plane never meets it, and the arithmetic that
        // finds where it does divides by zero to say so.
        var alongside = new Pose(
            new Vector3(0, 0, 1),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));

        Pointing.At(alongside, Facing).ShouldBe(Grabbed.Nothing);
    }

    [Fact]
    public void Pointing_AtABoardThatHasBeenTurned_FollowsTheBoard()
    {
        // The one that catches a transform applied the wrong way round. While
        // the board is square to the world an inverted rotation is invisible —
        // exactly how a frozen identity matrix hid itself in the adapter for as
        // long as it did.
        var turned = new Board(
            new Pose(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2)),
            0.5f,
            0.3f);

        // The same rotation as the board, which reads wrong and is not: a board
        // faces along its own positive Z and a controller points along its own
        // negative Z, so two things with the same orientation are aimed at each
        // other. The square-on case has that shape too and hides it, because
        // there both rotations are the identity.
        var pointing = new Pose(
            new Vector3(1, 0, 0),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));

        Pointing.At(pointing, turned).ShouldBe(Grabbed.Content);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Pointing_FromAPoseThatIsNotNumbers_SaysSo(float broken)
    {
        // A dropped tracking frame, stopped at the door and named — the same
        // line every other placement in this library draws.
        var dropped = new Pose(new Vector3(broken, 0, 1), Quaternion.Identity);

        Should.Throw<ArgumentException>(() => Pointing.At(dropped, Facing));
    }

    [Fact]
    public void Pointing_WithAnOrientationThatIsNotNumbers_SaysSo()
    {
        var dropped = new Pose(new Vector3(0, 0, 1), new Quaternion(float.NaN, 0, 0, 1));

        Should.Throw<ArgumentException>(() => Pointing.At(dropped, Facing));
    }

    [Fact]
    public void Pointing_AtABoardWhosePoseIsNotNumbers_SaysSo()
    {
        // The board's pose comes out of the same tracking runtime the
        // controller's does, by way of wherever it was last put, so it is no
        // more trustworthy.
        var nowhere = new Board(
            new Pose(new Vector3(0, float.NaN, 0), Quaternion.Identity), 0.5f, 0.3f);

        Should.Throw<ArgumentException>(() => Pointing.At(From(0, 0), nowhere));
    }

    [Fact]
    public void Pointing_AtABoardWhoseOrientationIsNotNumbers_SaysSo()
    {
        var nowhere = new Board(
            new Pose(Vector3.Zero, new Quaternion(0, float.PositiveInfinity, 0, 1)), 0.5f, 0.3f);

        Should.Throw<ArgumentException>(() => Pointing.At(From(0, 0), nowhere));
    }

    [Fact]
    public void Pointing_AtABoardWithNoOrientationAtAll_SaysSo()
    {
        // A quaternion of nothing is not a rotation, and normalising it answers
        // NaN rather than saying so — the trap Turn.Of exists to close.
        var nothing = new Board(
            new Pose(Vector3.Zero, new Quaternion(0, 0, 0, 0)), 0.5f, 0.3f);

        Should.Throw<ArgumentException>(() => Pointing.At(From(0, 0), nothing));
    }

    [Fact]
    public void Pointing_FromAControllerWithNoOrientationAtAll_SaysSo()
    {
        var nothing = new Pose(new Vector3(0, 0, 1), new Quaternion(0, 0, 0, 0));

        Should.Throw<ArgumentException>(() => Pointing.At(nothing, Facing));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    public void ABoard_WithNoWidth_IsNotABoard(float width)
    {
        var nothing = new Board(new Pose(Vector3.Zero, Quaternion.Identity), width, 0.3f);

        Should.Throw<ArgumentOutOfRangeException>(() => Pointing.At(From(0, 0), nothing));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.3f)]
    [InlineData(float.NaN)]
    public void ABoard_WithNoHeight_IsNotABoard(float height)
    {
        // Separately from the width, because one check covering both is one
        // check that can be written to look at the same number twice.
        var nothing = new Board(new Pose(Vector3.Zero, Quaternion.Identity), 0.5f, height);

        Should.Throw<ArgumentOutOfRangeException>(() => Pointing.At(From(0, 0), nothing));
    }

    /// <summary>
    /// A controller a metre in front of the board, offset across and up, aimed
    /// straight at it.
    /// </summary>
    private static Pose From(float across, float up) =>
        new(new Vector3(across, up, 1), Quaternion.Identity);
}
