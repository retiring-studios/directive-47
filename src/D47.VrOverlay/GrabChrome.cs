using System;

using D47.Placement;

namespace D47.VrOverlay;

/// <summary>
/// What the laser is on, whether the trigger is down, and which hand is doing
/// it.
/// </summary>
///
/// <remarks>
/// The three together rather than three calls, because they are one answer taken
/// from one poll of the runtime's event queue. Asked separately they could come
/// from different moments and describe a hand that was never in that state.
/// </remarks>
/// <param name="On">What pulling the trigger would take hold of.</param>
/// <param name="Held">Whether the trigger is down now.</param>
/// <param name="Hand">Which device the laser belongs to, as SteamVR numbers them.</param>
public readonly record struct Grip(Grabbed On, bool Held, uint Hand);

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
    /// <param name="shown">What the laser is near enough to be worth drawing.</param>
    void Showing(Grabbed lit, Shown shown);

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
    Grip Follow();

    /// <summary>
    /// Puts the chrome back around a panel that has been moved or resized.
    /// </summary>
    ///
    /// <remarks>
    /// The chrome is a second quad and does not follow the panel on its own.
    /// Without this a dragged panel slides out from inside its own bar and a
    /// scaled one swells out from behind its own handles, both of which look like
    /// the chrome being broken rather than like nobody having moved it.
    /// </remarks>
    /// <param name="panel">Where the panel is now, and how big.</param>
    void Frames(Board panel);
}

/// <summary>
/// The chrome, on a second quad SteamVR is holding.
/// </summary>
internal sealed class GrabChrome : IGrabChrome
{
    private readonly SteamVrOverlay _overlay;

    /// <summary>
    /// The panel this chrome frames. Not readonly since the panel can be
    /// dragged, and chrome measured against where it used to be would put every
    /// grab target somewhere the Commander is not aiming.
    /// </summary>
    private Board _panel;

    private Grabbed _showing;
    private Shown _shown;
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
        // Empty to begin with. Nobody is pointing at an overlay that has only
        // just appeared, and the chrome is invisible until they do.
        (byte[] pixels, int width, int height) =
            ChromeRender.Take(panel, Grabbed.Nothing, Shown.Nothing);

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
    public void Showing(Grabbed lit, Shown shown)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Nothing to do is the common case by a wide margin — a hand resting
        // still is the same answer every time it is looked at, and repainting a
        // texture thirty times a second to say so would be the whole cost of
        // this feature for no visible difference.
        //
        // Both halves, because a laser moving off a handle changes what is drawn
        // without changing what is lit. Comparing whole values is why Shown is a
        // record struct rather than a set.
        if (lit == _showing && shown == _shown)
        {
            return;
        }

        _showing = lit;
        _shown = shown;

        Repaint();
    }

    /// <inheritdoc/>
    public Grip Follow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Pointed first, because it is the call that drains the queue — the
        // trigger is read out of what that drain left behind.
        (float Across, float Up)? at = _overlay.Pointed();

        Grabbed lit = at is { } on ? Chrome.On(_panel, on.Across, on.Up) : Grabbed.Nothing;

        // Nothing near when the laser is elsewhere, which is an empty quad.
        Shown shown = at is { } close
            ? Chrome.Showing(_panel, close.Across, close.Up)
            : Shown.Nothing;

        (bool held, uint hand) = _overlay.Trigger();

        Showing(lit, shown);

        return new Grip(lit, held, hand);
    }

    /// <inheritdoc/>
    public void Frames(Board panel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The width alone, because a scale is uniform and the chrome's shares are
        // all shares of the panel — so a panel that kept its width kept its
        // height too, and the chrome around it is the shape it already was.
        bool resized = panel.Width != _panel.Width;

        _panel = panel;

        Board around = Chrome.Around(panel);

        if (!resized)
        {
            // A drag is the common case by a wide margin and moves the quad
            // thirty times a second. Resizing it to the size it already is would
            // be two more runtime calls per look to say nothing changed.
            _overlay.MoveTo(around.Where);

            return;
        }

        _overlay.ResizeTo(around);

        // And the texture after it. ChromeRender works its pixel size out from
        // the quad's metres, so a chrome left on its old render would be the
        // right shape at the wrong resolution — sharp at half a metre and soft
        // at two.
        Repaint();
    }

    /// <summary>
    /// Draws the chrome as it currently stands and puts it on the quad.
    /// </summary>
    ///
    /// <remarks>
    /// Both callers pass the same three things, and they are all fields — what
    /// changed before getting here is what differs. Written out twice, the second
    /// copy is where somebody eventually paints against a panel that has been
    /// replaced.
    /// </remarks>
    private void Repaint()
    {
        (byte[] pixels, int width, int height) =
            ChromeRender.Take(_panel, _showing, _shown);

        _overlay.Paint(pixels, width, height);
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
