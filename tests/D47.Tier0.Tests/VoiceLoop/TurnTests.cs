using System;
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public async Task PushToTalk_WhenTappedDuringAReply_CancelsTheInFlightTurn()
    {
        var events = new Events();
        List<TurnState> entered = [];
        using IDisposable _ = events.Subscribe(
            published => { if (published is Entered state) { entered.Add(state.State); } });

        using var voice = new SlowVoice();
        var turn = new Turn(
            new StandInMicrophone(),
            new StandInTranscriber("what is my fuel"),
            new StandInModel("Seventy percent."),
            voice,
            events);

        turn.Held();
        Task running = turn.Released(CancellationToken.None);

        // Waited for rather than slept through. The tap has to land while the
        // reply is actually being spoken, and a fixed pause would either be
        // slower than it needs to be or lose the race on a loaded machine —
        // which is the shape of flake that only fails on somebody else's run.
        (await voice.Speaking.WaitAsync(Patience, TestContext.Current.CancellationToken))
            .ShouldBeTrue("the turn should have reached the voice");

        // The tap: the key going down and coming up again, which is all the
        // hotkey ever reports. Deciding that this pair means cancel rather than
        // the start of a new turn is the turn's own job.
        turn.Held();

        // Both waits bounded, including this one. A turn that ignores the tap
        // leaves the stand-in voice talking forever, and an unbounded await here
        // makes that a hung test run rather than a failed test — which is the
        // one way a red is worse than useless.
        await turn.Released(CancellationToken.None)
            .WaitAsync(Patience, TestContext.Current.CancellationToken);

        await running.WaitAsync(Patience, TestContext.Current.CancellationToken);

        voice.WasCancelled.ShouldBeTrue("the tap should have cut the reply short");

        // And it ends where every turn ends. One that stopped anywhere else
        // would leave every surface showing Speaking for the rest of the
        // session.
        entered[^1].ShouldBe(TurnState.Idle);

        // The tap started nothing of its own. A second Transcribing here would
        // mean interrupting a reply also recorded a turn the Commander never
        // asked for.
        entered.Count(state => state == TurnState.Transcribing).ShouldBe(
            1,
            $"the tap should not have run a turn of its own, but got {Named(entered)}");
    }

    /// <summary>
    /// Long enough that a slow machine is not a failure, short enough that a
    /// genuine hang is a failed test rather than a hung run.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The states in the order they were entered, for a message somebody has to
    /// read at the point the assertion has already failed.
    /// </summary>
    private static string Named(IEnumerable<TurnState> entered) =>
        string.Join(" then ", entered);

    private sealed class StandInMicrophone : IMicrophone
    {
        public void Open()
        {
        }

        public Captured Close() => new(new byte[] { 1, 2, 3 });
    }

    /// <summary>
    /// A voice that keeps talking until something stops it, which is what makes
    /// there be an in-flight turn to cancel.
    /// </summary>
    private sealed class SlowVoice : IVoice, IDisposable
    {
        private readonly TaskCompletionSource<bool> _speaking = new();

        /// <summary>
        /// Completes once the voice has actually been asked to say something.
        /// </summary>
        public Task<bool> Speaking => _speaking.Task;

        /// <summary>
        /// Whether it was stopped rather than allowed to finish.
        /// </summary>
        public bool WasCancelled { get; private set; }

        public async Task Speak(string reply, CancellationToken stopping)
        {
            _speaking.TrySetResult(true);

            try
            {
                // Longer than the test's patience on purpose. Nothing here
                // finishes on its own, so a pass means the cancellation
                // arrived rather than that the wait ran out.
                await Task.Delay(Timeout.InfiniteTimeSpan, stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
        }

        public void Dispose() => _speaking.TrySetResult(false);
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
