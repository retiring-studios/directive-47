namespace D47.Placement;

/// <summary>
/// The overlay as something with edges: where it is, and how big.
/// </summary>
///
/// <remarks>
/// <para>
/// A <see cref="Pose"/> says where a thing is and which way it faces, which is
/// everything the overlay needed while nobody could point at it. Pointing needs
/// to know where it stops, and a width and a height passed beside a pose are two
/// numbers that can be swapped, half-defaulted, or disagreed about — the same
/// argument <c>Combination</c> makes for keeping modifiers and a key together.
/// </para>
/// <para>
/// Called a board rather than a panel or a quad because both of those already
/// name something else in this solution — <c>D47.Panel</c> is the application
/// and <c>D47.VrOverlay.Quad</c> is the transform handed to the runtime — and a
/// type that has to be spelled out in full every time it is used is one nobody
/// enjoys reading.
/// </para>
/// <para>
/// Facing along its own positive Z, which is where OpenVR points an overlay
/// quad, and measured from the middle out.
/// </para>
/// </remarks>
/// <param name="Where">Where it is, and which way it faces.</param>
/// <param name="Width">How wide it is, in metres.</param>
/// <param name="Height">How tall it is, in metres.</param>
public readonly record struct Board(Pose Where, float Width, float Height);

/// <summary>
/// What pulling the trigger right now would take hold of.
/// </summary>
public enum Grabbed
{
    /// <summary>
    /// Nothing. The controller is not aimed at the overlay, and the trigger does
    /// nothing.
    /// </summary>
    Nothing,

    /// <summary>
    /// The middle of it, which moves.
    /// </summary>
    Body,

    /// <summary>
    /// The left-hand edge, which scales.
    /// </summary>
    LeftEdge,

    /// <summary>
    /// The right-hand edge, which scales.
    /// </summary>
    RightEdge,

    /// <summary>
    /// The top edge, which scales.
    /// </summary>
    TopEdge,

    /// <summary>
    /// The bottom edge, which scales.
    /// </summary>
    BottomEdge,
}
