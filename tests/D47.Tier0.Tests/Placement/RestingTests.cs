using System;
using System.Numerics;

using D47.Placement;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Placement;

/// <summary>
/// Where the overlay sits before anybody has moved it.
///
/// <para>
/// Below the line of sight, because it was in it. Directly ahead at eye level is
/// where a Commander looks to fly, and a panel parked there is one that has to be
/// turned off to see out of — which is the same failure as an overlay that cannot
/// be moved, arrived at from the other direction.
/// </para>
/// </summary>
public class RestingTests
{
    /// <summary>
    /// How far out the arithmetic is allowed to be. Single-precision
    /// trigonometry over a metre and a half, so the last place is noise.
    /// </summary>
    private const float Rounding = 0.001f;

    [Fact]
    public void Resting_IsBelowTheLineOfSight()
    {
        // The whole of the change. Everything else here is what must not have
        // moved while this did.
        Resting.Place.Position.Y.ShouldBeLessThan(
            0,
            "straight ahead at eye level is where the Commander looks to fly");
    }

    [Fact]
    public void Resting_IsStillStraightAheadAcross()
    {
        // Down, not down and to one side. Which shoulder it would sit over is a
        // question nobody has asked, and answering it here would be choosing a
        // handedness for every Commander.
        Resting.Place.Position.X.ShouldBe(0, Rounding);
    }

    [Fact]
    public void Resting_IsNoFurtherAwayThanItWas()
    {
        // Lowering it must not also push it away or drag it closer. The distance
        // is what the quad's half-metre width was chosen against, so moving it
        // changes how big the render reads for a reason nobody intended.
        Resting.Place.Position.Length().ShouldBe(1.5f, Rounding);
    }

    [Fact]
    public void Resting_IsAsFarDownAsItSays()
    {
        // Read back as an angle rather than as coordinates, because the angle is
        // the thing that was decided and the coordinates are what it works out
        // to. A test asserting the coordinates would have to be edited to agree
        // with any change rather than disagreeing with a wrong one.
        Vector3 where = Resting.Place.Position;

        float belowTheLevel = float.RadiansToDegrees(
            MathF.Atan2(-where.Y, -where.Z));

        belowTheLevel.ShouldBe(30f, 0.1f);
    }

    [Fact]
    public void Resting_IsSquareToTheWorldUntilSomethingTiltsIt()
    {
        // Not tilted to face the head, which is worth wanting and is not built.
        // Asserted so that building it is a deliberate change to a stated fact
        // rather than something that quietly stops being true.
        Resting.Place.Orientation.ShouldBe(Quaternion.Identity);
    }
}
