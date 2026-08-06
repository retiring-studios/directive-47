using System;
using System.Threading;
using System.Threading.Tasks;

using D47.VoiceLoop;

namespace D47.Panel;

/// <summary>
/// What sits behind the turn's four contracts until there is something real
/// there.
///
/// <para>
/// Stage B of [#1](https://github.com/retiring-studios/directive-47/issues/1)'s
/// build order names the contracts; stage C implements them, three at once and
/// in three different projects. These exist so the shape can be wired up, held,
/// released and watched a wave early — and so the three stories that replace
/// them are each a straight swap rather than a first integration.
/// </para>
///
/// <para>
/// Deliberately visible rather than silent. A stand-in that did nothing at all
/// would make a broken claim on the hotkey and a working one look identical from
/// the cockpit, which is the failure this whole story exists to make observable.
/// </para>
/// </summary>
internal static class StandIns
{
    /// <summary>
    /// A microphone that opens, closes, and has heard nothing.
    /// </summary>
    ///
    /// <remarks>
    /// No longer what the application uses. <c>D47.Audio.Microphone</c> is, and
    /// <see cref="ChosenMicrophone"/> decides between the two — this is what a
    /// machine with no capture device gets, which
    /// [#228](https://github.com/retiring-studios/directive-47/issues/228) says
    /// has to keep working. A shipped fallback rather than scaffolding waiting
    /// to be deleted, unlike the three below it.
    /// </remarks>
    internal sealed class Microphone : IMicrophone
    {
        public void Open()
        {
        }

        public Captured Close() => new(ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>
    /// A transcriber that always heard the same thing.
    /// </summary>
    ///
    /// <remarks>
    /// Replaced by [#8](https://github.com/retiring-studios/directive-47/issues/8).
    /// The line is a question help can already answer, so the turn it drives is
    /// one whose real reply exists.
    /// </remarks>
    internal sealed class Transcriber : ITranscriber
    {
        internal const string AlwaysHears = "what can you do";

        public Task<string> Transcribe(Captured captured, CancellationToken stopping) =>
            Task.FromResult(AlwaysHears);
    }

    /// <summary>
    /// A model that says what it was asked, so the turn can be seen to have
    /// carried the transcript through.
    /// </summary>
    ///
    /// <remarks>
    /// Replaced by [#10](https://github.com/retiring-studios/directive-47/issues/10).
    /// </remarks>
    internal sealed class Model : IModel
    {
        public Task<string> Answer(string said, CancellationToken stopping) =>
            Task.FromResult($"You said: {said}. There is no model behind this yet.");
    }

    /// <summary>
    /// A voice that writes the reply down instead of saying it.
    /// </summary>
    ///
    /// <remarks>
    /// Replaced by [#9](https://github.com/retiring-studios/directive-47/issues/9).
    /// Until then the log is the only place a completed turn shows up, which is
    /// what makes holding the key worth doing at all.
    /// </remarks>
    internal sealed class Voice(Action<string> record) : IVoice
    {
        public Task Speak(string reply, CancellationToken stopping)
        {
            record(reply);
            return Task.CompletedTask;
        }
    }
}
