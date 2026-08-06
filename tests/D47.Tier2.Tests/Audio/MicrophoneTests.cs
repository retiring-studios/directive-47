using System;
using System.Threading;

using D47.Audio;
using D47.VoiceLoop;

using Shouldly;

using Xunit;

namespace D47.Tier2.Tests.Audio;

/// <summary>
/// Capturing what the Commander said, from the microphone that is really there.
///
/// <para>
/// Tier 2: a microphone and nothing else. No SteamVR, so these do not derive
/// from <c>HeadsetTest</c> and do not take its collection — two tests that want
/// different scarce things should not queue behind each other.
/// </para>
///
/// <para>
/// What a turn does with the audio is Tier 0 and is already asserted in
/// <c>D47.Tier0.Tests.VoiceLoop.TurnTests</c> against a stand-in. The one fact
/// that needs real hardware is this: the recording is as long as the key was
/// held, rather than as long as some buffer happens to be.
/// </para>
/// </summary>
public class MicrophoneTests
{
    /// <summary>
    /// The shorter of the two holds. Long enough that WASAPI has delivered many
    /// buffers rather than one or two, and short enough that the test is not
    /// something anybody waits through.
    /// </summary>
    private static readonly TimeSpan ShortHold = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The longer hold, a second on top of the shorter one. A whole second
    /// because the thing being measured is the difference, and a difference has
    /// to be large next to the jitter in when a capture thread actually stops.
    /// </summary>
    private static readonly TimeSpan LongHold = ShortHold + TimeSpan.FromSeconds(1);

    /// <summary>
    /// What to do when the machine has no microphone at all. A fact about the
    /// machine rather than a defect, said the way the other Tier 2 and Tier 3
    /// gates say it.
    /// </summary>
    private const string NeedsAMicrophone =
        "No capture device is available. These are Tier 2 tests: they prove Directive 47 "
        + "records for as long as the key is held, so there is no stand-in for a real "
        + "microphone. Plug one in, or enable one in Sound settings, and run them again.";

    [Fact]
    public void PushToTalk_WhileHeld_CapturesAudioForTheDurationOfTheHold()
    {
        Microphone.IsAvailable().ShouldBeTrue(NeedsAMicrophone);

        TimeSpan brief = Held(ShortHold);
        TimeSpan longer = Held(LongHold);

        // The difference rather than either length on its own, which is what
        // makes this a claim about the hold. Opening a capture costs a fixed
        // and unpredictable amount of time before the first buffer arrives —
        // measure one hold against the clock and that cost is inside the
        // tolerance, so a capture that always recorded exactly one second would
        // pass. Measure two and it cancels.
        TimeSpan extra = longer - brief;
        TimeSpan asked = LongHold - ShortHold;

        extra.TotalMilliseconds.ShouldBe(
            asked.TotalMilliseconds,
            tolerance: 150,
            customMessage:
                $"holding for {asked.TotalMilliseconds}ms longer should record about that much "
                + $"more audio, but {brief.TotalMilliseconds}ms and {longer.TotalMilliseconds}ms "
                + "differ by something else");

        // And that neither is a rounding error away from nothing. The check
        // above is a ratio and a capture returning almost no audio at both
        // lengths would satisfy it while recording nothing anybody could
        // transcribe.
        brief.ShouldBeGreaterThan(
            TimeSpan.FromMilliseconds(250),
            "half a second of holding should record something like half a second of audio");
    }

    /// <summary>
    /// Holds the key for a while and says how much audio came back.
    /// </summary>
    ///
    /// <remarks>
    /// The length is worked out from the bytes and the device's own format
    /// rather than timed with a stopwatch. A stopwatch would measure how long
    /// the test slept, which is the thing being checked against.
    /// </remarks>
    /// <param name="hold">How long to hold the key.</param>
    /// <returns>How much audio was captured, as time.</returns>
    private static TimeSpan Held(TimeSpan hold)
    {
        using var microphone = new Microphone();

        microphone.Open();

        Thread.Sleep(hold);

        Captured captured = microphone.Close();

        return microphone.HowLong(captured);
    }
}
