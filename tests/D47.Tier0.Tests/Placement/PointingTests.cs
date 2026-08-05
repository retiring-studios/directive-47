using System;
using System.Numerics;

using D47.Placement;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Placement;

/// <summary>
/// What a controller is pointing at: the overlay's body, one of its edges, or
/// nothing at all.
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
/// own negative Z, and an overlay quad faces along its own positive Z — so a
/// controller at positive Z with no rotation, aimed at a board at the origin
/// with no rotation, is somebody pointing straight at it.
/// </para>
/// </summary>
public class PointingTests
{
    /// <summary>
    /// Half a metre wide and a little over half as tall — the quad's shipped
    /// width, at roughly the render's proportions.
    /// </summary>
    private static readonly Board Facing =
        new(new Pose(Vector3.Zero, Quaternion.Identity), 0.5f, 0.3f);

    [Fact]
    public void Pointing_AtTheMiddle_IsTheBody()
    {
        Pointing.At(From(0, 0), Facing).ShouldBe(Grabbed.Body);
    }

    [Theory]
    [InlineData(-0.24f, 0f, Grabbed.LeftEdge)]
    [InlineData(0.24f, 0f, Grabbed.RightEdge)]
    [InlineData(0f, 0.14f, Grabbed.TopEdge)]
    [InlineData(0f, -0.14f, Grabbed.BottomEdge)]
    public void Pointing_NearABoundary_IsThatEdge(float across, float up, Grabbed expected)
    {
        Pointing.At(From(across, up), Facing).ShouldBe(expected);
    }

    [Theory]
    [InlineData(-0.1f, 0f)]
    [InlineData(0.1f, 0f)]
    [InlineData(0f, 0.05f)]
    [InlineData(0f, -0.05f)]
    public void Pointing_WellInsideTheBoundary_IsStillTheBody(float across, float up)
    {
        Pointing.At(From(across, up), Facing).ShouldBe(Grabbed.Body);
    }

    [Theory]
    [InlineData(-0.3f, 0f)]
    [InlineData(0.3f, 0f)]
    [InlineData(0f, 0.2f)]
    [InlineData(0f, -0.2f)]
    public void Pointing_PastTheBoundary_IsNothing(float across, float up)
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

        // A board turned a quarter turn about up now faces along positive X, so
        // the controller has to be out there to be pointing at it.
        //
        // The same rotation as the board, which reads wrong and is not: a board
        // faces along its own positive Z and a controller points along its own
        // negative Z, so two things with the same orientation are aimed at each
        // other. The square-on case has exactly that shape and hides it, because
        // there both rotations are the identity.
        var pointing = new Pose(
            new Vector3(1, 0, 0),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));

        Pointing.At(pointing, turned).ShouldBe(Grabbed.Body);
    }

    [Fact]
    public void Pointing_AtACorner_IsWhicheverEdgeItIsFurthestAlong()
    {
        // Both axes in their edge zones at once, which is what makes this a
        // corner — an earlier version of this test had one axis comfortably in
        // the body both times and never reached the decision it was named for.
        //
        // Scaling keeps the contents' proportions whichever edge is taken, so
        // this only has to be decided and then stay decided. Proportionally
        // furthest out is the one a Commander was most likely reaching for.
        Pointing.At(From(0.24f, 0.13f), Facing).ShouldBe(Grabbed.RightEdge);
        Pointing.At(From(0.21f, 0.149f), Facing).ShouldBe(Grabbed.TopEdge);
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
        var dropped = new Pose(
            new Vector3(0, 0, 1), new Quaternion(float.NaN, 0, 0, 1));

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
