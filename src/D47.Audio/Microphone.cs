using System;
using System.IO;
using System.Threading;

using D47.VoiceLoop;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// The Commander's microphone, open while the key is held.
/// </summary>
///
/// <remarks>
/// <para>
/// The adapter and none of the orchestration. When to open and what to do with
/// what comes back is <c>Turn</c>'s, it is Tier 0, and it is asserted in CI
/// against a stand-in — so the only thing that needs a real microphone is
/// whether this records for as long as it was asked to.
/// </para>
/// <para>
/// Shared mode, never exclusive. That is an invariant of
/// [#7](https://github.com/retiring-studios/directive-47/issues/7) rather than a
/// preference: a Commander on wing comms or Discord keeps them while Directive
/// 47 is listening, and exclusive mode would take the device away from both.
/// <c>WasapiCapture</c> opens shared unless told otherwise, so this is a matter
/// of not asking for the other thing.
/// </para>
/// </remarks>
public sealed class Microphone : IMicrophone, IDisposable
{
    /// <summary>
    /// How long to wait for the capture thread to finish before deciding it is
    /// not going to. Generous, because the only cost of waiting is a turn that
    /// answers late and the cost of giving up early is a turn that loses the
    /// end of what was said.
    /// </summary>
    private static readonly TimeSpan GivingUp = TimeSpan.FromSeconds(5);

    private readonly WasapiCapture _capture;
    private readonly ManualResetEventSlim _stopped = new(initialState: false);

    private MemoryStream? _recording;
    private bool _disposed;

    /// <summary>
    /// Opens the default capture device, without starting it.
    /// </summary>
    ///
    /// <remarks>
    /// The device is claimed here and the recording starts in
    /// <see cref="Open"/>, which is the split that matters for the two to three
    /// second budget: finding and initialising a device is the slow part, and
    /// doing it when the key goes down would be time spent not recording the
    /// first word.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The machine has no capture device.
    /// </exception>
    public Microphone()
    {
        if (!IsAvailable())
        {
            throw new InvalidOperationException(
                "There is no capture device to record from.");
        }

        _capture = new WasapiCapture();
        _capture.DataAvailable += Record;
        _capture.RecordingStopped += Stopped;
    }

    /// <summary>
    /// The format the device is actually recording in.
    /// </summary>
    ///
    /// <remarks>
    /// Read from the device rather than asked for. A shared-mode client records
    /// in whatever format the mixer is already running at, so asking for
    /// something else would mean resampling — and the transcriber
    /// [#8](https://github.com/retiring-studios/directive-47/issues/8) brings has
    /// not said yet what it wants.
    ///
    /// <para>
    /// Private, because <see cref="HowLong"/> is the only thing that reads it
    /// and nothing outside has asked. Making it public would also put NAudio's
    /// <c>WaveFormat</c> in this project's surface, and the whole point of the
    /// project is that the package stops here.
    /// </para>
    /// </remarks>
    private WaveFormat Format => _capture.WaveFormat;

    /// <summary>
    /// Whether this machine has a capture device at all.
    /// </summary>
    ///
    /// <remarks>
    /// Asked before constructing rather than discovered by catching, because a
    /// machine with no microphone is an ordinary state a Commander can be in —
    /// [#228](https://github.com/retiring-studios/directive-47/issues/228) says
    /// Directive 47 stays reachable without one — and an exception is the wrong
    /// shape for something the application is expected to handle.
    /// </remarks>
    /// <returns>Whether anything can be recorded from.</returns>
    public static bool IsAvailable()
    {
        using var devices = new MMDeviceEnumerator();

        return devices.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Count > 0;
    }

    /// <inheritdoc/>
    public void Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // A fresh buffer per hold rather than one cleared between them. What was
        // said last turn is not something to risk handing to the transcriber
        // this turn, and the cost of allocating is paid while the Commander is
        // still drawing breath.
        _recording = new MemoryStream();

        _stopped.Reset();

        _capture.StartRecording();
    }

    /// <inheritdoc/>
    ///
    /// <remarks>
    /// <c>StopRecording</c> is asynchronous — it asks the capture thread to
    /// stop and returns before it has. Anything read straight after it would
    /// miss whatever was still in flight, which is the tail of the last word
    /// the Commander said.
    /// </remarks>
    public Captured Close()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_recording is not { } recording)
        {
            throw new InvalidOperationException(
                "The microphone was closed without being opened.");
        }

        _capture.StopRecording();

        // Waited on rather than polled, and with a timeout. A spin on
        // CaptureState hangs the calling thread forever if the capture thread
        // dies on the way out, and the calling thread here is the one that has
        // to go on and answer the Commander.
        if (!_stopped.Wait(GivingUp))
        {
            throw new InvalidOperationException(
                $"The microphone did not stop within {GivingUp.TotalSeconds} seconds.");
        }

        _recording = null;

        return new Captured(recording.ToArray());
    }

    /// <summary>
    /// How long a recording lasts, given the format it was made in.
    /// </summary>
    ///
    /// <remarks>
    /// Here rather than on <see cref="Captured"/>, which carries bytes and no
    /// format on purpose — see its own remarks. The device that made a recording
    /// is the thing that knows how to read it, and today it is the only thing
    /// that needs to.
    /// </remarks>
    /// <param name="captured">What was recorded.</param>
    /// <returns>How much time it represents.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="captured"/> is null.</exception>
    public TimeSpan HowLong(Captured captured)
    {
        ArgumentNullException.ThrowIfNull(captured);

        return TimeSpan.FromSeconds(
            (double)captured.Samples.Length / Format.AverageBytesPerSecond);
    }

    /// <summary>
    /// Gives the device back.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _capture.DataAvailable -= Record;
        _capture.RecordingStopped -= Stopped;
        _capture.Dispose();

        _stopped.Dispose();

        _recording = null;
    }

    /// <summary>
    /// The capture thread has finished and written everything it had.
    /// </summary>
    private void Stopped(object? sender, StoppedEventArgs done) => _stopped.Set();

    /// <summary>
    /// Takes a buffer from the capture thread.
    /// </summary>
    ///
    /// <remarks>
    /// <c>BytesRecorded</c> rather than the buffer's length. NAudio hands back a
    /// buffer sized for the worst case and fills the front of it, so writing all
    /// of it would pad every recording with silence that was never heard — and
    /// the padding would be a fixed amount per buffer, which is exactly the
    /// shape of error a duration check is meant to catch.
    /// </remarks>
    private void Record(object? sender, WaveInEventArgs said) =>
        _recording?.Write(said.Buffer, 0, said.BytesRecorded);
}
