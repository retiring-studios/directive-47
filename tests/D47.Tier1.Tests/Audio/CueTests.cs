using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using D47.Audio;
using D47.VoiceLoop;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Audio;

/// <summary>
/// Which sound a state is worth, and picking one when there is more than one.
///
/// <para>
/// Tier 1 rather than Tier 2, because none of this opens a device. What is
/// asserted here is which cues exist and which one gets chosen; that the chosen
/// one actually reaches a speaker is <c>D47.Tier2.Tests</c>, and it is the only
/// part that needs hardware.
/// </para>
/// </summary>
public class CueTests
{
    [Fact]
    public void TheStates_WithSomethingToPlay_AreTheOnesWithAFolder()
    {
        // Named rather than counted, and first rather than last. Every other
        // test here walks EveryStateWithASound — so if the embedded resource
        // prefix ever drifts from what the project file's Link produces, that
        // collection goes empty and all of them pass having asserted nothing.
        // This is the one that goes red instead.
        Cues.EveryStateWithASound.ShouldBe(
            [TurnState.Listening, TurnState.Transcribing, TurnState.Thinking, TurnState.Idle],
            ignoreOrder: true);
    }

    [Fact]
    public void SoundCue_OnEnteringAState_PlaysFromThatStatesRandomizedSet()
    {
        // Every pick, many times over, comes from the set for that state and
        // eventually covers all of it. Both halves matter: one that returned
        // something outside the set would be reaching for a file that is not
        // there, and one that always returned the first would be a set in name
        // only.
        foreach (TurnState state in Cues.EveryStateWithASound)
        {
            IReadOnlyList<string> set = Cues.For(state);

            set.ShouldNotBeEmpty($"{state} is listed as having a sound");

            HashSet<string> picked = [];

            // Enough tries that missing a member is not chance. With three in
            // the largest set, forty draws miss one about as often as never.
            for (int attempt = 0; attempt < 40; attempt++)
            {
                string chosen = Cues.Pick(state, Random.Shared).ShouldNotBeNull();

                set.ShouldContain(chosen, $"{state} chose a sound that is not in its set");

                picked.Add(chosen);
            }

            picked.ShouldBe(
                set.ToHashSet(),
                ignoreOrder: true,
                customMessage:
                    $"forty draws from {state} should have reached all {set.Count} of them");
        }
    }

    [Fact]
    public void AState_WithNothingToSay_IsSilentRatherThanAnError()
    {
        // Speaking is the case this exists for, and it is deliberate rather than
        // an asset nobody got round to: the reply is the sound. A cue underneath
        // it would be talking over the answer the Commander asked for.
        Cues.For(TurnState.Speaking).ShouldBeEmpty();

        Cues.Pick(TurnState.Speaking, Random.Shared).ShouldBeNull();
    }

    [Fact]
    public void EveryCue_ItNames_IsActuallyThere()
    {
        // The one that catches a rename, a typo, or a file that never got added
        // to the project. The names are strings resolved at run time, so nothing
        // else would notice until a Commander entered that state and heard
        // silence.
        foreach (TurnState state in Cues.EveryStateWithASound)
        {
            foreach (string cue in Cues.For(state))
            {
                using Stream? sound = Cues.Open(cue);

                sound.ShouldNotBeNull($"{cue} is named for {state} but is not embedded");

                sound.Length.ShouldBeGreaterThan(
                    0, $"{cue} is embedded but empty, which is silence with extra steps");
            }
        }
    }
}
