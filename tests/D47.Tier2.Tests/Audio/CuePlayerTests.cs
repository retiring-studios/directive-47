using System;
using System.Threading;

using D47.Audio;
using D47.VoiceLoop;

using Shouldly;

using Xunit;

namespace D47.Tier2.Tests.Audio;

/// <summary>
/// The cues reaching a real output device.
///
/// <para>
/// Tier 2, and only just: which cue a state gets and whether the file is
/// actually embedded are Tier 1 and asserted in CI. What is left here is the one
/// thing a hosted runner cannot answer — that a WAV shipped inside the assembly
/// opens on this machine's mixer and starts playing.
/// </para>
///
/// <para>
/// It makes a noise. That is unavoidable and it is short.
/// </para>
/// </summary>
public class CuePlayerTests
{
    [Fact]
    public void ACue_ForAStateWithSounds_ReachesTheDevice()
    {
        var events = new Events();
        using var player = CuePlayer.Following(events, Random.Shared);

        // Through the bus rather than by calling something directly, because
        // subscribing correctly is half of what this type does and a test that
        // reached past it would not notice the subscription being dropped.
        Should.NotThrow(
            () => events.Publish(new Entered(TurnState.Listening)),
            "a state with a cue should play it rather than throw at the device");

        // Long enough for the mixer to have been handed the buffer and started.
        // Nothing here asserts the sound was audible — no test can — only that
        // the whole path from embedded bytes to a running device holds.
        Thread.Sleep(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void ACue_ForAStateWithNone_TouchesNoDeviceAtAll()
    {
        var events = new Events();
        using var player = CuePlayer.Following(events, Random.Shared);

        // Speaking has no cue, and the path for that has to stop before it asks
        // the machine for an output device. On a machine with no speakers this
        // is the difference between silence and a crash.
        Should.NotThrow(() => events.Publish(new Entered(TurnState.Speaking)));
    }

    [Fact]
    public void Cues_FollowingOneAnotherQuickly_LeaveOnlyTheLastPlaying()
    {
        var events = new Events();
        using var player = CuePlayer.Following(events, Random.Shared);

        // A whole turn's worth of transitions with no gap, which is what a fast
        // provider looks like. Each one stops the one before it, and the thing
        // that would break is disposing a device the mixer is still reading —
        // so this is a test that passes by not crashing.
        Should.NotThrow(() =>
        {
            events.Publish(new Entered(TurnState.Listening));
            events.Publish(new Entered(TurnState.Transcribing));
            events.Publish(new Entered(TurnState.Thinking));
            events.Publish(new Entered(TurnState.Speaking));
            events.Publish(new Entered(TurnState.Idle));
        });

        Thread.Sleep(TimeSpan.FromMilliseconds(250));
    }
}
