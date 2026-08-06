using System;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// The check every placement makes before it does any arithmetic.
/// </summary>
///
/// <remarks>
/// Extracted when the third copy appeared — one in the gaze-ray anchor, one for
/// a head's position and one for its orientation. Each was the same four lines
/// with a different noun in the message, and the noun turned out not to matter:
/// what somebody reading the failure needs is which argument and what was in it,
/// and both are here.
///
/// <para>
/// It exists at all because a dropped tracking frame hands back <c>NaN</c> and
/// every arithmetic path in this library will happily carry one through to a
/// transform, where it becomes an overlay that is nowhere. That reads as a
/// defect in the overlay rather than in the pose that caused it, so it is
/// stopped at the door and named.
/// </para>
/// </remarks>
internal static class Numbers
{
    private const string Usually = "A dropped tracking frame is the usual reason.";

    /// <summary>
    /// Throws unless every part of a point or a direction is a finite number.
    /// </summary>
    /// <param name="part">What to check.</param>
    /// <param name="name">The argument it came from.</param>
    /// <exception cref="ArgumentException">Some part of it is not finite.</exception>
    internal static void MustBeFinite(Vector3 part, string name)
    {
        if (!float.IsFinite(part.X) || !float.IsFinite(part.Y) || !float.IsFinite(part.Z))
        {
            throw new ArgumentException(
                $"This has to be made of finite numbers, and it was {part}. {Usually}",
                name);
        }
    }

    /// <summary>
    /// Throws unless every part of an orientation is a finite number.
    /// </summary>
    /// <param name="turn">What to check.</param>
    /// <param name="name">The argument it came from.</param>
    /// <exception cref="ArgumentException">Some part of it is not finite.</exception>
    internal static void MustBeFinite(Quaternion turn, string name)
    {
        if (!float.IsFinite(turn.X)
            || !float.IsFinite(turn.Y)
            || !float.IsFinite(turn.Z)
            || !float.IsFinite(turn.W))
        {
            throw new ArgumentException(
                $"This has to be made of finite numbers, and it was {turn}. {Usually}",
                name);
        }
    }

    /// <summary>
    /// Throws unless an extent is a size at all.
    /// </summary>
    ///
    /// <remarks>
    /// Here rather than on <see cref="Chrome"/>, where it was written. It went
    /// with the chrome while the chrome was the only thing that measured a board;
    /// <see cref="Stretch"/> measures one too, and a size check reached for
    /// through the type that draws handles is one nobody finds.
    /// </remarks>
    /// <param name="extent">How wide or how tall.</param>
    /// <param name="name">The argument it came from.</param>
    /// <exception cref="ArgumentOutOfRangeException">It is not a size.</exception>
    internal static void MustBeASize(float extent, string name)
    {
        if (!float.IsFinite(extent) || extent <= 0)
        {
            throw new ArgumentOutOfRangeException(
                name, extent, "A board with no size is not something anybody can point at.");
        }
    }
}
