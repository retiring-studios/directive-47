using System;

namespace D47.Panel;

/// <summary>
/// Turns a keyboard's repeat into a single press.
///
/// <para>
/// Windows will do this itself, with <c>MOD_NOREPEAT</c> on the registration,
/// and that was the first implementation. It was replaced because the flag also
/// makes the hotkey invisible to synthesized input — measured, not assumed: with
/// it, a <c>keybd_event</c> press produces no <c>WM_HOTKEY</c> at all, not
/// merely no repeats. So the choice was between correct behaviour with no test
/// and correct behaviour with one, and this is the second.
/// </para>
/// </summary>
internal sealed class RepeatGuard
{
    /// <summary>
    /// How long after a press the next one is treated as the same press
    /// continuing.
    ///
    /// <para>
    /// A second, because Windows' repeat <em>delay</em> is what has to be
    /// covered and it is configurable from 250 milliseconds up to a second. A
    /// shorter window would let the first repeat of a held key through, which
    /// is a toggle that flips back the moment a Commander holds the key a beat
    /// too long — the exact failure this exists to prevent, arriving less often
    /// and so harder to recognise.
    /// </para>
    ///
    /// <para>
    /// The cost, accepted: two deliberate presses inside a second read as one.
    /// For a show-and-hide that is closer to a feature than a defect.
    /// </para>
    /// </summary>
    internal static readonly TimeSpan LongerThanAnyKeyboardRepeat = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time;
    private readonly TimeSpan _quiet;

    private DateTimeOffset _lastSeen = DateTimeOffset.MinValue;

    /// <summary>
    /// Creates a guard.
    /// </summary>
    /// <param name="time">Where now comes from.</param>
    /// <param name="quiet">
    /// How long a press keeps swallowing the ones behind it. Defaults to
    /// <see cref="LongerThanAnyKeyboardRepeat"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> is null.</exception>
    internal RepeatGuard(TimeProvider time, TimeSpan? quiet = null)
    {
        ArgumentNullException.ThrowIfNull(time);

        _time = time;
        _quiet = quiet ?? LongerThanAnyKeyboardRepeat;
    }

    /// <summary>
    /// Whether this press is a press, or the same one still happening.
    /// </summary>
    ///
    /// <remarks>
    /// The clock is reset on every press seen, not only on the ones allowed
    /// through. That is what collapses a whole repeat train rather than the
    /// first fraction of it: each repeat arrives a few milliseconds after the
    /// last and pushes the window along ahead of itself, so the key stays
    /// swallowed for as long as it is held. Resetting only on the allowed ones
    /// would let a press through every second, for as long as somebody leaned
    /// on the key.
    /// </remarks>
    /// <returns>Whether to act on it.</returns>
    internal bool Allows()
    {
        DateTimeOffset now = _time.GetUtcNow();
        bool itsOwnPress = now - _lastSeen >= _quiet;

        _lastSeen = now;

        return itsOwnPress;
    }
}
