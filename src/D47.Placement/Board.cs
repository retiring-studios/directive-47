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
///
/// <remarks>
/// A bar to move and corners to scale, which is how Horizon OS does it and is
/// the maintainer's decision of 5 August 2026 — recorded on
/// [#235](https://github.com/retiring-studios/directive-47/issues/235), which
/// had asked for the body to move and the edges to scale.
///
/// <para>
/// The chrome that offers these sits outside the content rather than on top of
/// it, and that is what makes the choice more than a preference:
/// <c>PanelRender</c>'s invariant says nothing VR-specific may be painted into
/// the shared render, so an affordance drawn over the panel had nowhere to live.
/// </para>
/// </remarks>
public enum Grabbed
{
    /// <summary>
    /// Nothing. The controller is not aimed at the overlay, and the trigger does
    /// nothing.
    /// </summary>
    Nothing,

    /// <summary>
    /// The panel itself, which grabs nothing.
    ///
    /// <para>
    /// An answer rather than an absence: the Commander is pointing at the
    /// overlay, and something will want to know that even while there is nothing
    /// on it to press. Not moving is the point — the panel can be read and
    /// pointed at without being dragged out of place by accident.
    /// </para>
    /// </summary>
    Content,

    /// <summary>
    /// The bar beneath the panel, which moves it.
    /// </summary>
    Bar,

    /// <summary>
    /// The top left corner, which scales it.
    /// </summary>
    TopLeftCorner,

    /// <summary>
    /// The top right corner, which scales it.
    /// </summary>
    TopRightCorner,

    /// <summary>
    /// The bottom left corner, which scales it.
    /// </summary>
    BottomLeftCorner,

    /// <summary>
    /// The bottom right corner, which scales it.
    /// </summary>
    BottomRightCorner,
}
