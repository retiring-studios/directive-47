using System.Collections.Generic;

using D47.GameOverlay;
using D47.Placement;
using D47.VrOverlay;

using Shouldly;

using Valve.VR;

using Xunit;

namespace D47.Tier3.Tests.VrOverlay;

/// <summary>
/// Reading where the Commander's hands are while they are flying.
///
/// <para>
/// Tier 3, and the premise the whole of
/// <see href="https://github.com/retiring-studios/directive-47/issues/235">#235</see>
/// rests on: Elite does not bind the motion controllers, so an overlay
/// application can read them. That is a claim about the game, and the only place
/// it can be settled is on a machine with the game running.
/// </para>
///
/// <para>
/// Tier 2's <c>ControllerTests</c> already proves the adapter filters slots
/// correctly and that an empty answer is an ordinary one. None of that is
/// repeated here. What is here is the one fact that needs Elite.
/// </para>
/// </summary>
public class ControllerTests : GameAndHeadsetTest
{
    /// <summary>
    /// What to do when the game and the runtime are both up but the hands are
    /// on the desk. A fact about the machine rather than a defect, the same as
    /// the game not running, and said the same way.
    /// </summary>
    private const string NeedsHands =
        "No controller is tracked. This test asserts that the hands stay readable while Elite "
        + "holds the scene, and there is nothing to read — wake both controllers, hold them in "
        + "front of the headset until the runtime sees them, and run it again.";

    [Fact]
    public void Controllers_WhenPresent_AreTrackedWhileEliteHoldsTheScene()
    {
        // Not "Elite is running", which the base already settled from the
        // process list. This is the stronger fact and the one that matters: the
        // game is what SteamVR is rendering, so anything read below is read from
        // underneath it rather than from a runtime sitting idle at the
        // dashboard.
        uint scene = OpenVR.Applications.GetCurrentSceneProcessId();

        EliteWindow.IsTheGame(scene).ShouldBeTrue(
            "Elite is running but is not the scene application — start it in VR, because a "
            + "flat-mode game proves nothing about what an overlay can read");

        IReadOnlyList<Pose> held = new Controllers().Tracked();

        held.ShouldNotBeEmpty(NeedsHands);

        foreach (Pose hand in held)
        {
            // The claim, one hand at a time. A controller Elite had claimed
            // would not vanish from the pose list — poses need no input focus —
            // so the failure this guards against is the runtime handing back
            // untracked slots once a scene application owns the session, which
            // reads as a hand at the Commander's feet rather than as an error.
            hand.Position.Length().ShouldBeGreaterThan(
                0.0001f,
                "a hand at the exact origin is an untracked slot, not a controller");
        }
    }
}
