using System;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// Where a surface goes when it is put where somebody is looking.
/// </summary>
public static class Anchor
{
    /// <summary>
    /// The point a given distance along a gaze ray.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Along the ray, which is the whole of it. The overlay does not go at a
    /// fixed offset from the head that happens to be in front of it — it goes
    /// where the Commander is looking, so both ends of the ray matter and the
    /// answer moves when either does.
    /// </para>
    /// <para>
    /// The direction is normalised here rather than being required normalised.
    /// A pose out of a tracking runtime is not promised to be unit length, and
    /// a direction that happened to arrive fifty units long would otherwise put
    /// the overlay fifty times too far away — a bug that looks like the
    /// arithmetic working, because the overlay would still be in the right
    /// direction.
    /// </para>
    /// </remarks>
    /// <param name="gaze">Where the Commander is looking.</param>
    /// <param name="distance">How far away to put it, in metres.</param>
    /// <returns>The anchor point.</returns>
    /// <exception cref="ArgumentException">
    /// The ray points nowhere, or some part of it is not a finite number.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="distance"/> is not greater than zero.
    /// </exception>
    public static Vector3 Along(GazeRay gaze, float distance)
    {
        // Checked before anything is done with them, and loudly. Every one of
        // these produces a number rather than an exception if it is let
        // through — NaN from normalising nothing, an anchor inside the
        // Commander's head from a zero distance, an infinity from a dropped
        // tracking frame. All three then travel into a transform and come out
        // as an overlay that is somewhere nobody can find, which reads as a
        // defect in the overlay rather than in what it was told.
        Numbers.MustBeFinite(gaze.From, nameof(gaze));
        Numbers.MustBeFinite(gaze.Towards, nameof(gaze));

        // Finite first, and it has to be first: every comparison against NaN is
        // false, so NaN is neither negative nor zero and walks straight through
        // ThrowIfNegativeOrZero into the arithmetic.
        if (!float.IsFinite(distance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(distance),
                distance,
                "A distance has to be a finite number.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(distance);

        float reach = gaze.Towards.Length();

        if (reach == 0)
        {
            throw new ArgumentException(
                "A ray that points nowhere has no along.",
                nameof(gaze));
        }

        // Divided by its own length rather than through Vector3.Normalize,
        // which answers NaN for a zero vector instead of saying so. The check
        // above is what makes that impossible here, and doing the division in
        // the open is what keeps the two facts next to each other.
        return gaze.From + (gaze.Towards / reach * distance);
    }
}
