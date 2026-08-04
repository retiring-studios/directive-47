using System;

using D47.Render;
using D47.VrOverlay;

namespace D47.Panel;

/// <summary>
/// The overlay in the headset, on a machine that may not have one.
///
/// <para>
/// Most machines running Directive 47 have no SteamVR. That is a machine which
/// cannot give us what the overlay needs, not a defect, so the application asks
/// for one, writes down that it did not get one, and carries on. The panel is
/// unaffected either way.
/// </para>
///
/// <para>
/// The twin of <see cref="Overlay"/>, deliberately down to the shape. Both wrap
/// an adapter that may decline, both compose the reason here rather than asking
/// the adapter for one, and both are assertable in CI against a stand-in that
/// refuses to be created — which a real SteamVR cannot be asked to do on a
/// machine where it is running.
/// </para>
/// </summary>
internal sealed class Headset : IDisposable
{
    /// <summary>
    /// What to say when there is no runtime to put an overlay in.
    ///
    /// <para>
    /// Names SteamVR rather than saying "no overlay". The Commander who reads
    /// this has a headset they expected something to appear in, and the useful
    /// half of the sentence is which thing was missing.
    /// </para>
    /// </summary>
    private const string NoHeadsetOverlay =
        "This machine has no SteamVR running, which is what a headset overlay needs. "
        + "Directive 47 has started without one; the panel and the game overlay are "
        + "unaffected.";

    private readonly IHeadsetOverlay? _overlay;

    private bool _disposed;

    private Headset(IHeadsetOverlay? overlay)
    {
        _overlay = overlay;
    }

    /// <summary>
    /// Asks the machine for a headset overlay, and carries on without one.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The reason is composed here rather than asked of the factory, and the
    /// contract is what makes that honest: null from <c>Create</c> means the
    /// runtime and only the runtime, because every other failure throws. So
    /// "there is no SteamVR" is the whole of what null is allowed to mean.
    /// </para>
    /// <para>
    /// Which is also why nothing here catches. "Absent, not failed" is one
    /// <c>try</c>/<c>catch</c> away from reporting our own defects as a machine
    /// without a headset, and a defect reported that way is one nobody ever
    /// finds.
    /// </para>
    /// </remarks>
    /// <param name="overlays">Where a headset overlay comes from.</param>
    /// <param name="answer">What it should render.</param>
    /// <param name="record">Where to note that there is no overlay, and why.</param>
    /// <returns>The overlay, or one that will quietly do nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overlays"/> is null.</exception>
    internal static Headset From(
        IHeadsetOverlayFactory overlays, Answer answer, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(overlays);

        Perhaps<IHeadsetOverlay> made = overlays.Create(answer) is { } overlay
            ? Perhaps<IHeadsetOverlay>.Of(overlay)
            : Perhaps<IHeadsetOverlay>.Absent(NoHeadsetOverlay);

        return new Headset(made.Or(record));
    }

    /// <summary>
    /// Puts the overlay up, if there is one.
    /// </summary>
    ///
    /// <remarks>
    /// Shown at startup rather than waiting to be asked, because until
    /// [#147](https://github.com/retiring-studios/directive-47/issues/147) gives
    /// the Commander a way to say so there is nothing to ask with — and a quad
    /// the compositor knows about but is not showing is the same outcome as no
    /// overlay, reached by more code.
    /// </remarks>
    internal void Show() => _overlay?.Show();

    /// <summary>
    /// Takes the overlay down, if there is one.
    /// </summary>
    internal void Hide() => _overlay?.Hide();

    /// <summary>
    /// Gives the quad back to the compositor.
    /// </summary>
    ///
    /// <remarks>
    /// An overlay left behind holds a slot in SteamVR's list of running
    /// applications, and one that outlives the process that made it is
    /// somebody's cockpit with a rectangle in it and nothing to close it.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _overlay?.Dispose();
    }
}
