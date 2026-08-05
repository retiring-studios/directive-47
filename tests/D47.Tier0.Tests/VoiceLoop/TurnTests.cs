using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using D47.VoiceLoop;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.VoiceLoop;

/// <summary>
/// One turn, end to end, with stand-ins where the transcriber, the model and the
/// voice will go.
///
/// <para>
/// Tier 0 on purpose. The turn's shape is the thing stage C's three
/// implementations are written against, so it is asserted here with no
/// microphone, no network and no headset — which is also what makes it a fact
/// CI can hold on every pull request.
/// </para>
/// </summary>
public class TurnTests
{
    [Fact]
    public async Task PushToTalk_WhenHeldAndReleased_RunsATurnAndSpeaksTheReply()
    {
        var voice = new StandInVoice();
        var turn = new Turn(
            new StandInMicrophone(),
            new StandInTranscriber("what is my fuel"),
            new StandInModel("Seventy percent."),
            voice,
            new Events());

        turn.Held();
        await turn.Released(CancellationToken.None);

        voice.Spoken.ShouldBe(["Seventy percent."]);
    }

    [Fact]
    public async Task ATurn_OnEnteringEachState_PublishesIt()
    {
        var events = new Events();
        List<TurnState> entered = [];
        using IDisposable _ = events.Subscribe(
            published => { if (published is Entered state) { entered.Add(state.State); } });

        var turn = new Turn(
            new StandInMicrophone(),
            new StandInTranscriber("what is my fuel"),
            new StandInModel("Seventy percent."),
            new StandInVoice(),
            events);

        turn.Held();
        await turn.Released(CancellationToken.None);

        entered.ShouldBe(
        [
            TurnState.Listening,
            TurnState.Transcribing,
            TurnState.Thinking,
            TurnState.Speaking,
            TurnState.Idle,
        ]);
    }

    [Fact]
    public async Task ATurn_HandsTheTranscriptToTheModel_AndTheModelsReplyToTheVoice()
    {
        var model = new StandInModel("Seventy percent.");
        var voice = new StandInVoice();
        var turn = new Turn(
            new StandInMicrophone(),
            new StandInTranscriber("what is my fuel"),
            model,
            voice,
            new Events());

        turn.Held();
        await turn.Released(CancellationToken.None);

        model.Asked.ShouldBe(["what is my fuel"]);
        voice.Spoken.ShouldBe(["Seventy percent."]);
    }

    private sealed class StandInMicrophone : IMicrophone
    {
        public void Open()
        {
        }

        public Captured Close() => new(new byte[] { 1, 2, 3 });
    }

    private sealed class StandInTranscriber(string transcript) : ITranscriber
    {
        public Task<string> Transcribe(Captured captured, CancellationToken stopping) =>
            Task.FromResult(transcript);
    }

    private sealed class StandInModel(string reply) : IModel
    {
        public List<string> Asked { get; } = [];

        public Task<string> Answer(string said, CancellationToken stopping)
        {
            Asked.Add(said);
            return Task.FromResult(reply);
        }
    }

    private sealed class StandInVoice : IVoice
    {
        public List<string> Spoken { get; } = [];

        public Task Speak(string reply, CancellationToken stopping)
        {
            Spoken.Add(reply);
            return Task.CompletedTask;
        }
    }
}
