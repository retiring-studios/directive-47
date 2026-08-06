using System;

using D47.Placement;

namespace D47.VrOverlay;

/// <summary>
/// The bar and the handles, floating around the panel.
/// </summary>
///
/// <remarks>
/// A surface of its own rather than something the panel draws, which is what
/// keeps <c>PanelRender</c>'s invariant true — see the Architecture section of
/// <c>docs/decisions.md</c>. An interface because what decides which part to
/// light needs no runtime to be asserted.
/// </remarks>
public interface IGrabChrome : IDisposable
{
    /// <summary>
    /// Shows the chrome with one part picked out, or none.
    /// </summary>
    ///
    /// <remarks>
    /// Called as often as the hands are looked at, so it has to be cheap when
    /// nothing has changed — which is nearly always.
    /// </remarks>
    /// <param name="lit">What a trigger would take hold of right now.</param>
    void Showing(Grabbed lit);

    /// <summary>
    /// Looks at where the laser is and lights whatever it is on.
    /// </summary>
    ///
    /// <remarks>
    /// SteamVR draws the laser and works out where it meets the quad; this asks
    /// what is there and shows it. Called as often as the answer should be able
    /// to change.
    /// </remarks>
    /// <returns>What is being pointed at, for whoever wants to know.</returns>
    Grabbed Follow();
}

/// <summary>
/// The chrome, on a second quad SteamVR is holding.
/// </summary>
internal sealed class GrabChrome : IGrabChrome
{
    private readonly SteamVrOverlay _overlay;
    private readonly Board _panel;

    private Grabbed _showing;
    private bool _disposed;

    private GrabChrome(SteamVrOverlay overlay, Board panel, Grabbed showing)
    {
        _overlay = overlay;
        _panel = panel;
        _showing = showing;
    }

    /// <summary>
    /// Creates the chrome quad around a panel, with nothing lit.
    /// </summary>
    /// <param name="key">The key SteamVR files it under.</param>
    /// <param name="name">What SteamVR shows a human.</param>
    /// <param name="panel">The panel it goes around.</param>
    /// <returns>The chrome, which the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">SteamVR refused something.</exception>
    internal static GrabChrome Around(string key, string name, Board panel)
    {
        (byte[] pixels, int width, int height) = ChromeRender.Take(panel, Grabbed.Nothing);

        var overlay = SteamVrOverlay.Showing(
            key, name, Chrome.Around(panel), pixels, width, height);

        // Once, here, rather than on every look. Nothing hides the chrome —
        // nothing hides the panel it frames either — so asking the compositor to
        // show an overlay that is already showing, thirty times a second, would
        // be a runtime call per tick to say what has not changed.
        overlay.Show();

        // And this is what makes it something a Commander can aim at rather than
        // a picture floating in the cockpit.
        overlay.TakeAPointer();

        return new GrabChrome(overlay, panel, Grabbed.Nothing);
    }

    /// <inheritdoc/>
    public void Showing(Grabbed lit)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Nothing to do is the common case by a wide margin — a hand resting
        // still is the same answer every time it is looked at, and repainting a
        // texture thirty times a second to say so would be the whole cost of
        // this feature for no visible difference.
        if (lit == _showing)
        {
            return;
        }

        _showing = lit;

        (byte[] pixels, int width, int height) = ChromeRender.Take(_panel, lit);

        _overlay.Paint(pixels, width, height);
    }

    /// <inheritdoc/>
    public Grabbed Follow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Grabbed lit = _overlay.Pointed() is { } at
            ? Chrome.On(_panel, at.Across, at.Up)
            : Grabbed.Nothing;

        Showing(lit);

        return lit;
    }

    /// <summary>
    /// Gives the quad back.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _overlay.Dispose();
    }
}
