using System;
using System.Globalization;
using System.Runtime.InteropServices;

using D47.Placement;
using D47.Render;

using Valve.VR;

namespace D47.VrOverlay;

/// <summary>
/// The overlay SteamVR is holding: a handle, a quad, and the render on it.
///
/// <para>
/// Thin on purpose. It creates the overlay, puts pixels on it, shows and hides
/// it, and gives it back. Where the quad goes belongs to <c>D47.Placement</c>,
/// and whether to have one at all belongs outside this project entirely.
/// </para>
/// </summary>
internal sealed class SteamVrOverlay : IHeadsetOverlay
{
    private readonly ulong _handle;

    /// <summary>
    /// Where the laser was last seen on this quad, in metres from its middle.
    /// </summary>
    ///
    /// <remarks>
    /// Remembered because SteamVR reports movement rather than position: a hand
    /// held still sends nothing at all, and treating no event as no pointer
    /// would make the highlight flicker off the moment somebody stopped moving.
    /// </remarks>
    private (float Across, float Up)? _pointed;

    private bool _disposed;

    private SteamVrOverlay(ulong handle, Board placed)
    {
        _handle = handle;
        Placed = placed;
    }

    /// <inheritdoc/>
    public Board Placed { get; }

    /// <inheritdoc/>
    public bool IsVisible => !_disposed && OpenVR.Overlay.IsOverlayVisible(_handle);

    /// <summary>
    /// Creates the overlay and puts the render on it.
    /// </summary>
    ///
    /// <remarks>
    /// Everything here throws on failure rather than answering null. By the
    /// time this runs the runtime has already said yes, so anything that goes
    /// wrong from here is ours — and a defect of ours reported as an
    /// unsupported machine is a defect nobody ever finds.
    /// </remarks>
    /// <param name="key">The key SteamVR files the overlay under.</param>
    /// <param name="name">What SteamVR shows a human.</param>
    /// <param name="placed">Where the quad goes and how wide it is, in metres.</param>
    /// <param name="pixels">The render, as RGBA.</param>
    /// <param name="width">How wide the render is.</param>
    /// <param name="height">How tall the render is.</param>
    /// <returns>The overlay, which the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">SteamVR refused something.</exception>
    internal static SteamVrOverlay Showing(
        string key, string name, Board placed, byte[] pixels, int width, int height)
    {
        ulong handle = 0;

        EVROverlayError made = OpenVR.Overlay.CreateOverlay(key, name, ref handle);

        // Named, because the runtime's word for it is not the useful half. A key
        // is one application's identity for one quad, so the only way to be told
        // it is in use is that something else already has it — and on this
        // machine that something is almost always a Directive 47 already
        // running. Cost twenty minutes of reading KeyInUse as a defect.
        if (made == EVROverlayError.KeyInUse)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SteamVR already has an overlay under \"{key}\". A key is one "
                    + $"application's identity for one quad, so something else is holding "
                    + $"it — check whether Directive 47 is already running."));
        }

        Insist(made, "create the overlay");

        var overlay = new SteamVrOverlay(handle, placed);

        try
        {
            // Width only. OpenVR takes one number and works the height out from
            // the texture's shape, which is why the chrome's texture has to be
            // the proportions of the quad it goes on rather than any convenient
            // size.
            Insist(
                OpenVR.Overlay.SetOverlayWidthInMeters(handle, placed.Width),
                "size the overlay");

            HmdMatrix34_t where = Quad.At(placed.Where);

            Insist(
                OpenVR.Overlay.SetOverlayTransformAbsolute(
                    handle, ETrackingUniverseOrigin.TrackingUniverseSeated, ref where),
                "place the overlay");

            overlay.Paint(pixels, width, height);
        }
        catch
        {
            // The handle exists from the moment CreateOverlay succeeded, so a
            // failure after that point still has a quad to give back. Without
            // this, a refused transform would leave one in the compositor with
            // nothing holding it.
            overlay.Dispose();
            throw;
        }

        return overlay;
    }

    /// <summary>
    /// Puts pixels on the quad.
    /// </summary>
    ///
    /// <remarks>
    /// The buffer is pinned for the length of the call and no longer. OpenVR
    /// copies what it is given, so nothing here has to outlive the return —
    /// which is why this can hand over a managed array at all.
    /// </remarks>
    /// <param name="pixels">The render, as RGBA.</param>
    /// <param name="width">How wide the render is.</param>
    /// <param name="height">How tall the render is.</param>
    /// <exception cref="InvalidOperationException">SteamVR refused the texture.</exception>
    internal void Paint(byte[] pixels, int width, int height)
    {
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);

        try
        {
            Insist(
                OpenVR.Overlay.SetOverlayRaw(
                    _handle,
                    pinned.AddrOfPinnedObject(),
                    (uint)width,
                    (uint)height,
                    4),
                "put the render on the overlay");
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <inheritdoc/>
    ///
    /// <remarks>
    /// The render happens here rather than arriving already done, because an
    /// a presentation is what the rest of the application holds and pixels are what
    /// SteamVR wants — and being the place those two meet is most of what this
    /// project is for.
    /// </remarks>
    public void Paint(Presentation presented)
    {
        (byte[] pixels, int width, int height) = PanelRender.Take(presented);

        Paint(pixels, width, height);
    }

    /// <summary>
    /// Asks SteamVR to point a laser at this overlay and tell us where it lands.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Without this an overlay is a picture. SteamVR draws the laser and the
    /// cursor for overlays that say they take one, and draws nothing for
    /// overlays that do not — which is why the chrome appeared in the headset
    /// with no way to aim at it.
    /// </para>
    /// <para>
    /// <c>MakeOverlaysInteractiveIfVisible</c> is what extends that past the
    /// dashboard. A dashboard overlay gets a laser because the dashboard is up;
    /// this one has to ask, because it lives in the Commander's cockpit while
    /// the game is the scene application.
    /// </para>
    /// <para>
    /// The mouse scale is set to the quad's own metres, so a position reported
    /// back is measured in the same units everything else here uses. The
    /// alternative is a texture-pixel coordinate that would have to be converted
    /// by whoever reads it, which is a second place to get the mapping wrong.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">SteamVR refused something.</exception>
    internal void TakeAPointer()
    {
        Insist(
            OpenVR.Overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.Mouse),
            "give the overlay a pointer");

        Insist(
            OpenVR.Overlay.SetOverlayFlag(
                _handle, VROverlayFlags.MakeOverlaysInteractiveIfVisible, true),
            "make the overlay interactive outside the dashboard");

        var inMetres = new HmdVector2_t { v0 = Placed.Width, v1 = Placed.Height };

        Insist(
            OpenVR.Overlay.SetOverlayMouseScale(_handle, ref inMetres),
            "measure the overlay's pointer in metres");
    }

    /// <summary>
    /// Where the laser is on this overlay, if it is on it at all.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Drains the queue rather than reading one event, because the answer wanted
    /// is where the pointer is now and everything before the last move is
    /// already history.
    /// </para>
    /// <para>
    /// Measured from the middle of the quad, because that is the frame
    /// <c>Chrome.Parts</c> speaks. SteamVR counts from a corner, and the shift
    /// happens here so that nothing above the adapter has to know it counts
    /// from anywhere.
    /// </para>
    /// </remarks>
    /// <returns>
    /// Where the laser is, in metres from the middle, or nothing when it is not
    /// on this overlay.
    /// </returns>
    internal (float Across, float Up)? Pointed()
    {
        var arrived = new VREvent_t();
        uint size = (uint)Marshal.SizeOf<VREvent_t>();

        while (OpenVR.Overlay.PollNextOverlayEvent(_handle, ref arrived, size))
        {
            switch ((EVREventType)arrived.eventType)
            {
                case EVREventType.VREvent_MouseMove:
                    _pointed = (
                        arrived.data.mouse.x - (Placed.Width / 2),
                        arrived.data.mouse.y - (Placed.Height / 2));
                    break;

                // The laser left. Without this the highlight stays wherever it
                // was last seen, which reads as a stuck cursor rather than as a
                // hand that moved away.
                case EVREventType.VREvent_FocusLeave:
                    _pointed = null;
                    break;

                default:
                    break;
            }
        }

        return _pointed;
    }

    /// <inheritdoc/>
    public void Show() => Insist(OpenVR.Overlay.ShowOverlay(_handle), "show the overlay");

    /// <inheritdoc/>
    public void Hide() => Insist(OpenVR.Overlay.HideOverlay(_handle), "hide the overlay");

    /// <summary>
    /// Gives the quad back.
    /// </summary>
    ///
    /// <remarks>
    /// The runtime is not shut down here. The session belongs to whoever
    /// joined it, and one overlay going away is not that — a Directive 47 that
    /// left SteamVR every time a surface was hidden would be an application the
    /// compositor kept dropping from its list.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        OpenVR.Overlay.DestroyOverlay(_handle);
    }

    /// <summary>
    /// Turns a refusal into something a person can read.
    /// </summary>
    ///
    /// <remarks>
    /// Named for what it does to the caller rather than for what it checks. The
    /// alternative was every call site growing three lines of the same
    /// <c>if</c>, which is how the interesting line stops being visible.
    /// </remarks>
    private static void Insist(EVROverlayError answer, string what)
    {
        if (answer == EVROverlayError.None)
        {
            return;
        }

        throw new InvalidOperationException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"SteamVR would not {what}: {answer}."));
    }
}
