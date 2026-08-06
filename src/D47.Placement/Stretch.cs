using System;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// The overlay held by one of its corners: how big it was when the trigger went
/// down, and how big it becomes as that hand moves.
/// </summary>
///
/// <remarks>
/// <para>
/// The arithmetic and none of the runtime, the same cut <see cref="Grab"/> and
/// <see cref="Anchor"/> make, and the invariant
/// [#235](https://github.com/retiring-studios/directive-47/issues/235) states:
/// where the overlay lands is arithmetic, unit-testable with no VR runtime
/// present. Reading a controller and noticing a trigger is adapter work.
/// </para>
/// <para>
/// <b>The corner opposite the one being held stays where it is.</b> Pull the top
/// right and the bottom left is nailed down; the panel grows towards the hand,
/// which is the gesture everybody already has from dragging a window corner and
/// is what keeps the handle under the laser that took hold of it. The maintainer
/// had no preference between this and pinning the middle — see
/// [#286](https://github.com/retiring-studios/directive-47/issues/286), where
/// both are written down — so this is the implementer's, and it is recorded here
/// rather than in <c>docs/decisions.md</c>.
/// </para>
/// <para>
/// The consequence is that a stretch moves the overlay as well as resizing it.
/// A scale about a fixed corner is a scale about a point, and only the point
/// itself comes out of it unmoved.
/// </para>
/// <para>
/// One factor drives both extents, because #235's invariant says scaling scales
/// and does not reflow — the contents keep their proportions and are never laid
/// out again.
/// </para>
/// </remarks>
public sealed class Stretch
{
    /// <summary>
    /// The smallest the overlay goes, in metres across.
    ///
    /// <para>
    /// A quarter of the shipped half metre. A floor is not a preference: a corner
    /// pulled past its anchor gives a board of no size, and <see cref="Chrome"/>
    /// refuses to measure one of those at all — so without this the gesture ends
    /// in an exception on the watching thread rather than in a small overlay.
    /// </para>
    /// </summary>
    internal const float Smallest = 0.125f;

    /// <summary>
    /// The largest, in metres across.
    ///
    /// <para>
    /// Four times the shipped half metre, which fills the view from a seat. Past
    /// this the bar that drags it back is somewhere off the side of the
    /// Commander's head, so a corner pull that gets away from somebody would take
    /// the overlay with it.
    /// </para>
    /// </summary>
    internal const float Largest = 2f;

    private readonly Board _boardAtGrab;
    private readonly Vector3 _pinned;
    private readonly float _reachAtGrab;

    private Stretch(Board boardAtGrab, Vector3 pinned, float reachAtGrab)
    {
        _boardAtGrab = boardAtGrab;
        _pinned = pinned;
        _reachAtGrab = reachAtGrab;
    }

    /// <summary>
    /// Whether taking hold of this scales the overlay.
    /// </summary>
    ///
    /// <remarks>
    /// Here rather than at the caller, so which members of <see cref="Grabbed"/>
    /// are corners is answered in the one place that has to know. A watcher that
    /// worked it out for itself would be a second list to keep in step, and
    /// <see cref="Started"/> throwing is no use for asking — pointing at the
    /// content is the ordinary case, not a failure.
    /// </remarks>
    /// <param name="what">What the laser is on.</param>
    /// <returns>Whether it is a corner handle.</returns>
    public static bool Scales(Grabbed what) => what is Grabbed.TopLeftCorner
        or Grabbed.TopRightCorner
        or Grabbed.BottomLeftCorner
        or Grabbed.BottomRightCorner;

    /// <summary>
    /// Takes hold of a corner, at the moment the trigger goes down.
    /// </summary>
    ///
    /// <remarks>
    /// A position rather than a pose, unlike <see cref="Grab.Started"/>. Turning
    /// the wrist does not resize anything, and taking an orientation this never
    /// reads would be inviting the next person to expect that it did.
    /// </remarks>
    /// <param name="corner">Which handle was taken hold of.</param>
    /// <param name="hand">Where the controller is.</param>
    /// <param name="board">Where the overlay is, and how big.</param>
    /// <returns>The hold, to be asked how big the overlay is now.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of the hand or the board's pose is not a finite number, the
    /// board's orientation is not a rotation, or the hand is on the corner that
    /// would be pinned.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// That is not a corner, or the board has no width or no height.
    /// </exception>
    public static Stretch Started(Grabbed corner, Vector3 hand, Board board)
    {
        Numbers.MustBeFinite(hand, nameof(hand));
        Numbers.MustBeFinite(board.Where.Position, nameof(board));

        Numbers.MustBeASize(board.Width, nameof(board));
        Numbers.MustBeASize(board.Height, nameof(board));

        Vector3 pinned = Corner(board, Opposite(corner));

        float reach = Vector3.Distance(hand, pinned);

        if (reach <= 0)
        {
            // Nothing to measure from. Every later hand position would be some
            // multiple of nought away from the anchor, so the first twitch would
            // slam the overlay into whichever limit it was heading for. Refusing
            // to start is a grab that does nothing, which is what a Commander
            // with their hand inside the anchor expects anyway.
            throw new ArgumentException(
                "A corner cannot be pulled from the corner that is holding still — "
                + "there is no distance there to measure a size against.",
                nameof(hand));
        }

        return new Stretch(board, pinned, reach);
    }

    /// <summary>
    /// How big the overlay is, and where, given where the hand is now.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A ratio of distances from the pinned corner, in three dimensions, rather
    /// than a drag projected onto the panel's plane. Pull the hand twice as far
    /// from the anchor as it started and the overlay is twice the size, whichever
    /// way the hand went to get there — which also means this needs no plane and
    /// stays arithmetic.
    /// </para>
    /// <para>
    /// Measured from where the grab started rather than from the last answer, for
    /// the reason <see cref="Grab.Follows"/> carries: accumulating would make the
    /// overlay's growth a function of how often this is asked.
    /// </para>
    /// </remarks>
    /// <param name="hand">Where the controller is now.</param>
    /// <returns>Where the overlay should be, and how big.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of the position is not a finite number.
    /// </exception>
    public Board Follows(Vector3 hand)
    {
        Numbers.MustBeFinite(hand, nameof(hand));

        float factor = Vector3.Distance(hand, _pinned) / _reachAtGrab;

        // Clamped on the width, because that is the extent the limits are stated
        // in and the height follows it. Clamping the two separately is how a
        // ceiling turns into a reflow.
        factor = Math.Clamp(
            factor,
            Smallest / _boardAtGrab.Width,
            Largest / _boardAtGrab.Width);

        // A scale about the pinned corner. Everything moves away from it by the
        // factor, including the middle — which is why this returns a board and
        // not two numbers.
        Vector3 middle = _pinned + ((_boardAtGrab.Where.Position - _pinned) * factor);

        return new Board(
            _boardAtGrab.Where with { Position = middle },
            _boardAtGrab.Width * factor,
            _boardAtGrab.Height * factor);
    }

    /// <summary>
    /// Where one of a board's corners is, in the world.
    /// </summary>
    ///
    /// <remarks>
    /// In the board's own frame and then turned, because the shipped overlay is
    /// never square to the cockpit. Reading corners off the world axes would pin
    /// a point in mid-air beside the panel rather than a point on it.
    /// </remarks>
    private static Vector3 Corner(Board board, Grabbed corner)
    {
        float across = corner is Grabbed.TopRightCorner or Grabbed.BottomRightCorner
            ? board.Width / 2
            : -board.Width / 2;

        float up = corner is Grabbed.TopLeftCorner or Grabbed.TopRightCorner
            ? board.Height / 2
            : -board.Height / 2;

        return board.Where.Position
            + Vector3.Transform(
                new Vector3(across, up, 0), Turn.Of(board.Where.Orientation));
    }

    /// <summary>
    /// The corner across the diagonal, which is the one that stays put.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">That is not a corner.</exception>
    private static Grabbed Opposite(Grabbed corner) => corner switch
    {
        Grabbed.TopLeftCorner => Grabbed.BottomRightCorner,
        Grabbed.TopRightCorner => Grabbed.BottomLeftCorner,
        Grabbed.BottomLeftCorner => Grabbed.TopRightCorner,
        Grabbed.BottomRightCorner => Grabbed.TopLeftCorner,
        _ => throw new ArgumentOutOfRangeException(
            nameof(corner),
            corner,
            "Only a corner handle scales the overlay. The bar moves it and the "
            + "content is deliberately not grabbable."),
    };
}
