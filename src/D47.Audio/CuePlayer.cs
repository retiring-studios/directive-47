using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;

using D47.VoiceLoop;

using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// Plays the loop's sound cues as the turn moves between states.
/// </summary>
///
/// <remarks>
/// <para>
/// Subscribes rather than being called. The turn publishes what it is doing and
/// never holds a reference to anything that makes a noise, the same rule the
/// surfaces follow — so this attaches to the bus and the turn stays unaware of
/// it.
/// </para>
/// <para>
/// One cue at a time. A cue says which state the loop is in, so the one before
/// it is out of date the moment the next arrives; two overlapping chimes say
/// nothing a Commander can read. Starting one stops whatever was playing.
/// </para>
/// </remarks>
public sealed class CuePlayer : IDisposable
{
    private readonly Random _draw;
    private readonly Lock _guard = new();

    private IDisposable? _listening;
    private Playing? _playing;
    private bool _disposed;

    private CuePlayer(Random draw)
    {
        _draw = draw;
    }

    /// <summary>
    /// Starts listening to a turn and playing what it says.
    /// </summary>
    ///
    /// <remarks>
    /// The randomness is passed in rather than reached for, so a machine with no
    /// sound card is not the only way to get a predictable run.
    /// </remarks>
    /// <param name="events">Where the turn says what it is doing.</param>
    /// <param name="draw">Where the choice between a state's cues comes from.</param>
    /// <returns>The player, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static CuePlayer Following(Events events, Random draw)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(draw);

        var player = new CuePlayer(draw);

        player._listening = events.Subscribe(player.Heard);

        return player;
    }

    /// <summary>
    /// Stops listening and silences anything still playing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _listening?.Dispose();
        _listening = null;

        Silence();
    }

    /// <summary>
    /// Something happened on the bus.
    /// </summary>
    ///
    /// <remarks>
    /// Only state transitions make a sound today. A failure publishes
    /// <c>Failed</c> and then <c>Entered(Idle)</c>, so it is already audible as
    /// the turn ending; whether it deserves a sound of its own is
    /// <see href="https://github.com/retiring-studios/directive-47/issues/7">#7</see>'s
    /// question rather than this one's.
    /// </remarks>
    private void Heard(TurnEvent published)
    {
        if (published is Entered state && Cues.Pick(state.State, _draw) is { } cue)
        {
            Play(cue);
        }
    }

    /// <summary>
    /// Starts a cue, stopping whatever was playing.
    /// </summary>
    ///
    /// <remarks>
    /// Returns before the sound has finished. <c>Listening</c> is published from
    /// the thread the hotkey runs on, which is the UI thread, and "no stage
    /// blocks the UI thread" is an invariant of
    /// <see href="https://github.com/retiring-studios/directive-47/issues/7">#7</see>.
    /// </remarks>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification =
            "The stream is handed to Playing, which owns it from then on and disposes it "
            + "along with the reader and the device. Playing's constructor disposes it itself "
            + "if it cannot get that far, so there is no path where it is dropped.")]
    private void Play(string cue)
    {
        if (Cues.Open(cue) is not { } sound)
        {
            // Named but not embedded. Silence rather than a throw: this runs on
            // the turn's threads and on the UI thread, and neither has anywhere
            // to put an exception. EveryCue_ItNames_IsActuallyThere is what
            // makes sure it cannot happen rather than a check here.
            return;
        }

        var started = new Playing(sound);

        lock (_guard)
        {
            if (_disposed)
            {
                started.Dispose();

                return;
            }

            _playing?.Dispose();
            _playing = started;
        }

        started.Start();
    }

    /// <summary>
    /// Stops anything playing.
    /// </summary>
    private void Silence()
    {
        Playing? stopping;

        lock (_guard)
        {
            stopping = _playing;
            _playing = null;
        }

        stopping?.Dispose();
    }

    /// <summary>
    /// One cue on its way to the speakers, and everything it has to give back.
    /// </summary>
    ///
    /// <remarks>
    /// A type of its own because there are three things to dispose in an order
    /// that matters, and doing it in fields on the player meant three nullable
    /// fields that could disagree about which cue they belonged to.
    /// </remarks>
    private sealed class Playing : IDisposable
    {
        private readonly Stream _sound;
        private readonly WaveFileReader _reader;
        private readonly WasapiOut _output;

        private bool _disposed;

        internal Playing(Stream sound)
        {
            WaveFileReader? reading = null;

            try
            {
                reading = new WaveFileReader(sound);

                _output = new WasapiOut();
            }
            catch
            {
                // A cue that is not a readable WAV, or a machine with no output
                // device. Neither is worth leaking a stream over, and the caller
                // has nothing to dispose because it never got an instance.
                reading?.Dispose();
                sound.Dispose();

                throw;
            }

            _sound = sound;
            _reader = reading;
        }

        internal void Start()
        {
            _output.Init(_reader);
            _output.Play();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // The output first and the stream last, because stopping the device
            // is what makes it stop reading — the other order hands the mixer a
            // disposed reader mid-buffer.
            _output.Dispose();
            _reader.Dispose();
            _sound.Dispose();
        }
    }
}
