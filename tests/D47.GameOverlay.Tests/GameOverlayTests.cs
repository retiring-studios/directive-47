using D47.Capabilities;
using D47.Help;
using D47.Render;

using Shouldly;

using Xunit;

namespace D47.GameOverlay.Tests;

/// <summary>
/// The overlay over the real game. Everything here is Tier 3: it needs Elite
/// running in borderless windowed mode, and there is no stand-in for the game
/// that would make the claim mean anything.
/// </summary>
public class GameOverlayTests : GameTest
{
    private readonly ITestOutputHelper _output;

    public GameOverlayTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GameOverlay_WhenShown_DrawsTheSharedRenderOverTheGame()
    {
        using var overlay = RunningOverlay.ShownOver(Game, HelpsAnswer());

        // Read once, while the overlay is up. Asking again from inside a
        // failure message would describe the stack at the moment of the
        // assertion, which is a different desktop from the one being asserted
        // about if anything moved in between.
        string stack = Screen.DescribeStack();
        _output.WriteLine(stack);

        // The shared render. Help's top level, read off the visual tree the
        // overlay built — the same text the panel's own tests read off the
        // same control. One group, because help is currently the only
        // registered capability.
        overlay.VisibleText().ShouldBe(["Getting around"]);

        // Over the game, in both senses. Inside its rectangle, so it is drawn
        // on the game rather than beside it; and in front of it in the window
        // stack, which is Windows' opinion rather than the overlay's own.
        Game.Bounds.Contains(overlay.Bounds).ShouldBeTrue(
            $"The overlay is at {overlay.Bounds} and the game is at {Game.Bounds}.");

        Screen.DepthOf(overlay.Handle).ShouldBeLessThan(
            Screen.DepthOf(Game.Handle),
            $"The overlay should be in front of the game.{stack}");
    }

    /// <summary>
    /// Help's top-level answer, composed the way the panel composes it. Help is
    /// the only capability that exists, and it needs no microphone, no network
    /// and no game — which matters here, because everything else in this test
    /// does need the game and the render must not.
    /// </summary>
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
