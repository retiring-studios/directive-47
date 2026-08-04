using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;

using D47.Placement;

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
    /// <summary>
    /// How wide the quad is in the cockpit, in metres.
    ///
    /// <para>
    /// A number to react to rather than a measured one, in the same spirit as
    /// the game overlay's opening opacity. Half a metre at roughly arm's length
    /// is legible without filling the view; it is expected to move once
    /// somebody has worn it, and it becomes a setting when
    /// [#140](https://github.com/retiring-studios/directive-47/issues/140)
    /// gives the Commander a way to say so.
    /// </para>
    /// </summary>
    private const float AboutAHandSpan = 0.5f;

    /// <summary>
    /// Where the quad sits until something tells it otherwise: a metre and a
    /// half in front of the seated origin, square to the view.
    ///
    /// <para>
    /// A <c>Pose</c> rather than a matrix, and that is the whole of this story.
    /// It used to be twelve frozen floats in OpenVR's own layout — written when
    /// <c>D47.Placement</c> was an empty scaffold, and honest then — which made
    /// this adapter the thing deciding where the overlay went, contrary to what
    /// its own project file says about it.
    /// </para>
    /// <para>
    /// Still a value the adapter holds, because nothing supplies one yet.
    /// Remembering it across a restart is
    /// [#141](https://github.com/retiring-studios/directive-47/issues/141), and
    /// putting it somewhere by voice is
    /// [#140](https://github.com/retiring-studios/directive-47/issues/140). A
    /// parameter with no caller would be a seam built for nobody.
    /// </para>
    /// </summary>
    private static readonly Pose InFrontOfTheCommander =
        new(new Vector3(0, 0, -1.5f), Quaternion.Identity);

    private readonly ulong _handle;
    private bool _disposed;

    private SteamVrOverlay(ulong handle)
    {
        _handle = handle;
    }

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
    /// <param name="pixels">The render, as RGBA.</param>
    /// <param name="width">How wide the render is.</param>
    /// <param name="height">How tall the render is.</param>
    /// <returns>The overlay, which the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">SteamVR refused something.</exception>
    internal static SteamVrOverlay Showing(
        string key, string name, byte[] pixels, int width, int height)
    {
        ulong handle = 0;

        Insist(OpenVR.Overlay.CreateOverlay(key, name, ref handle), "create the overlay");

        var overlay = new SteamVrOverlay(handle);

        try
        {
            Insist(
                OpenVR.Overlay.SetOverlayWidthInMeters(handle, AboutAHandSpan),
                "size the overlay");

            HmdMatrix34_t where = Quad.At(InFrontOfTheCommander);

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
