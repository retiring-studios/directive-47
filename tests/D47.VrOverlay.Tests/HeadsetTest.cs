using System;
using System.Diagnostics.CodeAnalysis;

using Valve.VR;

namespace D47.VrOverlay.Tests;

/// <summary>
/// Base for every test that needs SteamVR actually running.
///
/// <para>
/// It fails rather than skips, which is the rule
/// <c>D47.GameOverlay.Tests</c> already follows and for the same reason: these
/// run in exactly one place — the dev PC, with the runtime up — and there is
/// nowhere else for them to be run later. A skip would be a green build that
/// checked nothing, which is the one result worse than a red one.
/// </para>
///
/// <para>
/// The runtime being installed is not the same fact as the runtime running, and
/// the message says which one is missing. <c>IsRuntimeInstalled</c> answers for
/// the first; only <c>VR_Init</c> answers for the second, so that is what
/// decides. A headset is deliberately not required — SteamVR runs perfectly well
/// with the Null driver, and an overlay is registered with the compositor
/// whether or not anybody is wearing anything.
/// </para>
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification =
        "The test classes that inherit this are public, as xUnit requires, and a public "
        + "class cannot derive from an internal one. The rule does not recognise this as a "
        + "test type because it holds no facts of its own.")]
public abstract class HeadsetTest : IDisposable
{
    /// <summary>
    /// Why a Tier 2 test failed, in the words somebody reading the output
    /// needs — including what to do about it, because a runtime that is not
    /// running is a fact about the machine rather than a defect in the code.
    /// </summary>
    internal const string NeedsSteamVr =
        "SteamVR is not running. These are Tier 2 tests: they prove Directive 47 puts a real "
        + "overlay in front of the real compositor, so there is no stand-in for it and nowhere "
        + "else to run them. Start SteamVR and run them again — a headset is not needed, the "
        + "Null driver is enough.";

    private bool _disposed;

    /// <summary>
    /// Joins SteamVR as an overlay application, or fails the test saying it is
    /// not there.
    /// </summary>
    ///
    /// <remarks>
    /// <c>VRApplication_Overlay</c> rather than <c>Scene</c>. An overlay
    /// application does not own the frame loop and does not displace whatever is
    /// already rendering it, which is the whole shape of this surface — Elite is
    /// the scene application, and Directive 47 floats over it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">SteamVR is not running.</exception>
    protected HeadsetTest()
    {
        EVRInitError failure = EVRInitError.None;

        OpenVR.Init(ref failure, EVRApplicationType.VRApplication_Overlay);

        if (failure != EVRInitError.None)
        {
            throw new InvalidOperationException(
                $"{NeedsSteamVr} (OpenVR said {failure}.)");
        }
    }

    /// <summary>
    /// Leaves the runtime again.
    /// </summary>
    ///
    /// <remarks>
    /// Every test, not once for the class. A process that stays joined holds a
    /// slot in the compositor's list of running applications, and a test run
    /// that leaves one behind is one somebody has to notice and clear.
    /// </remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Leaves the runtime, once.
    /// </summary>
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
