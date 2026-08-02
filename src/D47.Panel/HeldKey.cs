namespace D47.Panel;

/// <summary>
/// Tells a key being held down from a key being pressed again.
///
/// <para>
/// Windows repeats a held hotkey, so one press arrives as a stream of messages.
/// Every message after the first belongs to the press already happening; the
/// next real press is the one after the key has been let go.
/// </para>
///
/// <para>
/// This replaced a version that compared timestamps, and the reason it had to is
/// worth keeping: no window of time can separate the two cases. Windows sends
/// the first repeat after the repeat <em>delay</em>, which a Commander can set
/// as high as a second — so a window short enough to allow two deliberate taps
/// is also short enough to let a held key through, and one long enough to
/// swallow a held key eats the taps. Release is the only thing that actually
/// distinguishes them, which is what the maintainer said when the timestamp
/// version "did not feel right".
/// </para>
/// </summary>
internal sealed class HeldKey
{
    private bool _stillDown;

    /// <summary>
    /// Whether this message is its own press, or the one already happening.
    /// </summary>
    /// <returns>Whether to act on it.</returns>
    internal bool Allows()
    {
        if (_stillDown)
        {
            return false;
        }

        _stillDown = true;

        return true;
    }

    /// <summary>
    /// The key came up, so the next message is a new press.
    /// </summary>
    internal void LetGo() => _stillDown = false;
}
