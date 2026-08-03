using System;

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
    /// Puts the overlay in front of the Commander.
    /// </summary>
    void Show();

    /// <summary>
    /// Takes it away again.
    /// </summary>
    void Hide();
}
