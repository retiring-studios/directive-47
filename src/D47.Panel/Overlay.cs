using D47.GameOverlay;
using D47.Render;

namespace D47.Panel;

/// <summary>
/// The game overlay, as the application deals with it: ask for one, and put it
/// over the game if there is a game to put it over.
///
/// <para>
/// Deciding to carry on without an overlay lives here rather than in
/// <c>D47.GameOverlay</c>, because it is logic and that project is an adapter.
/// It is also what makes the decision testable in CI: a stand-in can refuse to
/// be created, where a real machine cannot be asked to stop compositing.
/// </para>
/// </summary>
internal static class Overlay
{
    /// <summary>
    /// Shows the overlay, unless this machine cannot have one.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The machine is asked before the game is looked for, which is the
    /// opposite of the order that saves work. Two reasons, and neither is
    /// performance. Whether a machine can host an overlay has nothing to do
    /// with whether Elite happens to be running, so checking the game first
    /// would make the answer depend on something unrelated to the question —
    /// and it would leave this method untested on any machine without the game,
    /// because nothing would ever reach the factory.
    /// </para>
    /// <para>
    /// Nothing is caught here. A factory that throws is a defect in the
    /// overlay, and a defect reported as an unsupported machine is a defect
    /// nobody ever finds. The only thing this understands is
    /// <see langword="null"/>.
    /// </para>
    /// </remarks>
    /// <param name="overlays">Where an overlay comes from.</param>
    /// <param name="answer">What it should render.</param>
    internal static void Show(IGameOverlayFactory overlays, Answer answer)
    {
        if (overlays.Create(answer) is not { } overlay)
        {
            return;
        }

        if (EliteWindow.Find() is { } game)
        {
            overlay.ShowOver(game);
        }
    }
}
