using System;

using D47.GameOverlay;
using D47.Render;

namespace D47.Panel;

/// <summary>
/// The game overlay, as the application deals with it: one that may not exist,
/// and a way to turn it on and off.
///
/// <para>
/// The decisions live here rather than in <c>D47.GameOverlay</c>, because
/// carrying on without a surface and knowing which way a toggle goes are logic,
/// and that project is an adapter. It is also what makes both assertable in CI:
/// a stand-in can refuse to be created, and a stand-in overlay is a boolean,
/// where a real machine cannot be asked to stop compositing and a real window
/// needs a desktop.
/// </para>
/// </summary>
internal sealed class Overlay
{
    private readonly IGameOverlay? _overlay;
    private readonly Func<IntPtr, bool> _isTheGame;

    private Overlay(IGameOverlay? overlay, Func<IntPtr, bool> isTheGame)
    {
        _overlay = overlay;
        _isTheGame = isTheGame;
    }

    /// <summary>
    /// Asks for an overlay, and accepts the answer either way.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Nothing is caught. A factory that throws has a defect in it, and a defect
    /// reported as an unsupported machine is a defect nobody ever finds. The
    /// only thing this understands is <see langword="null"/>.
    /// </para>
    /// <para>
    /// The machine is asked at startup, before anything knows whether Elite is
    /// running. Whether a machine can host an overlay has nothing to do with
    /// whether the game happens to be up, and asking in the other order would
    /// leave this unreached — and so untested — on every machine without the
    /// game. That includes CI.
    /// </para>
    /// </remarks>
    /// <param name="overlays">Where an overlay comes from.</param>
    /// <param name="answer">What it should render.</param>
    /// <param name="isTheGame">
    /// Whether a given window belongs to Elite. Defaults to asking the real
    /// game.
    /// </param>
    /// <returns>The overlay, or one that will quietly do nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overlays"/> is null.</exception>
    internal static Overlay From(
        IGameOverlayFactory overlays,
        Answer answer,
        Func<IntPtr, bool>? isTheGame = null)
    {
        ArgumentNullException.ThrowIfNull(overlays);

        return new Overlay(overlays.Create(answer), isTheGame ?? EliteWindow.IsTheGame);
    }

    /// <summary>
    /// Takes the mouse out of the way when the game comes forward, and takes it
    /// back when the game goes away.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Both halves matter. While Elite is in front, a click over the overlay
    /// has to reach the cockpit underneath — an overlay that swallows one is
    /// worse than no overlay. While it is not, the overlay has to be reachable,
    /// or there is nothing for a Commander to grab and move.
    /// </para>
    /// <para>
    /// Told rather than asked. Windows says when the foreground changes and the
    /// application reacts; polling for it would be a timer that is either
    /// slower than an alt-tab or busy for no reason.
    /// </para>
    /// </remarks>
    /// <param name="window">Whatever is now in front.</param>
    internal void ForegroundIsNow(IntPtr window)
    {
        if (_overlay is not { } overlay)
        {
            return;
        }

        overlay.PassesInputThrough = _isTheGame(window);
    }

    /// <summary>
    /// Puts the overlay over the game if it is not on screen, and takes it away
    /// if it is.
    ///
    /// <para>
    /// Does nothing at all when the machine could not give us an overlay. There
    /// is no error to report: the Commander pressing a key for a thing this
    /// machine cannot draw is not a failure, it is the same absence the
    /// application already carried on without.
    /// </para>
    /// </summary>
    internal void Toggle()
    {
        if (_overlay is not { } overlay)
        {
            return;
        }

        if (overlay.IsVisible)
        {
            overlay.Hide();
            return;
        }

        overlay.Show();
    }

    /// <summary>
    /// Shows the overlay, if there is one and there is a game to put it over.
    /// </summary>
    internal void Show() => _overlay?.Show();
}
