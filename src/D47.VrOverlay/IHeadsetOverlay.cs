using System;

using D47.Placement;
using D47.Render;

namespace D47.VrOverlay;

/// <summary>
/// The overlay in the headset, as the application deals with it: on the
/// Commander's view or not, and two ways to change that.
///
/// <para>
/// Deliberately says nothing about SteamVR, about textures, or about where the
/// quad goes. Talking to the runtime is the adapter's job; deciding when to
/// show and when to hide is logic, and logic is asserted in CI against a
/// stand-in that is nothing but a boolean — which is the whole reason
/// <c>IGameOverlay</c> in <c>D47.GameOverlay</c> looks like this too.
/// </para>
///
/// <para>
/// Disposable, where <c>IGameOverlay</c> is not. That one is a WPF window and
/// the framework owns its lifetime; this one is a handle the compositor is
/// holding, and a quad nobody gave back stays floating in the cockpit after the
/// application that put it there has gone.
/// </para>
/// </summary>
/// <remarks>
/// One interface with a single SteamVR implementation, which is what
/// <c>docs/decisions.md</c> settled: OpenXR is a someday nice-to-have and
/// appears in no definition of done, so this keeps the door open without paying
/// for it now.
/// </remarks>
public interface IHeadsetOverlay : IDisposable
{
    /// <summary>
    /// Whether the overlay is currently in front of the Commander.
    /// </summary>
    ///
    /// <remarks>
    /// Asked of the compositor rather than remembered here. A flag set by
    /// <see cref="Show"/> would keep answering yes after SteamVR had taken the
    /// overlay away for reasons of its own, and a surface that lies about being
    /// on screen is worse than one that is not.
    /// </remarks>
    bool IsVisible { get; }

    /// <summary>
    /// Where the quad is and how big it is, in metres.
    /// </summary>
    ///
    /// <remarks>
    /// Asked of the overlay rather than worked out again by whoever needs it.
    /// The chrome framing this quad has to agree with it exactly, and the way
    /// two things stop agreeing is each deriving the answer from the same
    /// constants separately — see the Architecture section of
    /// <c>docs/decisions.md</c>.
    /// </remarks>
    Board Placed { get; }

    /// <summary>
    /// Puts the quad somewhere else, keeping its size.
    /// </summary>
    ///
    /// <remarks>
    /// A pose rather than a board, because moving is not resizing and the two
    /// arrive from different gestures — the bar drags and the corners scale.
    /// Where to move to is decided in <c>D47.Placement</c> and arrives here
    /// already worked out, which is the same split every other placement follows.
    /// </remarks>
    /// <param name="where">Where the quad should be.</param>
    void MoveTo(Pose where);

    /// <summary>
    /// Puts the overlay in front of the Commander.
    /// </summary>
    void Show();

    /// <summary>
    /// Takes it away again.
    /// </summary>
    void Hide();

    /// <summary>
    /// Puts a different presented on the quad.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// An presented rather than pixels, so this interface still says nothing about
    /// textures. Turning one into the other is the adapter's job and most of the
    /// reason the adapter exists.
    /// </para>
    /// <para>
    /// Unconditional: it paints whatever it is given, every time it is asked.
    /// Whether an answer is worth painting is a decision, decisions live outside
    /// the adapter, and an overlay that quietly declined would be one nothing in
    /// CI could catch declining wrongly.
    /// </para>
    /// </remarks>
    /// <param name="presented">What to show.</param>
    void Paint(Presentation presented);
}
