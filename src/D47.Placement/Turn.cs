using System;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// An orientation, taken as a rotation.
/// </summary>
///
/// <remarks>
/// The second copy of this appeared with world-locked placement, which needs
/// exactly what head-locked needed: finite, not nothing, and unit length.
///
/// <para>
/// Normalised rather than required normalised. A quaternion out of a tracking
/// runtime drifts off unit length over a session, and one that is one per cent
/// long scales everything it touches — so the overlay creeps away from the
/// Commander the longer they play, which reads as the overlay being wrong
/// rather than the pose it was handed.
/// </para>
/// </remarks>
internal static class Turn
{
    /// <summary>
    /// The orientation as a rotation.
    /// </summary>
    /// <param name="orientation">What to take.</param>
    /// <returns>The same rotation, at unit length.</returns>
    /// <exception cref="ArgumentException">
    /// It is not finite, or it is not a rotation at all.
    /// </exception>
    internal static Quaternion Of(Quaternion orientation)
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
