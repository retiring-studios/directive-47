using System;
using System.Globalization;
using System.Windows.Input;

using D47.Data;

namespace D47.Panel;

/// <summary>
/// Which combination is held to talk: what the Commander chose, the claim on it,
/// and the turn it runs while it is down.
///
/// <para>
/// The same split as <see cref="ChosenHotkey"/> and for the same reason —
/// reading what was remembered and deciding whether it means anything is logic,
/// and logic that can be asserted without a desktop as long as it does not sit
/// inside the thing that needs one.
/// </para>
///
/// <para>
/// Held, not pressed, which is the whole difference from
/// <see cref="ChosenHotkey"/>. The overlay toggles on a press and the release
/// only re-arms it; here the release is the event that matters, because it is
/// what says the Commander has stopped talking.
/// </para>
/// </summary>
///
/// <remarks>
/// <para>
/// Still <c>RegisterHotKey</c> rather than a low-level keyboard hook.
/// <see cref="Hotkey"/>'s comment left that open for this feature to settle, and
/// it settles the other way: the registration already watches for the key coming
/// up, so hold-and-release needs no new API — and the hook is the one keyloggers
/// use, inside an unsigned single-file exe. The trade is that release is noticed
/// on a 20 ms look rather than the instant it happens, which is a fifth of the
/// time a person takes to lift a finger. If that ever proves too coarse, the
/// hook is still there to reach for.
/// </para>
/// </remarks>
internal sealed class PushToTalk : IDisposable
{
    /// <summary>
    /// What the store calls it, spelled the way it would be said out loud.
    /// </summary>
    internal const string WhatItIsCalled = "push to talk";

    /// <summary>
    /// Where it starts on a machine that has never been told otherwise, and what
    /// it falls back to when what it was told is not a combination.
    ///
    /// <para>
    /// Deliberately not <see cref="ChosenHotkey.Familiar"/>. Both claims are
    /// system-wide and independent, so two settings shipping the same default
    /// would mean whichever registered second never registered at all — and the
    /// Commander would meet a feature that silently does nothing.
    /// </para>
    /// </summary>
    internal static readonly Combination ShippedWith =
        new(ModifierKeys.Control | ModifierKeys.Alt, Key.V);

    private readonly Hotkey? _claimed;
    private bool _disposed;

    private PushToTalk(Hotkey? claimed)
    {
        _claimed = claimed;
    }

    /// <summary>
    /// The combination that was chosen: whatever was left behind, or the shipped
    /// one when nothing usable was.
    /// </summary>
    ///
    /// <remarks>
    /// A line that is not a combination is an absence rather than a failure, for
    /// the reason <see cref="ChosenHotkey.ChosenIn"/> gives. Nothing at all is
    /// silent, because a first run has chosen nothing and that is every machine
    /// once.
    /// </remarks>
    /// <param name="settings">What the Commander chose.</param>
    /// <param name="record">Where to note a choice that could not be used.</param>
    /// <returns>The combination to claim.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="settings"/> or <paramref name="record"/> is null.
    /// </exception>
    internal static Combination ChosenIn(SettingsStore settings, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(record);

        if (settings.Read(WhatItIsCalled) is not { } written)
        {
            return ShippedWith;
        }

        if (Combination.TryParse(written, out Combination? chosen))
        {
            return chosen;
        }

        record(CouldNotUse(written));

        return ShippedWith;
    }

    /// <summary>
    /// Reads the chosen combination and claims it, carrying on without one if
    /// something else already owns it.
    /// </summary>
    ///
    /// <remarks>
    /// Must be called on a thread with a running message loop, for the reason
    /// <see cref="ChosenHotkey.From"/> gives.
    /// </remarks>
    /// <param name="settings">What the Commander chose.</param>
    /// <param name="record">Where to note anything it carried on without.</param>
    /// <param name="held">What to do when the key goes down.</param>
    /// <param name="released">What to do when it comes up.</param>
    /// <returns>The claim, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    internal static PushToTalk From(
        SettingsStore settings, Action<string> record, Action held, Action released)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(released);

        Combination chosen = ChosenIn(settings, record);

        return new PushToTalk(
            Hotkey.TryRegister(chosen, held, released).Or(record));
    }

    /// <summary>
    /// Gives the combination back.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _claimed?.Dispose();
    }

    /// <summary>
    /// What to say about a remembered choice that is not a combination.
    /// </summary>
    private static string CouldNotUse(string written) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Push-to-talk was remembered as \"{written}\", which is not a combination of at "
            + $"least one modifier and a key. Directive 47 has started on {ShippedWith} instead, "
            + $"and will keep using that until \"{WhatItIsCalled}\" says something it can read.");
}
