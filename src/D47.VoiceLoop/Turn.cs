using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace D47.VoiceLoop;

/// <summary>
/// One turn: hold the key and it listens, let go and it answers, tap it again
/// and it stops.
///
/// <para>
/// The orchestration and nothing else. What it orchestrates lives behind the
/// four contracts it is handed, so the whole of a turn can be asserted in CI
/// with no microphone, no network and no headset — which is the entire reason
/// this project is Tier 0 and separate from <c>D47.Audio</c>.
/// </para>
///
/// <para>
/// Every state it enters is published on <see cref="Events"/>. The surfaces
/// render from those and from nothing else, so a turn never holds a reference to
/// anything that draws.
/// </para>
///
/// <para>
/// It owns its own cancellation. The hotkey is an input mechanism and reports
/// two things — the key went down, the key came up — for a tap that interrupts a
/// reply exactly as for a hold that starts one. Which of the two a press is can
/// only be answered by something that knows whether a turn is already running,
/// and that is here. Putting it in the hotkey would also move the decision into
/// <c>D47.Panel</c>, out of Tier 0 and out of CI's reach.
/// </para>
/// </summary>
public sealed class Turn
{
    private readonly IMicrophone _microphone;
    private readonly ITranscriber _transcriber;
    private readonly IModel _model;
    private readonly IVoice _voice;
    private readonly Events _events;

    /// <summary>
    /// Guards the two fields below, which are written from the thread the hotkey
    /// runs on and read from the one the turn runs on.
    /// </summary>
    private readonly Lock _guard = new();

    /// <summary>
    /// The turn in flight, or nothing. Also the answer to whether a key going
    /// down starts a turn or ends one.
    /// </summary>
    private CancellationTokenSource? _running;

    /// <summary>
    /// Whether the key now down was pressed to cancel, so the release after it
    /// knows not to run a turn of its own.
    /// </summary>
    private bool _cancelling;

    /// <summary>
    /// Creates a turn over the things it needs.
    /// </summary>
    /// <param name="microphone">Where what was said comes from.</param>
    /// <param name="transcriber">Turns that into words.</param>
    /// <param name="model">Answers the words.</param>
    /// <param name="voice">Says the answer.</param>
    /// <param name="events">Where the states are published.</param>
    /// <exception cref="ArgumentNullException">Any of them is null.</exception>
    public Turn(
        IMicrophone microphone,
        ITranscriber transcriber,
        IModel model,
        IVoice voice,
        Events events)
    {
        ArgumentNullException.ThrowIfNull(microphone);
        ArgumentNullException.ThrowIfNull(transcriber);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(events);

        _microphone = microphone;
        _transcriber = transcriber;
        _model = model;
        _voice = voice;
        _events = events;
    }

    /// <summary>
    /// The key went down. Starts listening, or stops a reply.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Deliberately not async. Opening the microphone is what has to happen
    /// before the Commander starts talking, and anything awaited here is time
    /// spent not recording the first word of it.
    /// </para>
    /// <para>
    /// Pressing while a turn is in flight cancels it and starts nothing. The
    /// Commander who interrupts an answer wants it to stop; giving them a
    /// recording as well would leave the microphone open until they pressed
    /// again, which is the opposite of what the gesture asked for.
    /// </para>
    /// </remarks>
    public void Held()
    {
        lock (_guard)
        {
            if (_running is { } inFlight)
            {
                // Remembered as well as acted on, because the release is a
                // separate call arriving later and it has no other way to know
                // this press was an interruption.
                _cancelling = true;

                inFlight.Cancel();

                return;
            }

            _cancelling = false;
        }

        _microphone.Open();
        Enter(TurnState.Listening);
    }

    /// <summary>
    /// The key came up. Runs the rest of the turn.
    /// </summary>
    ///
    /// <remarks>
    /// Ends at <see cref="TurnState.Idle"/> however it goes, which is what makes
    /// the next hold a turn rather than a second one layered on the first.
    /// </remarks>
    /// <param name="stopping">Abandons the turn when this is signalled.</param>
    /// <returns>When the reply has been spoken.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification =
            "The four contracts reach a microphone, a model and somebody else's speech "
            + "service, and there is no list of what those can throw. Nothing awaits a turn, "
            + "so an exception leaving here is unobserved rather than handled — the whole "
            + "point of the story is that any of them failing ends at Idle instead of "
            + "silently ending the loop.")]
    public async Task Released(CancellationToken stopping)
    {
        CancellationTokenSource running;

        lock (_guard)
        {
            if (_cancelling)
            {
                // The key coming up after an interruption. The turn it stopped
                // is unwinding on its own thread and will publish Idle when it
                // gets there; there is nothing for this release to do and
                // nothing for it to say.
                _cancelling = false;

                return;
            }

            running = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            _running = running;
        }

        // Where the turn has got to, so that a failure can say which stage gave
        // up rather than only that one did. Tracked beside Enter rather than
        // read back off the bus: the bus is where this is told, not a place to
        // ask.
        TurnState reached = TurnState.Listening;

        void Reach(TurnState next)
        {
            reached = next;
            Enter(next);
        }

        try
        {
            Captured captured = _microphone.Close();

            Reach(TurnState.Transcribing);
            string said = await _transcriber.Transcribe(captured, running.Token)
                .ConfigureAwait(false);

            Reach(TurnState.Thinking);
            string reply = await _model.Answer(said, running.Token).ConfigureAwait(false);

            Reach(TurnState.Speaking);
            await _voice.Speak(reply, running.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Swallowed rather than thrown on. A cancelled turn is what the
            // Commander asked for by tapping the key, so it is an ordinary
            // ending — and one that threw would be reported as a failure.
        }
        catch (Exception failed)
        {
            // Published rather than thrown, which is what makes returning to
            // Idle a fact rather than a catch block somewhere above. Nothing
            // awaits a turn, so an exception leaving here is unobserved instead
            // of handled.
            _events.Publish(new Failed(reached, failed));
        }
        finally
        {
            lock (_guard)
            {
                _running = null;
            }

            running.Dispose();

            Enter(TurnState.Idle);
        }
    }

    private void Enter(TurnState state) => _events.Publish(new Entered(state));
}
