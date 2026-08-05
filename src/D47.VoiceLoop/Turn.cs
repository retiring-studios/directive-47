using System;
using System.Threading;
using System.Threading.Tasks;

namespace D47.VoiceLoop;

/// <summary>
/// One turn: hold the key and it listens, let go and it answers.
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
/// </summary>
public sealed class Turn
{
    private readonly IMicrophone _microphone;
    private readonly ITranscriber _transcriber;
    private readonly IModel _model;
    private readonly IVoice _voice;
    private readonly Events _events;

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
    /// The key went down. Starts listening.
    /// </summary>
    ///
    /// <remarks>
    /// Deliberately not async. Opening the microphone is what has to happen
    /// before the Commander starts talking, and anything awaited here is time
    /// spent not recording the first word of it.
    /// </remarks>
    public void Held()
    {
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
    public async Task Released(CancellationToken stopping)
    {
        try
        {
            Captured captured = _microphone.Close();

            Enter(TurnState.Transcribing);
            string said = await _transcriber.Transcribe(captured, stopping)
                .ConfigureAwait(false);

            Enter(TurnState.Thinking);
            string reply = await _model.Answer(said, stopping).ConfigureAwait(false);

            Enter(TurnState.Speaking);
            await _voice.Speak(reply, stopping).ConfigureAwait(false);
        }
        finally
        {
            Enter(TurnState.Idle);
        }
    }

    private void Enter(TurnState state) => _events.Publish(new Entered(state));
}
