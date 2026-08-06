using System.Collections.Generic;

using D47.Placement;
using D47.VrOverlay;

using Shouldly;

using Valve.VR;

using Xunit;

namespace D47.Tier2.Tests.VrOverlay;

/// <summary>
/// Reading where the Commander's hands are, from the runtime that knows.
///
/// <para>
/// Tier 2: it needs SteamVR up, and nothing else. Not a headset and not
/// controllers — what a machine with neither proves is that an empty answer is
/// an ordinary one, which is the case a Commander is in for most of a session
/// and the one an implementation is most likely to get wrong.
/// </para>
///
/// <para>
/// What a pose <em>means</em> is not here. That is <c>Chrome</c> in
/// <c>D47.Placement</c>, it is Tier 0, and it is asserted in CI against numbers.
/// </para>
/// </summary>
public class ControllerTests : HeadsetTest
{
    [Fact]
    public void Controllers_WithTheRuntimeRunning_Answer()
    {
        // Whatever is plugged in. The point is that asking is safe on a machine
        // in whatever state this one happens to be in — the runtime hands back a
        // full array of sixty-four slots either way, and most of them are nothing.
        IReadOnlyList<Pose> held = new Controllers().Tracked();

        held.ShouldNotBeNull();
    }

    [Fact]
    public void EveryControllerAnsweredWith_IsAPoseThePlacementLibraryAccepts()
    {
        // The fact that matters, and the reason the validity checks are in the
        // adapter. An untracked slot is all zeroes, and all zeroes reads back as
        // a unit rotation at the origin — perfectly valid-looking and completely
        // wrong. If the filtering is dropped, this is what notices, because a
        // board built on such a pose is one Chrome will happily answer about.
        foreach (Pose held in new Controllers().Tracked())
        {
            // The pose put where a board's own pose goes, which is what puts it
            // in front of the finite-number checks. Chrome validates the surface
            // it is asked about rather than anything aimed at it.
            Should.NotThrow(
                () => Chrome.On(new Board(held, 0.5f, 0.3f), 0, 0),
                "a controller the adapter handed on should be one the arithmetic accepts");

            // Not at the origin facing backwards, which is exactly what an
            // unfiltered empty slot looks like. A real controller can be
            // anywhere, but it is not there to four decimal places.
            held.Position.Length().ShouldBeGreaterThan(
                0.0001f,
                "a controller at the exact origin is an empty slot that got through");
        }
    }

    [Fact]
    public void Controllers_AreNotTheHeadsetOrAnythingElseTheRuntimeTracks()
    {
        // The headset is always device zero and is always tracked while SteamVR
        // runs, so if the class filter were dropped this would find it. It is the
        // one device this machine is guaranteed to have.
        CVRSystem runtime = OpenVR.System;

        runtime.ShouldNotBeNull();

        var everything = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        runtime.GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin.TrackingUniverseSeated, 0, everything);

        int hands = 0;
        int devices = 0;

        for (uint device = 0; device < everything.Length; device++)
        {
            if (!everything[device].bPoseIsValid || !everything[device].bDeviceIsConnected)
            {
                continue;
            }

            devices++;

            if (runtime.GetTrackedDeviceClass(device) == ETrackedDeviceClass.Controller)
            {
                hands++;
            }
        }

        // Exactly the controllers, counted the same way but independently. An
        // earlier version of this asserted only that we answer with fewer
        // devices than the runtime tracks, which is a weaker claim and a fragile
        // one — with the headset off and both controllers asleep the runtime
        // tracks nothing, and "fewer than nothing" fails for a reason that has
        // nothing to do with the code.
        new Controllers().Tracked().Count.ShouldBe(
            hands, "the answer should be the controllers, all of them and only them");

        // And the filter is doing something rather than passing everything
        // through, which the count alone cannot tell you on a machine where the
        // only tracked device happens to be a controller.
        if (devices > hands)
        {
            hands.ShouldBeLessThan(
                devices, "the headset and the base stations are not hands");
        }
    }
}
