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

    [Theory]
    [InlineData(TurnState.Transcribing)]
    [InlineData(TurnState.Thinking)]
    [InlineData(TurnState.Speaking)]
    public async Task VoiceLoop_OnAProviderFailure_ReturnsToIdleWithoutCrashing(TurnState stage)
    {
        var events = new Events();
        List<TurnEvent> published = [];
        using IDisposable _ = events.Subscribe(published.Add);

        // Every stage, not just the first. All three reach somebody else's
        // service by stage C, and a catch that happened to sit around only one
        // of them would pass a test written against that one.
        var broken = new InvalidOperationException("the provider fell over");

        var turn = new Turn(
            new StandInMicrophone(),
            stage == TurnState.Transcribing
                ? new FailingTranscriber(broken)
                : new StandInTranscriber("what is my fuel"),
            stage == TurnState.Thinking
                ? new FailingModel(broken)
                : new StandInModel("Seventy percent."),
            stage == TurnState.Speaking ? new FailingVoice(broken) : new StandInVoice(),
            events);

        turn.Held();

        // The whole assertion, and it is the absence of something: this does not
        // throw. Nothing awaits the real one, so an exception escaping here is
        // unobserved rather than handled.
        await Should.NotThrowAsync(() => turn.Released(CancellationToken.None));

        // Failure is an event, not a state. It says which stage, so a surface
        // can say more than that something went wrong.
        published.OfType<Failed>().ShouldHaveSingleItem().During.ShouldBe(stage);

        published.OfType<Failed>().Single().Why.ShouldBe(broken);

        // The states, all of them, spelled out. It stops at the stage that
        // failed and then lands on Idle — so the next hold is a turn rather than
        // a second one layered on a surface still showing Thinking, and no sixth
        // state was invented on the way, which is the half of the decision a
        // Failed event on its own would not hold to.
        TurnState[] reached =
            [.. published.OfType<Entered>().Select(published => published.State)];

        reached.ShouldBe(
            [.. UpToAndIncluding(stage), TurnState.Idle],
            $"the turn should stop at {stage} and end idle, but went {Named(reached)}");
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

    /// <summary>
    /// Every state a turn passes through on the way to one, that one included.
    /// </summary>
    ///
    /// <remarks>
    /// Written out rather than derived from the enum's order. The enum is
    /// declared in the order a turn happens to visit its members today, and a
    /// test that leans on that is asserting the declaration rather than the
    /// sequence.
    /// </remarks>
    private static TurnState[] UpToAndIncluding(TurnState stage) => stage switch
    {
        TurnState.Transcribing => [TurnState.Listening, TurnState.Transcribing],
        TurnState.Thinking =>
            [TurnState.Listening, TurnState.Transcribing, TurnState.Thinking],
        _ =>
        [
            TurnState.Listening,
            TurnState.Transcribing,
            TurnState.Thinking,
            TurnState.Speaking,
        ],
    };

    private sealed class StandInMicrophone : IMicrophone
    {
        public void Open()
        {
        }

        public Captured Close() => new(new byte[] { 1, 2, 3 });
    }

    private sealed class FailingTranscriber(Exception broken) : ITranscriber
    {
        public Task<string> Transcribe(Captured captured, CancellationToken stopping) =>
            Task.FromException<string>(broken);
    }

    private sealed class FailingModel(Exception broken) : IModel
    {
        public Task<string> Answer(string said, CancellationToken stopping) =>
            Task.FromException<string>(broken);
    }

    private sealed class FailingVoice(Exception broken) : IVoice
    {
        public Task Speak(string reply, CancellationToken stopping) =>
            Task.FromException(broken);
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
