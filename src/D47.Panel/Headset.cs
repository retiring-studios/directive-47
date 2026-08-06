using System;

using D47.Placement;
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

    /// <summary>
    /// Where the quad is, for whatever needs to frame it or point at it.
    /// </summary>
    ///
    /// <remarks>
    /// Absent on a machine with no runtime, which is the same absence the
    /// overlay itself is — there is no quad, so there is nowhere to point.
    /// </remarks>
    internal Board? Placed => _overlay?.Placed;

    /// <summary>
    /// What the quad is currently carrying, so that a new presented can be
    /// compared with it rather than drawn over it.
    /// </summary>
    private Presentation _showing;

    private bool _disposed;

    private Headset(IHeadsetOverlay? overlay, Presentation showing)
    {
        _overlay = overlay;
        _showing = showing;
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
    /// <param name="presented">What it should render.</param>
    /// <param name="record">Where to note that there is no overlay, and why.</param>
    /// <returns>The overlay, or one that will quietly do nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overlays"/> is null.</exception>
    internal static Headset From(
        IHeadsetOverlayFactory overlays, Presentation presented, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(overlays);

        Perhaps<IHeadsetOverlay> made = overlays.Create(presented) is { } overlay
            ? Perhaps<IHeadsetOverlay>.Of(overlay)
            : Perhaps<IHeadsetOverlay>.Absent(NoHeadsetOverlay);

        return new Headset(made.Or(record), presented);
    }

    /// <summary>
    /// Shows a new presented, if it is a different one.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Both halves of one decision. An overlay showing a stale frame is
    /// confidently wrong about what the Commander is looking at; an overlay
    /// repainting a texture nothing has changed spends a headset's frame budget
    /// on nothing, and the headset is where that budget is scarcest.
    /// </para>
    /// <para>
    /// Driven by the change rather than by a clock, which is what makes the
    /// second half true at all — a timer would repaint an unchanged presented
    /// forever and no test could tell it was wrong.
    /// </para>
    /// <para>
    /// Comparing answers only became possible when they started comparing as
    /// values ([#200](https://github.com/retiring-studios/directive-47/issues/200)).
    /// Before that a record holding a list compared it by identity, so two
    /// answers with the same content were different and this would have
    /// repainted on every turn.
    /// </para>
    /// </remarks>
    /// <param name="presented">What to show.</param>
    internal void Showing(Presentation presented)
    {
        if (_showing == presented)
        {
            return;
        }

        _showing = presented;

        _overlay?.Paint(presented);
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
