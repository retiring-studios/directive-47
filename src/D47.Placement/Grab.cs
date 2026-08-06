using System;
using System.Numerics;

namespace D47.Placement;

/// <summary>
/// The overlay held in a hand: where it was when the trigger went down, and
/// where it goes as that hand moves.
/// </summary>
///
/// <remarks>
/// <para>
/// The arithmetic and none of the runtime, the same cut <see cref="Anchor"/> and
/// <see cref="WorldLocked"/> make. Reading a controller and noticing a trigger
/// belongs to the adapter and needs SteamVR; what those mean for where the
/// overlay goes is this, and needs nothing — which is why a grab can be got
/// right in CI before anybody puts a headset on.
/// </para>
/// <para>
/// A rigid hold rather than a drag along the laser. Everything keeps the
/// distance and angle it had at the moment it was grabbed, so the overlay
/// behaves like a thing in the hand rather than a thing on a string. It is also
/// what makes the gesture reversible without a special case: put your hand back
/// and the overlay is back.
/// </para>
/// <para>
/// Nothing here re-faces the overlay towards the Commander as it moves. Whether
/// it should is
/// [#216](https://github.com/retiring-studios/directive-47/issues/216), which is
/// its own feature, and a grab that quietly did it would settle that question by
/// accident.
/// </para>
/// </remarks>
public sealed class Grab
{
    private readonly Pose _handAtGrab;
    private readonly Pose _boardAtGrab;
    private readonly Quaternion _handTurnedBack;

    private Grab(Pose handAtGrab, Pose boardAtGrab, Quaternion handTurnedBack)
    {
        _handAtGrab = handAtGrab;
        _boardAtGrab = boardAtGrab;
        _handTurnedBack = handTurnedBack;
    }

    /// <summary>
    /// Takes hold, at the moment the trigger goes down.
    /// </summary>
    /// <param name="hand">Where the controller is.</param>
    /// <param name="board">Where the overlay is.</param>
    /// <returns>The hold, to be asked where the overlay goes.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of either pose is not a finite number, or an orientation is not
    /// a rotation.
    /// </exception>
    public static Grab Started(Pose hand, Pose board)
    {
        Numbers.MustBeFinite(hand.Position, nameof(hand));
        Numbers.MustBeFinite(board.Position, nameof(board));

        Quaternion held = Turn.Of(hand.Orientation);

        // The board's orientation is checked here rather than kept, because
        // Follows rebuilds it from the hand and never reads it back. Checking it
        // anyway is what stops a caller handing in nonsense and being told about
        // it a frame later, somewhere else.
        Turn.Of(board.Orientation);

        // Undoing the hand's rotation is the whole of what makes this rigid, and
        // it is worked out once. The conjugate of a unit quaternion is its
        // inverse, and Turn.Of has already made this one unit length.
        return new Grab(hand, board, Quaternion.Conjugate(held));
    }

    /// <summary>
    /// Where the overlay goes, given where the hand is now.
    /// </summary>
    ///
    /// <remarks>
    /// Measured from where the grab started rather than from the last answer.
    /// Accumulating would make the overlay's speed a function of how often this
    /// is asked, which reads as the tracking being broken rather than as the
    /// arithmetic being wrong.
    /// </remarks>
    /// <param name="hand">Where the controller is now.</param>
    /// <returns>Where the overlay should be.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of the pose is not a finite number, or the orientation is not a
    /// rotation.
    /// </exception>
    public Pose Follows(Pose hand)
    {
        Numbers.MustBeFinite(hand.Position, nameof(hand));

        Quaternion now = Turn.Of(hand.Orientation);

        // How far the hand has turned since it took hold.
        Quaternion turned = now * _handTurnedBack;

        // The overlay was held out in front of the hand, and that arm turns with
        // the wrist. Rotating the offset is what carries the overlay around the
        // hand; leaving it alone would spin the overlay where it stands while
        // the hand turned away from it.
        var arm = Vector3.Transform(_boardAtGrab.Position - _handAtGrab.Position, turned);

        return new Pose(
            hand.Position + arm,
            turned * Turn.Of(_boardAtGrab.Orientation));
    }
}
