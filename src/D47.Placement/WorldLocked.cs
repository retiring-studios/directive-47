using System.Numerics;

namespace D47.Placement;

/// <summary>
/// Placement. The overlay stays where it was put, whatever the head does —
/// part of the ship rather than of the visor.
///
/// <para>
/// The only mode there is. Head-locked was built and taken out again, so this
/// is no longer one of two things a Commander chooses between; it is what
/// placement means.
/// </para>
/// </summary>
public static class WorldLocked
{
    /// <summary>
    /// Where the overlay sits, given where it was bolted.
    /// </summary>
    ///
    /// <remarks>
    /// No head anywhere in this, and nothing left to compare it against. While
    /// there were two modes, "world-locked does not follow the view" was worth
    /// asserting, because a placement could arrive from head-locked and had to
    /// stop following. With one mode, ignoring the head is simply what this is,
    /// and the signature is the whole of the claim.
    /// </remarks>
    /// <param name="placed">Where it was bolted.</param>
    /// <returns>The overlay's transform.</returns>
    /// <exception cref="System.ArgumentException">
    /// Some part of the pose is not a finite number, or the orientation is not
    /// a rotation.
    /// </exception>
    public static Matrix4x4 At(Pose placed)
    {
        Numbers.MustBeFinite(placed.Position, nameof(placed));

        var transform = Matrix4x4.CreateFromQuaternion(Turn.Of(placed.Orientation));

        transform.Translation = placed.Position;

        return transform;
    }
}
