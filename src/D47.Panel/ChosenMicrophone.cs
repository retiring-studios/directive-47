using System;

using D47.VoiceLoop;

namespace D47.Panel;

/// <summary>
/// Which microphone the loop gets: the real one, or a stand-in on a machine
/// that has none.
/// </summary>
///
/// <remarks>
/// <para>
/// The same split as <see cref="ChosenHotkey"/> and <see cref="PushToTalk"/>, and
/// for the same reason — deciding what the machine offers is logic, and logic
/// worth asserting without the hardware as long as it does not sit inside the
/// thing that needs it.
/// </para>
/// <para>
/// A machine with no capture device is an ordinary state a Commander is allowed
/// to be in.
/// [#228](https://github.com/retiring-studios/directive-47/issues/228) says
/// Directive 47 stays reachable without a microphone, so this cannot be an
/// exception on the way up.
/// </para>
/// </remarks>
internal static class ChosenMicrophone
{
    /// <summary>
    /// What to say when there is nothing to record with.
    /// </summary>
    internal const string NoneFound =
        "No microphone was found, so push-to-talk will record nothing. Directive 47 still "
        + "runs — plug one in or enable one in Sound settings, then restart to use it.";

    /// <summary>
    /// Picks the microphone to use, once.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Asked once at startup rather than per turn. Enumerating audio endpoints
    /// is a call into the operating system, and the only other place to put it
    /// is the path that opens the microphone when the key goes down — which is
    /// the one place [#7](https://github.com/retiring-studios/directive-47/issues/7)
    /// says nothing may be spent, because it is time not recording the first
    /// word.
    /// </para>
    /// <para>
    /// The cost is that plugging one in mid-session does nothing until a
    /// restart, which the message says out loud rather than leaving a Commander
    /// to discover.
    /// </para>
    /// </remarks>
    /// <param name="anyDevice">Whether the machine has anything to record with.</param>
    /// <param name="record">Where to note that it has not.</param>
    /// <returns>The microphone to hand the loop.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    internal static IMicrophone Available(Func<bool> anyDevice, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(anyDevice);
        ArgumentNullException.ThrowIfNull(record);

        if (anyDevice())
        {
            return new Audio.Microphone();
        }

        record(NoneFound);

        return new StandIns.Microphone();
    }
}
