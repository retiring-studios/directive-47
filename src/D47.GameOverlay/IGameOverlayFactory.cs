using D47.Render;

namespace D47.GameOverlay;

/// <summary>
/// Makes the overlay, or says the machine cannot have one.
///
/// <para>
/// The seam exists so that "carry on without it" is a decision taken outside
/// this project. Adapters hold no logic; deciding what to do when a machine
/// cannot composite is logic, and it is asserted in CI against a stand-in
/// rather than on a machine somebody has broken on purpose.
/// </para>
/// </summary>
public interface IGameOverlayFactory
{
    /// <summary>
    /// Creates the overlay.
    /// </summary>
    /// <param name="presented">What the overlay should render.</param>
    /// <returns>
    /// The overlay, or <see langword="null"/> when this machine cannot host
    /// one.
    ///
    /// <para>
    /// Null means the machine, and only the machine — no compositor, no driver,
    /// nothing we can do anything about. Every other failure is thrown, because
    /// a defect of ours reported as an unsupported machine is a defect nobody
    /// ever finds.
    /// </para>
    /// </returns>
    IGameOverlay? Create(Presentation presented);
}
