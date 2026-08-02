using D47.Capabilities;
using D47.GameOverlay;
using D47.Help;
using D47.Render;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// What the application does when the machine cannot host an overlay, and what
/// it does when the overlay is broken. Those are different things and this is
/// the only place that difference is asserted.
///
/// <para>
/// Tier 1 and in process. Nothing here needs the game, a desktop, or a window
/// — the whole point of putting a seam in front of the adapter is that the
/// decision can be tested without a machine that has been broken on purpose.
/// </para>
/// </summary>
public class OverlayTests
{
    [Fact]
    public void GameOverlay_WhenItCannotBeCreated_IsAbsentNotFailed()
    {
        var machineThatCannot = new FactoryThatRefuses();

        Should.NotThrow(() => Overlay.Show(machineThatCannot, HelpsAnswer()));

        // Asked, and not merely survived. Without this the test passes on any
        // machine with no game running, for a reason that has nothing to do
        // with what it claims to check.
        machineThatCannot.WasAsked.ShouldBeTrue(
            "the application should have asked for an overlay before deciding to do without one");
    }

    [Fact]
    public void GameOverlay_WhenCreationFailsForAnyOtherReason_SaysSoRatherThanGoingQuiet()
    {
        // The criterion that matters more than the one above it. "Absent, not
        // failed" is one try/catch away from reporting our own defects as an
        // unsupported machine, and this is what stops that being written.
        var broken = new FactoryThatIsBroken();

        Should.Throw<StandInFailure>(() => Overlay.Show(broken, HelpsAnswer()));
    }

    /// <summary>
    /// A machine that cannot composite, without breaking one.
    /// </summary>
    private sealed class FactoryThatRefuses : IGameOverlayFactory
    {
        internal bool WasAsked { get; private set; }

        public GameOverlayWindow? Create(Answer answer)
        {
            WasAsked = true;
            return null;
        }
    }

    /// <summary>
    /// An overlay with a defect in it, which is a different thing from a
    /// machine that cannot have one.
    /// </summary>
    private sealed class FactoryThatIsBroken : IGameOverlayFactory
    {
        public GameOverlayWindow? Create(Answer answer) => throw new StandInFailure();
    }

    private static Answer HelpsAnswer()
    {
        var registry = new CapabilityRegistry(HelpCapability.Descriptor);

        return new Answer
        {
            Descriptor = HelpCapability.Descriptor,
            Result = new HelpCapability(registry).Answer(),
        };
    }
}
