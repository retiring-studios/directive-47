using System.Collections.Generic;

using D47.Panel;
using D47.VoiceLoop;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Which microphone the application ends up holding.
///
/// <para>
/// Tier 1, and the branch that matters here is the one with no device — a real
/// <c>D47.Audio.Microphone</c> cannot be built on a machine without one, which is
/// exactly the case this exists to survive. That the real one records is
/// <c>D47.Tier2.Tests</c>, and it needs the machine.
/// </para>
/// </summary>
public class ChosenMicrophoneTests
{
    [Fact]
    public void AMachine_WithNoCaptureDevice_GetsAStandInRatherThanACrash()
    {
        List<string> recorded = [];

        IMicrophone chosen = ChosenMicrophone.Available(() => false, recorded.Add);

        // Opening and closing it has to be safe, because the turn will do
        // exactly that the first time the Commander holds the key. This is the
        // whole point: no microphone is a state a Commander is allowed to be
        // in — see #228 — and the loop still has to run to the end.
        Should.NotThrow(() =>
        {
            chosen.Open();
            chosen.Close();
        });

        recorded.ShouldHaveSingleItem()
            .ShouldContain("microphone", Case.Insensitive);
    }

    [Fact]
    public void AMachine_WithNoCaptureDevice_SaysSoOnceRatherThanEveryTurn()
    {
        List<string> recorded = [];

        IMicrophone chosen = ChosenMicrophone.Available(() => false, recorded.Add);

        for (int turn = 0; turn < 5; turn++)
        {
            chosen.Open();
            chosen.Close();
        }

        // The choice is made once at startup, so the warning is written once.
        // A line per turn would bury a real problem under the same sentence
        // repeated for as long as the session lasts.
        recorded.Count.ShouldBe(1);
    }

    [Fact]
    public void TheLookup_IsAskedOnce_RatherThanEveryTimeSomethingWantsIt()
    {
        int asked = 0;

        ChosenMicrophone.Available(
            () => { asked++; return false; },
            _ => { });

        // Enumerating audio endpoints is a call into the operating system, and
        // this sits on the path that opens the microphone when the key goes
        // down — which #7 says is the one place nothing may be spent.
        asked.ShouldBe(1);
    }
}
