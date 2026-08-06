using System;

using D47.Placement;
using D47.Render;

using Valve.VR;

namespace D47.VrOverlay;

/// <summary>
/// Makes the real overlay, on a machine with SteamVR running.
/// </summary>
public sealed class HeadsetOverlayFactory : IHeadsetOverlayFactory
{
    /// <summary>
    /// What SteamVR files the overlay under.
    ///
    /// <para>
    /// Reverse-domain and unique to this surface, because the key is the
    /// compositor's identity for it — two applications sharing one would be two
    /// applications fighting over the same quad. Public because it is how a
    /// test asks the runtime whether the overlay it can see is ours, and
    /// asserting through the compositor rather than through our own object is
    /// the whole point of a Tier 2 test.
    /// </para>
    /// </summary>
    public const string Key = "studios.retiring.directive47.panel";

    /// <summary>
    /// What SteamVR files the chrome under.
    ///
    /// <para>
    /// A second key for a second quad, because the compositor identifies an
    /// overlay by it and two sharing one would be two overlays fighting. Public
    /// for the same reason the panel's is: it is how a test asks the runtime
    /// whether the chrome it can see is ours.
    /// </para>
    /// </summary>
    public const string ChromeKey = "studios.retiring.directive47.chrome";

    /// <summary>
    /// What SteamVR shows a human, in its own settings and dashboards.
    /// </summary>
    private const string Name = "Directive 47";

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
    ///
    /// <para>
    /// Here rather than on <c>SteamVrOverlay</c>, which now carries two quads of
    /// different sizes and has no business preferring one of them.
    /// </para>
    /// </summary>
    private const float AboutAHandSpan = 0.5f;

    /// <summary>
    /// Where the panel's quad goes and how big it is, given what it will show.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The height comes from the render's own proportions rather than from a
    /// second constant. OpenVR is told a width and works the height out from the
    /// texture, so a height chosen here could disagree with what the compositor
    /// actually draws — and the chrome, which is placed from this, would frame a
    /// rectangle that is not there.
    /// </para>
    /// <para>
    /// Must be called on a thread WPF will talk to, because measuring means
    /// building a visual tree.
    /// </para>
    /// </remarks>
    /// <param name="presented">What the overlay will show.</param>
    /// <returns>The panel's quad.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="presented"/> is null.</exception>
    public static Board Quad(Presentation presented)
    {
        (int width, int height) = PanelRender.Measure(presented);

        return new Board(
            Resting.Place, AboutAHandSpan, AboutAHandSpan * height / width);
    }

    /// <summary>
    /// Creates the overlay, or reports that this machine cannot have one.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Must be called on a thread WPF will talk to, because getting the render
    /// means building a visual tree. That constraint comes from the render
    /// rather than from SteamVR.
    /// </para>
    /// <para>
    /// Joins the runtime only if nobody has. An application that is already an
    /// overlay application stays one, and a second <c>VR_Init</c> in a process
    /// that has one is a call with nothing to do — but making the call
    /// conditional is what keeps this usable from a test that joined first in
    /// order to fail early with a sentence about SteamVR not running.
    /// </para>
    /// <para>
    /// Leaving the runtime is deliberately not this project's business, for the
    /// reason <see cref="SteamVrOverlay.Dispose"/> gives.
    /// </para>
    /// </remarks>
    /// <param name="presented">What the overlay should render.</param>
    /// <returns>
    /// The overlay, or <see langword="null"/> when SteamVR is not there.
    /// </returns>
    public IHeadsetOverlay? Create(Presentation presented)
    {
        if (!Joined())
        {
            return null;
        }

        (byte[] pixels, int width, int height) = PanelRender.Take(presented);

        return SteamVrOverlay.Showing(Key, Name, Quad(presented), pixels, width, height);
    }

    /// <summary>
    /// Creates the chrome around a panel, or reports that this machine cannot
    /// have one.
    /// </summary>
    ///
    /// <remarks>
    /// Takes the panel's quad rather than working it out, so the chrome frames
    /// the rectangle that is actually there. Needs no STA thread: the chrome is
    /// flat rectangles and never builds a visual tree, which is what lets the
    /// thread watching the controllers own it.
    /// </remarks>
    /// <param name="panel">The panel's quad, from <see cref="Quad"/>.</param>
    /// <returns>
    /// The chrome, or <see langword="null"/> when SteamVR is not there.
    /// </returns>
    public static IGrabChrome? Around(Board panel) =>
        Joined() ? GrabChrome.Around(ChromeKey, $"{Name} chrome", panel) : null;

    /// <summary>
    /// Whether this process is an overlay application talking to a running
    /// SteamVR.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// <c>VRApplication_Overlay</c> rather than <c>Scene</c>. An overlay
    /// application does not own the frame loop and does not displace whatever
    /// is already rendering, which is the whole shape of this surface — Elite
    /// is the scene application and Directive 47 floats over it. Joining as a
    /// scene application would be Directive 47 asking to become the game.
    /// </para>
    /// <para>
    /// Asked, rather than discovered by catching what happens when the runtime
    /// is absent. That distinction is the one the game overlay's factory
    /// already draws: a <c>try</c>/<c>catch</c> around the construction would
    /// convert our own defects into "unsupported machine" alongside the genuine
    /// case. A precondition converts nothing.
    /// </para>
    /// </remarks>
    private static bool Joined()
    {
        if (OpenVR.System is not null)
        {
            return true;
        }

        EVRInitError failure = EVRInitError.None;

        OpenVR.Init(ref failure, EVRApplicationType.VRApplication_Overlay);

        return failure == EVRInitError.None && OpenVR.Overlay is not null;
    }
}
