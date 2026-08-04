using D47.Render;

namespace D47.VrOverlay;

/// <summary>
/// Makes the overlay in the headset, or says this machine cannot have one.
///
/// <para>
/// The seam exists so that "carry on without it" is a decision taken outside
/// this project. Adapters hold no logic; deciding what to do on a machine with
/// no SteamVR is logic, and it is asserted in CI against a stand-in rather than
/// on a machine somebody has uninstalled a runtime from. That story is
/// [#136](https://github.com/retiring-studios/directive-47/issues/136).
/// </para>
/// </summary>
public interface IHeadsetOverlayFactory
{
    /// <summary>
    /// Creates the overlay.
    /// </summary>
    /// <param name="presented">What the overlay should render.</param>
    /// <returns>
    /// The overlay, which the caller owns and must dispose, or
    /// <see langword="null"/> when this machine cannot host one.
    ///
    /// <para>
    /// Null means the machine, and only the machine — no runtime installed, or
    /// one that is not running. Every other failure is thrown, because a defect
    /// of ours reported as an unsupported machine is a defect nobody ever finds.
    /// This is the same contract <c>IGameOverlayFactory</c> already has, and
    /// deliberately so: two adapters that presented "cannot" in two different ways
    /// would need two decisions outside them.
    /// </para>
    /// </returns>
    IHeadsetOverlay? Create(Presentation presented);
}
