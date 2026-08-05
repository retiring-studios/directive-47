using System;
using System.Threading;
using System.Threading.Tasks;

namespace D47.VoiceLoop;

/// <summary>
/// What was said while the key was held, as bytes.
/// </summary>
///
/// <remarks>
/// No sample rate, no channel count and no encoding, because nothing yet reads
/// them: one thing produces this and one thing consumes it, and today both are
/// stand-ins. The format is
/// [#8](https://github.com/retiring-studios/directive-47/issues/8)'s to settle,
/// when there is a real transcriber with an opinion about what it can accept.
/// </remarks>
/// <param name="Samples">The captured audio.</param>
public sealed record Captured(ReadOnlyMemory<byte> Samples);

/// <summary>
/// The microphone, as the turn deals with it: open while the key is held, closed
/// when it is let go.
/// </summary>
///
/// <remarks>
/// Opened in shared mode by whatever implements this, never exclusive — a
/// Commander on wing comms or Discord keeps them while Directive 47 is
/// listening. That is an invariant of
/// [#7](https://github.com/retiring-studios/directive-47/issues/7) and it binds
/// the implementation rather than this interface, which has never heard of
/// WASAPI.
/// </remarks>
public interface IMicrophone
{
    /// <summary>
    /// Starts capturing.
    /// </summary>
    void Open();

    /// <summary>
    /// Stops capturing and hands back what was said.
    /// </summary>
    /// <returns>The audio captured since <see cref="Open"/>.</returns>
    Captured Close();
}

/// <summary>
/// Audio in, words out.
/// </summary>
public interface ITranscriber
{
    /// <summary>
    /// Turns what was said into words.
    /// </summary>
    /// <param name="captured">What was said.</param>
    /// <param name="stopping">Gives up when this is signalled.</param>
    /// <returns>The words.</returns>
    Task<string> Transcribe(Captured captured, CancellationToken stopping);
}

/// <summary>
/// Words in, a reply out.
/// </summary>
///
/// <remarks>
/// A whole reply rather than a stream of it. Speaking a sentence at a time is
/// [#9](https://github.com/retiring-studios/directive-47/issues/9)'s, and the
/// shape that serves it is that story's to choose once there is something real
/// behind both this and <see cref="IVoice"/>. Guessing it now would be naming a
/// seam for nobody.
/// </remarks>
public interface IModel
{
    /// <summary>
    /// Answers what the Commander said.
    /// </summary>
    /// <param name="said">What they said.</param>
    /// <param name="stopping">Gives up when this is signalled.</param>
    /// <returns>The reply.</returns>
    Task<string> Answer(string said, CancellationToken stopping);
}

/// <summary>
/// A reply in, sound out.
/// </summary>
public interface IVoice
{
    /// <summary>
    /// Says it out loud.
    /// </summary>
    /// <param name="reply">What to say.</param>
    /// <param name="stopping">Stops speaking when this is signalled.</param>
    /// <returns>When it has been said.</returns>
    Task Speak(string reply, CancellationToken stopping);
}
