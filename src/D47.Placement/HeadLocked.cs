using System;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// Placement that goes where the head goes.
/// </summary>
public static class HeadLocked
{
    /// <summary>
    /// Where the overlay sits, given where the head is and how far in front of
    /// it the overlay was put.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Position and orientation both, because following the view is both. An
    /// overlay that tracked where the head went while holding the room's
    /// orientation would be edge-on the moment the Commander turned, which is a
    /// surface that cannot be read rather than one in the wrong place.
    /// </para>
    /// <para>
    /// The offset is applied in the head's frame and then carried into the
    /// world, which is what keeps the overlay the same distance away whatever
    /// the head is doing. Composed the other way round it still moves when the
    /// head moves — so every "it followed" assertion passes — and the distance
    /// comes out wrong, which is the part a Commander would actually notice.
    /// </para>
    /// </remarks>
    /// <param name="head">Where the Commander's head is, and which way it faces.</param>
    /// <param name="inView">Where the overlay sits relative to the head.</param>
    /// <returns>The overlay's transform.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of the pose or the offset is not a finite number, or the
    /// orientation is not a rotation.
    /// </exception>
    public static Matrix4x4 Follows(Pose head, Vector3 inView)
    {
        Numbers.MustBeFinite(head.Position, nameof(head));
        Numbers.MustBeFinite(inView, nameof(inView));

        Quaternion facing = Facing(head.Orientation);

        // Rotation and translation kept apart and then put together by hand
        // rather than multiplying two matrices. The rotation has to act on the
        // offset without acting on the head's own position, and writing that as
        // a composition is where the order goes wrong quietly.
        var transform = Matrix4x4.CreateFromQuaternion(facing);

        transform.Translation = head.Position + Vector3.Transform(inView, facing);

        return transform;
    }

    /// <summary>
    /// The orientation as a rotation: finite, and unit length.
    /// </summary>
    ///
    /// <remarks>
    /// Normalised rather than required normalised. A quaternion out of a
    /// tracking runtime drifts off unit length over a session, and one that is
    /// one per cent long scales everything it touches — so the overlay creeps
    /// away from the Commander the longer they play, which reads as the overlay
    /// being wrong rather than the pose it was handed.
    /// </remarks>
    private static Quaternion Facing(Quaternion orientation)
    {
        Numbers.MustBeFinite(orientation, nameof(orientation));

        // Checked rather than left to Quaternion.Normalize, which answers NaN
        // for a quaternion of nothing instead of saying so — the same trap
        // Vector3.Normalize sets, and Anchor sidesteps it the same way.
        if (orientation.LengthSquared() == 0)
        {
            throw new ArgumentException(
                "An orientation of nothing is not a rotation.",
                nameof(orientation));
        }

        return Quaternion.Normalize(orientation);
    }
}
