using System;
using System.Collections.Generic;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// One rectangle of chrome, and what grabbing it would do.
/// </summary>
///
/// <remarks>
/// Measured from the middle of the chrome quad, in metres, because that is the
/// frame the thing carrying it is drawn in.
/// </remarks>
/// <param name="What">What pulling the trigger here would take hold of.</param>
/// <param name="Left">Its left edge.</param>
/// <param name="Right">Its right edge.</param>
/// <param name="Bottom">Its bottom edge.</param>
/// <param name="Top">Its top edge.</param>
public readonly record struct Patch(
    Grabbed What, float Left, float Right, float Bottom, float Top);

/// <summary>
/// The overlay's chrome: the bar that moves it and the handles that scale it,
/// as a shape in the cockpit.
/// </summary>
///
/// <remarks>
/// <para>
/// A second quad around the panel rather than anything painted on it.
/// <c>PanelRender</c>'s invariant keeps VR-specific drawing out of the shared
/// render, so the affordance lives on a surface of its own — transparent through
/// the middle, sitting outside the content. See the Architecture section of
/// <c>docs/decisions.md</c>.
/// </para>
/// <para>
/// The proportions live here and <see cref="Parts"/> is the only thing that
/// reads them, so where a grab target starts is answered once. Two constants —
/// one for the arithmetic and one for the drawing — is exactly the drift the
/// same decision rules out.
/// </para>
/// </remarks>
public static class Chrome
{
    /// <summary>
    /// How tall the bar under the panel is, as a share of the panel's height.
    ///
    /// <para>
    /// A share rather than a distance in metres, so the chrome stays the same
    /// gesture once the overlay can be scaled — a fixed size would be most of a
    /// small overlay and a sliver of a large one. On the shipped half-metre quad
    /// this is a little over four centimetres, which is a comfortable thing to
    /// point at from a seat.
    /// </para>
    ///
    /// <para>
    /// A number to react to rather than a measured one, in the same spirit as
    /// the quad's width and the overlay's opening opacity.
    /// </para>
    /// </summary>
    internal const float BarShare = 0.15f;

    /// <summary>
    /// How big a corner handle is, as a share of the panel's shorter side.
    ///
    /// <para>
    /// The shorter side, so a wide, short panel does not get handles taller than
    /// itself. Each handle straddles its corner rather than sitting inside or
    /// outside it, which is what makes it reachable whether the Commander aims a
    /// little high or a little wide — and is why the chrome has to reach past
    /// the panel on every side.
    /// </para>
    /// </summary>
    internal const float CornerShare = 0.2f;

    /// <summary>
    /// The quad the chrome is drawn on, given the panel it belongs to.
    /// </summary>
    ///
    /// <remarks>
    /// Lower than the panel, because the bar hangs beneath and the handles only
    /// straddle. Centring the two would put the bar half off the bottom of the
    /// quad meant to be carrying it.
    /// </remarks>
    /// <param name="panel">The panel this chrome goes around.</param>
    /// <returns>Where the chrome quad sits, and how big it is.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of the panel's pose is not a finite number, or its orientation
    /// is not a rotation.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The panel has no width or no height.
    /// </exception>
    public static Board Around(Board panel)
    {
        Numbers.MustBeFinite(panel.Where.Position, nameof(panel));
        Numbers.MustBeFinite(panel.Where.Orientation, nameof(panel));

        MustBeASize(panel.Width, nameof(panel));
        MustBeASize(panel.Height, nameof(panel));

        float overhang = Reach(panel);
        float bar = panel.Height * BarShare;

        float above = (panel.Height / 2) + overhang;
        float below = (panel.Height / 2) + MathF.Max(bar, overhang);

        Quaternion facing = Turn.Of(panel.Where.Orientation);

        // Down in the panel's own frame, not the world's. A panel rolled on its
        // side has its bar out to one side, and chrome that always dropped along
        // world down would leave it hanging off a corner.
        var dropped = Vector3.Transform(
            new Vector3(0, (above - below) / 2, 0), facing);

        return new Board(
            new Pose(panel.Where.Position + dropped, panel.Where.Orientation),
            panel.Width + (overhang * 2),
            above + below);
    }

    /// <summary>
    /// Every piece of chrome there is, in the chrome quad's own metres, measured
    /// from its middle.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Here rather than wherever the drawing happens, because the drawing and
    /// the hit test have to agree about where a handle is — and the way they stop
    /// agreeing is each working it out from the shares. This is the one place the
    /// shares are read.
    /// </para>
    /// <para>
    /// In the chrome's frame and not the panel's, because the chrome is what
    /// gets painted. The two differ by the drop that makes room for the bar,
    /// which is exactly the offset a renderer would otherwise forget.
    /// </para>
    /// </remarks>
    /// <param name="panel">The panel this chrome goes around.</param>
    /// <returns>
    /// The four handles and then the bar, in the order a point should be tested
    /// against them — most specific first, because the lower handles overlap the
    /// bar and taking the bar there would leave two of them unreachable.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Some part of the panel's pose is not a finite number, or its orientation
    /// is not a rotation.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The panel has no width or no height.
    /// </exception>
    public static IReadOnlyList<Patch> Parts(Board panel)
    {
        Board around = Around(panel);

        float reach = Reach(panel);
        float bar = panel.Height * BarShare;

        // The panel's edges in the chrome's frame. The chrome hangs lower to
        // carry the bar, so this offset is not zero and treating it as zero puts
        // every piece half a bar too high.
        float dropped = around.Where.Position.Y - panel.Where.Position.Y;

        float top = (panel.Height / 2) - dropped;
        float bottom = (-panel.Height / 2) - dropped;
        float left = -panel.Width / 2;
        float right = panel.Width / 2;

        // Handles first, and the order is part of the contract rather than
        // incidental: a handle straddles its corner, so the lower pair overlap
        // the bar. Whoever tests a point against these takes the first that
        // contains it, and taking the bar there would make two of the four
        // handles unreachable.
        return
        [
            Handle(Grabbed.TopLeftCorner, left, top, reach),
            Handle(Grabbed.TopRightCorner, right, top, reach),
            Handle(Grabbed.BottomLeftCorner, left, bottom, reach),
            Handle(Grabbed.BottomRightCorner, right, bottom, reach),
            new Patch(Grabbed.Bar, left, right, bottom - bar, bottom),
        ];
    }

    /// <summary>
    /// What is at a point on the chrome, measured from the middle of the chrome
    /// quad in metres.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Shaped for a runtime that has already worked out where a laser met the
    /// quad, which SteamVR has. Where the ray met it is its to answer; what is
    /// there is still ours.
    ///
    /// <para>
    /// There was a <c>Pointing.At</c> that took a controller pose and did the
    /// ray-quad intersection itself. It was deleted once this existed — see the
    /// Architecture section of <c>docs/decisions.md</c>.
    /// </para>
    /// </para>
    /// <para>
    /// Anywhere that is not a piece of chrome is the content — the middle of the
    /// quad is transparent and the panel shows through it, so a point there is a
    /// Commander aiming at what the overlay is saying rather than at nothing.
    /// </para>
    /// </remarks>
    /// <param name="panel">The panel the chrome goes around.</param>
    /// <param name="across">How far right of the chrome's middle, in metres.</param>
    /// <param name="up">How far above the chrome's middle, in metres.</param>
    /// <returns>What pulling the trigger there would take hold of.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of the panel's pose is not a finite number.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The panel has no width or no height.
    /// </exception>
    public static Grabbed On(Board panel, float across, float up)
    {
        foreach (Patch patch in Parts(panel))
        {
            if (across >= patch.Left
                && across <= patch.Right
                && up >= patch.Bottom
                && up <= patch.Top)
            {
                return patch.What;
            }
        }

        return Grabbed.Content;
    }

    /// <summary>
    /// A handle straddling a corner, so half of it is over the panel.
    /// </summary>
    private static Patch Handle(Grabbed what, float across, float up, float reach) =>
        new(what, across - reach, across + reach, up - reach, up + reach);

    /// <summary>
    /// How far a corner handle reaches past the panel — half its side, because
    /// it straddles.
    /// </summary>
    internal static float Reach(Board panel) =>
        MathF.Min(panel.Width, panel.Height) * CornerShare / 2;

    /// <summary>
    /// Throws unless an extent is a size at all.
    /// </summary>
    internal static void MustBeASize(float extent, string name)
    {
        if (!float.IsFinite(extent) || extent <= 0)
        {
            throw new ArgumentOutOfRangeException(
                name, extent, "A board with no size is not something anybody can point at.");
        }
    }
}
