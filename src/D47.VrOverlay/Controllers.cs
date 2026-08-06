using System.Collections.Generic;

using D47.Placement;

using Valve.VR;

namespace D47.VrOverlay;

/// <summary>
/// Where the Commander's hands are.
/// </summary>
///
/// <remarks>
/// An interface because the thing that watches them runs a loop and decides what
/// it means, and neither of those needs a runtime to be asserted. What is behind
/// this needs SteamVR; what reads it does not.
/// </remarks>
public interface IControllers
{
    /// <summary>
    /// Every controller the runtime is currently sure about.
    /// </summary>
    ///
    /// <remarks>
    /// Empty is an ordinary answer and the common one — both controllers asleep
    /// on the desk is most of a session. It is not a failure and nothing should
    /// report it as one.
    /// </remarks>
    /// <returns>Their poses, in no particular order.</returns>
    IReadOnlyList<Pose> Tracked();

    /// <summary>
    /// One controller, named the way the runtime names it.
    /// </summary>
    ///
    /// <remarks>
    /// By device rather than by hand, because a grab has to keep following the
    /// controller that started it and SteamVR identifies that one by the index
    /// on the event. The index means nothing on its own — which slot a
    /// controller lands in depends on the order they woke up — so it is only
    /// ever useful carried back from something the runtime said.
    /// </remarks>
    /// <param name="device">The slot the runtime used.</param>
    /// <returns>
    /// Its pose, or <see langword="null"/> when that slot holds nothing tracked.
    /// </returns>
    Pose? At(uint device);
}

/// <summary>
/// The controllers, as SteamVR has them.
/// </summary>
///
/// <remarks>
/// <para>
/// Poses only. Reading where a controller <em>is</em> needs no action manifest
/// and no input focus; only reading a button does. That is what lets this exist
/// before the manifest question of
/// [#235](https://github.com/retiring-studios/directive-47/issues/235) has to be
/// answered.
/// </para>
/// <para>
/// Seated space, because that is the origin the overlay is placed in. Two
/// origins would put the hand and the thing it is pointing at in different
/// worlds, and the arithmetic between them would be quietly wrong rather than
/// loudly broken.
/// </para>
/// </remarks>
public sealed class Controllers : IControllers
{
    /// <summary>
    /// How far ahead to ask for. Nothing, because this is used to decide what a
    /// Commander is pointing at right now rather than to draw a frame at a known
    /// time — and a prediction would put the answer somewhere their hand has not
    /// been yet.
    /// </summary>
    private const float RightNow = 0;

    /// <inheritdoc/>
    public IReadOnlyList<Pose> Tracked()
    {
        if (OpenVR.System is not { } runtime)
        {
            return [];
        }

        var found = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        runtime.GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin.TrackingUniverseSeated, RightNow, found);

        List<Pose> held = [];

        for (uint device = 0; device < found.Length; device++)
        {
            if (runtime.GetTrackedDeviceClass(device) != ETrackedDeviceClass.Controller)
            {
                continue;
            }

            // Both checks, and they are not the same question: a controller can
            // be connected and not yet tracked, and a slot can hold a device
            // that has gone away.
            //
            // This is the only place either is asked, because nothing downstream
            // can. An untracked slot is all zeroes, and all zeroes reads back as
            // a unit rotation at the origin rather than as anything invalid —
            // so a missed check here becomes a controller that appears to be
            // sitting at the Commander's feet pointing backwards. See
            // QuadTests.APoseRead_FromAnEmptySlot_LooksPerfectlyValid.
            if (!found[device].bPoseIsValid || !found[device].bDeviceIsConnected)
            {
                continue;
            }

            held.Add(Quad.From(found[device].mDeviceToAbsoluteTracking));
        }

        return held;
    }

    /// <inheritdoc/>
    public Pose? At(uint device)
    {
        if (OpenVR.System is not { } runtime
            || device >= OpenVR.k_unMaxTrackedDeviceCount)
        {
            return null;
        }

        var found = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        runtime.GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin.TrackingUniverseSeated, RightNow, found);

        // The same two checks Tracked makes, and for the same reason: an
        // untracked slot is all zeroes, and all zeroes reads back as a unit
        // rotation at the origin rather than as anything invalid. Missing it
        // here would drag a grabbed overlay to the Commander's feet the moment a
        // controller went to sleep.
        if (!found[device].bPoseIsValid || !found[device].bDeviceIsConnected)
        {
            return null;
        }

        // Not asked whether it is a controller. The caller got this index off an
        // event the runtime raised about its own pointer, so second-guessing what
        // kind of device that was would be this code disagreeing with SteamVR
        // about which thing is doing the pointing.
        return Quad.From(found[device].mDeviceToAbsoluteTracking);
    }
}
