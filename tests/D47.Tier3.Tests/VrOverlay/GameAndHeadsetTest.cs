using System;
using System.Diagnostics.CodeAnalysis;

using D47.Tier3.Tests.GameOverlay;

using Valve.VR;

using Xunit;

namespace D47.Tier3.Tests.VrOverlay;

/// <summary>
/// Base for every test that needs Elite Dangerous and SteamVR at once.
///
/// <para>
/// Tier 2's <c>HeadsetTest</c> proves what the runtime answers with nothing else
/// running. This proves what it answers while the game holds the scene, which is
/// the only state a Commander is ever actually in and the one no runner without
/// a game on it can reach.
/// </para>
///
/// <para>
/// It fails rather than skips, twice over, for the reason both tiers already
/// give: these run in one place and there is nowhere else for them to be run
/// later. The two gates are separate because the two facts are — SteamVR up with
/// no game is Tier 2's world, and it is not this one.
/// </para>
/// </summary>
[Collection(GameTest.Collection)]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification =
        "The test classes that inherit this are public, as xUnit requires, and a public "
        + "class cannot derive from an internal one. The rule does not recognise this as a "
        + "test type because it holds no facts of its own.")]
public abstract class GameAndHeadsetTest : GameTest, IDisposable
{
    /// <summary>
    /// Why one of these failed when the game is up but the runtime is not.
    /// </summary>
    internal const string NeedsSteamVrToo =
        "SteamVR is not running, although Elite is. These tests prove the runtime still "
        + "answers while the game holds the scene, so both have to be up. Start SteamVR and "
        + "run them again.";

    private bool _disposed;

    /// <summary>
    /// Finds the game, then joins SteamVR as an overlay application.
    /// </summary>
    ///
    /// <remarks>
    /// <c>VRApplication_Overlay</c>, the same as Tier 2 and for the same reason,
    /// and here it matters in a way it does not there: Elite is already the scene
    /// application, and joining as <c>Scene</c> would be this test asking to
    /// replace the very thing it is checking we sit alongside.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Elite is not running, or SteamVR is not.
    /// </exception>
    protected GameAndHeadsetTest()
    {
        EVRInitError failure = EVRInitError.None;

        OpenVR.Init(ref failure, EVRApplicationType.VRApplication_Overlay);

        if (failure != EVRInitError.None)
        {
            throw new InvalidOperationException($"{NeedsSteamVrToo} (OpenVR said {failure}.)");
        }
    }

    /// <summary>
    /// Leaves the runtime again.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Leaves the runtime, once.
    /// </summary>
    ///
    /// <remarks>
    /// Every test rather than once for the class, the same as Tier 2: a process
    /// that stays joined holds a slot in the compositor's list of running
    /// applications, and one left behind is one somebody has to notice and clear.
    /// </remarks>
    /// <param name="disposing">Whether this came from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _disposed = true;

        OpenVR.Shutdown();
    }
}
