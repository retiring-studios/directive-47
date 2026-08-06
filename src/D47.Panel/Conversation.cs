using System;
using System.Threading;
using System.Threading.Tasks;

using D47.Audio;
using D47.VoiceLoop;

namespace D47.Panel;

/// <summary>
/// The voice loop, assembled: a microphone, the turn that drives it, the bus it
/// reports on, and the two things listening to that bus.
/// </summary>
///
/// <remarks>
/// <para>
/// One thing rather than five fields on <see cref="App"/>. The parts arrived one
/// story at a time — the turn, then the log subscription, then the cues, then the
/// real microphone — and each addition was a line in the composition root and a
/// line in its teardown. Four stories in, that was five fields, five disposals in
/// an order that matters, and a method called <c>ClaimPushToTalk</c> doing five
/// things.
/// </para>
/// <para>
/// Nothing here decides anything. What a turn does is <c>Turn</c> and is Tier 0;
/// which microphone to use is <see cref="ChosenMicrophone"/>; what a state sounds
/// like is <c>Cues</c>. This owns them, wires them to each other, and gives them
/// back in one piece.
/// </para>
/// <para>
/// The hotkey is deliberately not in here. A hotkey is an input mechanism and
/// owns nothing — it reports that a key went down and that it came up, and
/// <c>Turn</c> decides what those mean. It also belongs beside the other
/// system-wide claim <see cref="App"/> makes, not beside the loop it happens to
/// drive.
/// </para>
/// </remarks>
internal sealed class Conversation : IDisposable
{
    private readonly IMicrophone _microphone;
    private readonly Turn _turn;
    private readonly IDisposable _watching;
    private readonly CuePlayer _cues;

    private bool _disposed;

    private Conversation(
        IMicrophone microphone,
        Turn turn,
        IDisposable watching,
        CuePlayer cues)
    {
        _microphone = microphone;
        _turn = turn;
        _watching = watching;
        _cues = cues;
    }

    /// <summary>
    /// Builds the loop over whatever this machine turns out to have.
    /// </summary>
    ///
    /// <remarks>
    /// The transcriber, the model and the voice are still stand-ins. Stage C of
    /// [#1](https://github.com/retiring-studios/directive-47/issues/1)'s build
    /// order replaces them, three at once, and each is a straight swap of one
    /// argument here.
    /// </remarks>
    /// <param name="record">Where to note anything worth knowing about.</param>
    /// <returns>The loop, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    internal static Conversation Assembled(Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var turns = new Events();

        IMicrophone microphone = ChosenMicrophone.Available(Microphone.IsAvailable, record);

        var turn = new Turn(
            microphone,
            new StandIns.Transcriber(),
            new StandIns.Model(),
            new StandIns.Voice(record),
            turns);

        // A turn no longer throws when a provider falls over — it publishes and
        // returns to Idle. Without something listening, the failure that used to
        // reach a catch block in the composition root becomes silent.
        IDisposable watching = turns.Subscribe(published =>
        {
            if (published is Failed gaveUp)
            {
                record($"The turn gave up during {gaveUp.During}: {gaveUp.Why.Message}");
            }
        });

        // Random.Shared rather than one of our own. Nothing wants a sequence it
        // can reproduce, and a Commander who could tell which chime came next
        // would be hearing a pattern rather than a set.
        var cues = CuePlayer.Following(turns, Random.Shared);

        // The bus is not kept. Nothing outside asks for it today, and
        // [#72](https://github.com/retiring-studios/directive-47/issues/72) is
        // what will — a status and a live log in front of the Commander. It
        // arrives with that rather than ahead of it.
        return new Conversation(microphone, turn, watching, cues);
    }

    /// <summary>
    /// The key went down.
    /// </summary>
    internal void Held() => _turn.Held();

    /// <summary>
    /// The key came up. Runs the rest of the turn.
    /// </summary>
    /// <param name="stopping">Abandons the turn when this is signalled.</param>
    /// <returns>When the reply has been spoken.</returns>
    internal Task Released(CancellationToken stopping) => _turn.Released(stopping);

    /// <summary>
    /// Gives back everything the loop is holding.
    /// </summary>
    ///
    /// <remarks>
    /// The listeners before the bus they listen to, and the microphone last
    /// because a turn still unwinding may yet close it. Nothing here waits for a
    /// turn in flight — the process is going away, and a cancelled turn's own
    /// finally is what gives its token source back.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Both hold a subscription on the bus, and an undisposed one is this
        // application keeping itself alive. The cue player holds an audio device
        // besides.
        _cues.Dispose();
        _watching.Dispose();

        // The real one holds a capture device. IMicrophone says nothing about
        // disposing, because the stand-in has nothing to give back and the
        // contract describes what a turn needs rather than what an adapter owns.
        (_microphone as IDisposable)?.Dispose();
    }
}
