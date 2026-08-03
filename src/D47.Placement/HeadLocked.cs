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

        Quaternion facing = Turn.Of(head.Orientation);

        // Rotation and translation kept apart and then put together by hand
        // rather than multiplying two matrices. The rotation has to act on the
        // offset without acting on the head's own position, and writing that as
        // a composition is where the order goes wrong quietly.
        var transform = Matrix4x4.CreateFromQuaternion(facing);

        transform.Translation = head.Position + Vector3.Transform(inView, facing);

        return transform;
    }

    /// <summary>
    /// The offset that keeps a world-locked overlay exactly where it is, so
    /// that changing mode moves nothing.
    /// </summary>
    ///
    /// <remarks>
    /// The inverse of what <see cref="Follows"/> does, and deliberately spelled
    /// as one: take the gap between the overlay and the head in the world, and
    /// turn it back into the head's own frame.
    ///
    /// <para>
    /// The conjugate rather than <c>Quaternion.Inverse</c>, which divides by the
    /// length squared and so does the normalising twice. For a rotation the two
    /// are the same answer, and the conjugate says which fact is being used.
    /// </para>
    /// <para>
    /// Turning the gap the wrong way round is the mistake this invites, and it
    /// is invisible to a Commander looking straight ahead: with the head at the
    /// identity the conjugate and the rotation are the same thing. That is why
    /// the switching tests run over three head poses rather than one.
    /// </para>
    /// </remarks>
    /// <param name="head">Where the Commander's head is now.</param>
    /// <param name="placed">Where the overlay is bolted.</param>
    /// <returns>Where it sits relative to the head.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of either pose is not a finite number, or the head's
    /// orientation is not a rotation.
    /// </exception>
    public static Vector3 TakingOver(Pose head, Pose placed)
    {
        Numbers.MustBeFinite(head.Position, nameof(head));
        Numbers.MustBeFinite(placed.Position, nameof(placed));

        Quaternion facing = Turn.Of(head.Orientation);

        return Vector3.Transform(
            placed.Position - head.Position,
            Quaternion.Conjugate(facing));
    }
}
