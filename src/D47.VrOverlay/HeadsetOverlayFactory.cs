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
    /// What SteamVR shows a human, in its own settings and dashboards.
    /// </summary>
    private const string Name = "Directive 47";

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

        return SteamVrOverlay.Showing(Key, Name, pixels, width, height);
    }

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
