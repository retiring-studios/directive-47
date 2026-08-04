using System;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// Where the overlay sits before anybody has moved it.
/// </summary>
///
/// <remarks>
/// <para>
/// Here rather than in <c>D47.VrOverlay</c>, which held it and said so: that
/// project is an adapter, and a default position is a placement decision. It was
/// twelve frozen floats in OpenVR's layout before it was a <c>Pose</c>, and a
/// <c>Pose</c> in the adapter before it was here.
/// </para>
/// <para>
/// It stops being the answer once
/// [#141](https://github.com/retiring-studios/directive-47/issues/141) remembers
/// where the Commander put it and
/// [#140](https://github.com/retiring-studios/directive-47/issues/140) gives
/// them a way to put it anywhere. Until then it is the only position there is,
/// which is why it is worth getting somewhere livable rather than somewhere
/// merely arithmetically tidy.
/// </para>
/// </remarks>
public static class Resting
{
    /// <summary>
    /// How far away it sits, in metres.
    ///
    /// <para>
    /// Unchanged while the angle moved, on purpose. The quad's half-metre width
    /// was chosen against this distance, so changing it changes how large the
    /// render reads for a reason nobody asked for.
    /// </para>
    /// </summary>
    private const float AtArmsLength = 1.5f;

    /// <summary>
    /// How far below the line of sight it sits, in degrees.
    ///
    /// <para>
    /// Sixty, which is a glance down at a console rather than a panel in the
    /// way. Straight ahead is where a Commander looks to fly, and an overlay
    /// parked there is one that has to be turned off to see out of — the same
    /// failure as an overlay that cannot be moved, reached from the other side.
    /// </para>
    /// <para>
    /// A number to react to rather than a measured one, in the same spirit as
    /// the quad's width and the game overlay's opening opacity. This is the one
    /// value to change if it is wrong, and it is expected to move once somebody
    /// has worn it.
    /// </para>
    /// </summary>
    private const float BelowTheLevel = 60f;

    /// <summary>
    /// The resting place: straight ahead across, below the line of sight, square
    /// to the world.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Straight ahead across rather than over one shoulder. Which shoulder is a
    /// question nobody has asked, and answering it here would be choosing a
    /// handedness on every Commander's behalf.
    /// </para>
    /// <para>
    /// Square to the world rather than tilted to face the head. Tilting is worth
    /// wanting — a panel this far down is read at an angle — and it is not built.
    /// </para>
    /// </remarks>
    public static Pose Place { get; } = new(Below(BelowTheLevel, AtArmsLength), Quaternion.Identity);

    /// <summary>
    /// A point a given angle below the line of sight, at a given distance.
    /// </summary>
    ///
    /// <remarks>
    /// Worked out from the angle rather than written as coordinates, so that the
    /// decision and the arithmetic are not two things that can disagree. OpenVR's
    /// seated space is X right, Y up, Z back — so forward is negative Z, and down
    /// is negative Y.
    /// </remarks>
    /// <param name="degrees">How far below the level of the eyes.</param>
    /// <param name="metres">How far away.</param>
    /// <returns>The point.</returns>
    private static Vector3 Below(float degrees, float metres)
    {
        float radians = float.DegreesToRadians(degrees);

        return new Vector3(0, -metres * MathF.Sin(radians), -metres * MathF.Cos(radians));
    }
}
