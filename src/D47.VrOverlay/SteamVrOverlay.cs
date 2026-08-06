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

        Insist(OpenVR.Overlay.CreateOverlay(key, name, ref handle), "create the overlay");

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
