using System;
using System.Threading;

using D47.Render;
using D47.TestSupport;

using Xunit;

namespace D47.VrOverlay.Tests;

/// <summary>
/// Puts the overlay up and leaves it there, so a person can look at it.
///
/// <para>
/// This asserts nothing, and that is deliberate. SteamVR will not answer for a
/// raw overlay's pixels — <c>GetOverlayTextureSize</c> says
/// <c>InvalidHandle</c> and <c>GetOverlayImageData</c> says
/// <c>InvalidTexture</c>, both having been tried — so whether the render
/// actually arrived on the quad is a question only eyes can settle. Delete the
/// <c>SetOverlayRaw</c> call and every asserting test in this project still
/// passes.
/// </para>
///
/// <para>
/// It exists because the manual step it replaces could not be carried out. The
/// pull request originally said to run
/// <see cref="HeadsetOverlayTests"/> with the headset on, which was written
/// without noticing that the whole class finishes in under a second — the quad
/// was on screen for about as long as a blink and nobody could have seen it.
/// A check somebody cannot perform is not a check.
/// </para>
///
/// <para>
/// Opt-in, like the desktop tests, and for the same reason: it takes the
/// headset for half a minute and has no business running when somebody merely
/// typed <c>dotnet test</c>. Set <c>D47_VR_LOOK=1</c>.
/// </para>
/// </summary>
public class LookAtTheOverlayTests : HeadsetTest
{
    /// <summary>
    /// What opts in.
    /// </summary>
    private const string TheOptIn = "D47_VR_LOOK";

    /// <summary>
    /// Long enough to put a headset on, find the quad, and read it — not so
    /// long that somebody who started it by accident is stuck watching.
    /// </summary>
    private static readonly TimeSpan LongEnoughToLook = TimeSpan.FromSeconds(30);

    private static readonly string NotAsked =
        $"Set {TheOptIn}=1 to have the overlay held up for "
        + $"{LongEnoughToLook.TotalSeconds:0} seconds to be looked at. It asserts nothing and "
        + $"takes the headset for as long as it runs.";

    [Fact]
    public void Overlay_WhenSomebodyIsWatching_StaysUpLongEnoughToBeRead()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable(TheOptIn) == "1", NotAsked);

        Answer answer = Fixtures.HelpsAnswer();

        using IHeadsetOverlay overlay = StaThread.Run(
            () => new HeadsetOverlayFactory().Create(answer))
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        overlay.Show();

        // The whole of it. What a person is looking for is on the pull request:
        // a dark grey panel with orange text, readable, the right way up, solid
        // to its edges. Blank is the failure that matters, because that is the
        // render never having arrived and nothing else would ever say so.
        Thread.Sleep(LongEnoughToLook);
    }
}
