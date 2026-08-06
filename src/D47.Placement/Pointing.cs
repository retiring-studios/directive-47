using System;
using System.Collections.Generic;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// What a controller is aimed at.
/// </summary>
///
/// <remarks>
/// <para>
/// The arithmetic and none of the runtime. Reading where a controller actually
/// is belongs to the adapter, and needs SteamVR; deciding what being there means
/// is this, and needs nothing. That is the same cut <c>Anchor</c> and
/// <c>WorldLocked</c> already make, and it is why grabbing can be got right in
/// CI before anybody puts a headset on.
/// </para>
/// <para>
/// The chrome it describes — a bar under the panel, a handle at each corner —
/// sits outside the content rather than over it. That is how Horizon OS does it,
/// and it is also the only shape available: <c>PanelRender</c>'s invariant keeps
/// anything VR-specific out of the shared render, so an affordance drawn on the
/// panel would have had nowhere to live.
/// </para>
/// </remarks>
public static class Pointing
{
    /// <summary>
    /// What the controller is pointing at.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A ray rather than a line: the overlay behind a Commander's hand is not
    /// something they are pointing at, and treating it as one would have them
    /// grab a thing they had deliberately turned away from.
    /// </para>
    /// <para>
    /// Aiming at nothing is an answer rather than an absence to report. Most of
    /// the time a controller is pointed at some part of a cockpit that is not
    /// this overlay, and that has to be quiet.
    /// </para>
    /// </remarks>
    /// <param name="controller">Where the controller is, and which way it points.</param>
    /// <param name="board">The panel, as something with edges.</param>
    /// <returns>What pulling the trigger would take hold of.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of either pose is not a finite number, or an orientation is not
    /// a rotation.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The board has no width or no height.
    /// </exception>
    public static Grabbed At(Pose controller, Board board)
    {
        Numbers.MustBeFinite(controller.Position, nameof(controller));
        Numbers.MustBeFinite(controller.Orientation, nameof(controller));
        Numbers.MustBeFinite(board.Where.Position, nameof(board));
        Numbers.MustBeFinite(board.Where.Orientation, nameof(board));

        Chrome.MustBeASize(board.Width, nameof(board));
        Chrome.MustBeASize(board.Height, nameof(board));

        Quaternion aim = Turn.Of(controller.Orientation);
        Quaternion facing = Turn.Of(board.Where.Orientation);

        // A controller points along its own negative Z and an overlay quad faces
        // along its own positive Z. Both are OpenVR's conventions rather than
        // ours, and getting either backwards is a bug that looks like the
        // arithmetic working — the overlay would simply never be pointed at.
        var along = Vector3.Transform(-Vector3.UnitZ, aim);
        var outward = Vector3.Transform(Vector3.UnitZ, facing);

        float closing = Vector3.Dot(along, outward);

        // Parallel to the plane, so the ray never meets it. Checked rather than
        // left to the division below, which answers infinity and then carries it
        // into comparisons that happen to be false — the right answer by
        // accident, which is the kind that stops being right when somebody
        // rearranges them.
        if (closing == 0)
        {
            return Grabbed.Nothing;
        }

        Vector3 toBoard = board.Where.Position - controller.Position;
        float howFar = Vector3.Dot(toBoard, outward) / closing;

        // Behind the hand. Zero counts as behind: a controller exactly in the
        // plane of the overlay is not pointing at it either.
        if (howFar <= 0)
        {
            return Grabbed.Nothing;
        }

        Vector3 met = controller.Position + (along * howFar);

        // Into the board's own frame, by undoing its rotation rather than
        // building a matrix and inverting it. The conjugate of a unit quaternion
        // is its inverse, and Turn.Of has already made this one unit length.
        var local = Vector3.Transform(
            met - board.Where.Position, Quaternion.Conjugate(facing));

        return WhereThatLands(local.X, local.Y, board);
    }

    /// <summary>
    /// What any of the Commander's hands is pointing at.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Something grabbable beats the content, and the content beats nothing.
    /// Both hands report something true when one rests on the panel and the
    /// other on a handle, and showing the content would be picking the more
    /// boring of two right answers.
    /// </para>
    /// <para>
    /// Order-independent on purpose. The runtime hands back device slots rather
    /// than hands, and which slot a controller lands in depends on the order
    /// they woke up — so an answer that depended on it would be right on some
    /// runs and not others.
    /// </para>
    /// </remarks>
    /// <param name="controllers">Wherever the hands are.</param>
    /// <param name="board">The panel, as something with edges.</param>
    /// <returns>What pulling a trigger would take hold of.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="controllers"/> is null.
    /// </exception>
    public static Grabbed At(IReadOnlyList<Pose> controllers, Board board)
    {
        ArgumentNullException.ThrowIfNull(controllers);

        Grabbed best = Grabbed.Nothing;

        foreach (Pose held in controllers)
        {
            Grabbed found = At(held, board);

            if (found is not Grabbed.Nothing and not Grabbed.Content)
            {
                return found;
            }

            if (found == Grabbed.Content)
            {
                best = found;
            }
        }

        return best;
    }

    /// <summary>
    /// What a point in the board's own frame is on, measured from the middle of
    /// the panel.
    /// </summary>
    ///
    /// <remarks>
    /// Corners first, because a handle straddles its corner: half of each is
    /// over the panel and the lower pair reach down into the bar. Asking about
    /// the content first would make the corners unreachable from three sides.
    /// </remarks>
    private static Grabbed WhereThatLands(float across, float up, Board board)
    {
        float halfWide = board.Width / 2;
        float halfTall = board.Height / 2;

        if (OnACorner(across, up, halfWide, halfTall, board) is { } corner)
        {
            return corner;
        }

        if (MathF.Abs(across) > halfWide)
        {
            return Grabbed.Nothing;
        }

        if (MathF.Abs(up) <= halfTall)
        {
            return Grabbed.Content;
        }

        // Beneath the panel and no further than the bar is tall. Only beneath:
        // the same distance above is off the top, and a bar on both sides would
        // be two things to grab that mean the same.
        float bar = board.Height * Chrome.BarShare;

        return up < 0 && up >= -(halfTall + bar) ? Grabbed.Bar : Grabbed.Nothing;
    }

    /// <summary>
    /// The corner handle a point is inside, if it is inside one.
    /// </summary>
    /// <returns>The corner, or <see langword="null"/> for anywhere else.</returns>
    private static Grabbed? OnACorner(
        float across, float up, float halfWide, float halfTall, Board board)
    {
        float reach = Chrome.Reach(board);

        if (MathF.Abs(MathF.Abs(across) - halfWide) > reach
            || MathF.Abs(MathF.Abs(up) - halfTall) > reach)
        {
            return null;
        }

        return up >= 0
            ? across < 0 ? Grabbed.TopLeftCorner : Grabbed.TopRightCorner
            : across < 0 ? Grabbed.BottomLeftCorner : Grabbed.BottomRightCorner;
    }
}
