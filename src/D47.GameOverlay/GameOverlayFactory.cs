using System.Runtime.InteropServices;

using D47.Render;

namespace D47.GameOverlay;

/// <summary>
/// Makes the real overlay, on a machine that can host one.
/// </summary>
public sealed class GameOverlayFactory : IGameOverlayFactory
{
    /// <summary>
    /// Creates the overlay, or reports that this machine cannot have one.
    /// </summary>
    /// <param name="presented">What the overlay should render.</param>
    /// <returns>
    /// The overlay, or <see langword="null"/> when the desktop is not being
    /// composited.
    /// </returns>
    public IGameOverlay? Create(Presentation presented) =>
        DesktopIsComposited() ? new EliteOverlay(new GameOverlayWindow(presented)) : null;

    /// <summary>
    /// Whether Windows is compositing the desktop.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Asked, rather than discovered by catching what happens when it is not.
    /// That distinction is the whole story this method belongs to: a
    /// <c>try</c>/<c>catch</c> around the construction would convert our own
    /// defects into "unsupported machine" alongside the genuine case, and a
    /// bug that reports itself as somebody else's hardware is a bug nobody ever
    /// finds. A precondition converts nothing.
    /// </para>
    /// <para>
    /// Composition is what per-pixel alpha needs, and it is on by default on
    /// every version of Windows this product supports — so in practice this
    /// answers yes. Where it answers no is the case worth having: a remote
    /// session, or a machine whose compositor has been turned off. If the call
    /// itself fails, the answer is no, because a machine that cannot say
    /// whether it composites is not one to hand a transparent window.
    /// </para>
    /// </remarks>
    private static bool DesktopIsComposited() =>
        DwmIsCompositionEnabled(out bool composited) == 0 && composited;

    // System32 rather than the default probing order: dwmapi is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the executable being loaded instead.
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmIsCompositionEnabled(
        [MarshalAs(UnmanagedType.Bool)] out bool composited);
}
