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

    private Overlay(IGameOverlay? overlay)
    {
        _overlay = overlay;
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
    /// <returns>The overlay, or one that will quietly do nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overlays"/> is null.</exception>
    internal static Overlay From(IGameOverlayFactory overlays, Answer answer)
    {
        ArgumentNullException.ThrowIfNull(overlays);

        return new Overlay(overlays.Create(answer));
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
