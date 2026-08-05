using System;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// What a controller is aimed at.
/// </summary>
///
/// <remarks>
/// The arithmetic and none of the runtime. Reading where a controller actually
/// is belongs to the adapter, and needs SteamVR; deciding what being there means
/// is this, and needs nothing. That is the same cut <c>Anchor</c> and
/// <c>WorldLocked</c> already make, and it is why grabbing can be got right in
/// CI before anybody puts a headset on.
/// </remarks>
public static class Pointing
{
    /// <summary>
    /// How much of each half is edge rather than body.
    ///
    /// <para>
    /// A fifth, which on the shipped half-metre quad is the outer five
    /// centimetres of each side. A number to react to rather than a measured
    /// one, in the same spirit as the quad's width and the overlay's opening
    /// opacity — too small and scaling is a thing a Commander misses and moves
    /// the overlay instead, too large and there is no middle left to move it by.
    /// This is the one value to change if it feels wrong.
    /// </para>
    ///
    /// <para>
    /// A share rather than a distance in metres, so that it stays the same
    /// gesture once the overlay can be scaled. A fixed margin would be most of a
    /// small overlay and a sliver of a large one.
    /// </para>
    /// </summary>
    private const float EdgeShare = 0.2f;

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
    /// <param name="board">The overlay, as something with edges.</param>
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

        MustBeASize(board.Width, nameof(board));
        MustBeASize(board.Height, nameof(board));

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
        // into a comparison that is false for every edge — the right answer by
        // accident, which is the kind that stops being right when somebody
        // rearranges the comparisons.
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

        Grabbed across = Within(local.X, board.Width, Grabbed.LeftEdge, Grabbed.RightEdge);
        Grabbed up = Within(local.Y, board.Height, Grabbed.BottomEdge, Grabbed.TopEdge);

        if (across == Grabbed.Nothing || up == Grabbed.Nothing)
        {
            return Grabbed.Nothing;
        }

        return across == Grabbed.Body && up == Grabbed.Body
            ? Grabbed.Body
            : Nearer(local.X, board.Width, across, local.Y, board.Height, up);
    }

    /// <summary>
    /// Whereabouts along one axis a point landed: off the end, in the body, or in
    /// one of the two edge zones.
    /// </summary>
    /// <param name="where">How far from the middle, in metres.</param>
    /// <param name="size">The full extent of that axis.</param>
    /// <param name="below">The edge in the negative direction.</param>
    /// <param name="above">The edge in the positive direction.</param>
    /// <returns>
    /// <see cref="Grabbed.Nothing"/> past the end, <see cref="Grabbed.Body"/>
    /// inside, or the edge it fell in.
    /// </returns>
    private static Grabbed Within(float where, float size, Grabbed below, Grabbed above)
    {
        float half = size / 2;
        float reach = MathF.Abs(where);

        if (reach > half)
        {
            return Grabbed.Nothing;
        }

        return reach < half * (1 - EdgeShare)
            ? Grabbed.Body
            : where < 0 ? below : above;
    }

    /// <summary>
    /// Which of two answers to give for a point that is in both edge zones.
    /// </summary>
    ///
    /// <remarks>
    /// A corner. Scaling keeps the contents' proportions whichever edge is
    /// taken, so this only has to be decided and then stay decided — and
    /// proportionally furthest out is the one a Commander was most likely aiming
    /// for. Where only one axis is an edge, that axis wins by having a larger
    /// share than a body's.
    /// </remarks>
    private static Grabbed Nearer(
        float across, float width, Grabbed horizontal, float up, float height, Grabbed vertical)
    {
        if (horizontal == Grabbed.Body)
        {
            return vertical;
        }

        if (vertical == Grabbed.Body)
        {
            return horizontal;
        }

        return MathF.Abs(across) / (width / 2) >= MathF.Abs(up) / (height / 2)
            ? horizontal
            : vertical;
    }

    private static void MustBeASize(float extent, string name)
    {
        if (!float.IsFinite(extent) || extent <= 0)
        {
            throw new ArgumentOutOfRangeException(
                name, extent, "A board with no size is not something anybody can point at.");
        }
    }
}
