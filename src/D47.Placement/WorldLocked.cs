using System.Numerics;

namespace D47.Placement;

/// <summary>
/// Placement that stays where it was put, whatever the head does.
/// </summary>
public static class WorldLocked
{
    /// <summary>
    /// Where the overlay sits, given where it was bolted.
    /// </summary>
    ///
    /// <remarks>
    /// No head anywhere in this, which is the whole difference between the two
    /// modes and the reason it is one line. What is worth checking is not that
    /// this ignores the head — its signature says so — but that a placement
    /// handed over from head-locked then stops following, and that is asserted
    /// against <see cref="HeadLocked.Follows"/> rather than against this alone.
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

    /// <summary>
    /// The world pose that keeps a head-locked overlay exactly where it is, so
    /// that changing mode moves nothing.
    /// </summary>
    ///
    /// <remarks>
    /// Asked of the head-locked transform rather than worked out again from the
    /// offset. The two would have to agree exactly for the handover to be
    /// invisible, and the surest way to make two calculations agree is for
    /// there to be one of them.
    /// </remarks>
    /// <param name="head">Where the Commander's head is now.</param>
    /// <param name="inView">Where the overlay sits relative to the head.</param>
    /// <returns>Where to bolt it.</returns>
    /// <exception cref="System.ArgumentException">
    /// Some part of the pose or the offset is not a finite number, or the
    /// orientation is not a rotation.
    /// </exception>
    public static Pose TakingOver(Pose head, Vector3 inView)
    {
        Matrix4x4 following = HeadLocked.Follows(head, inView);

        return new Pose(
            following.Translation,
            Quaternion.CreateFromRotationMatrix(following));
    }
}
