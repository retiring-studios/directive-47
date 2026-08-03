using System;
using System.Globalization;
using System.Windows.Input;

namespace D47.Panel;

/// <summary>
/// Which combination toggles the overlay: what the Commander chose, the claim
/// on it, and what happens when the choice changes.
///
/// <para>
/// The decisions live here rather than in <see cref="Hotkey"/>, which is the
/// same split the overlay already has. <see cref="Hotkey"/> is the wrapper over
/// <c>RegisterHotKey</c> and knows nothing about files; reading what was
/// remembered, deciding whether it means anything, and choosing what to do
/// instead when it does not are logic — and logic that can be asserted without
/// a desktop, as long as it does not sit inside the thing that needs one.
/// </para>
///
/// <para>
/// Hard-coded until
/// [#114](https://github.com/retiring-studios/directive-47/issues/114). The
/// combination stopped being cosmetic in
/// [#103](https://github.com/retiring-studios/directive-47/issues/103): a claim
/// is system-wide, so an application that already owns one leaves Directive 47
/// with no hotkey, a line in a log, and — until this — no way for the Commander
/// to get one back.
/// </para>
/// </summary>
internal sealed class ChosenHotkey : IDisposable
{
    /// <summary>
    /// What the store calls it.
    ///
    /// <para>
    /// Spelled the way it would be said out loud, because
    /// <c>remembered.json</c> is a file somebody opens and reads. It is also,
    /// until there is a settings page, the way this value is changed.
    /// </para>
    /// </summary>
    private const string WhatItIsCalled = "overlay hotkey";

    /// <summary>
    /// Where it starts on a machine that has never been told otherwise, and
    /// what it falls back to when what it was told is not a combination.
    ///
    /// <para>
    /// The one the application has always used. A default that moved when this
    /// story landed would take the working key away from every Commander who
    /// never asked for one.
    /// </para>
    /// </summary>
    private static readonly Combination Familiar =
        new(ModifierKeys.Control | ModifierKeys.Alt, Key.D);

    private readonly Store _remembered;
    private readonly Action<string> _record;
    private readonly Action _pressed;

    private Combination _inForce;
    private Hotkey? _claimed;
    private bool _disposed;

    private ChosenHotkey(
        Store remembered,
        Action<string> record,
        Action pressed,
        Combination inForce,
        Hotkey? claimed)
    {
        _remembered = remembered;
        _record = record;
        _pressed = pressed;
        _inForce = inForce;
        _claimed = claimed;
    }

    /// <summary>
    /// The combination that was chosen: whatever was left behind, or the
    /// familiar one when nothing usable was.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A line that is not a combination is an absence rather than a failure, in
    /// the same sense as an unreadable store or an opacity outside 0 to 1: the
    /// application starts, and says what it found. Refusing to start over a
    /// hand-edited setting whose whole purpose is to be adjusted would be the
    /// worse trade.
    /// </para>
    /// <para>
    /// Nothing at all is silent, because a first run has chosen nothing and
    /// that is every machine once. A line written on every ordinary startup is a
    /// line nobody looks at twice.
    /// </para>
    /// </remarks>
    /// <param name="remembered">What the last run left behind.</param>
    /// <param name="record">Where to note a choice that could not be used.</param>
    /// <returns>The combination to claim.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="remembered"/> or <paramref name="record"/> is null.
    /// </exception>
    internal static Combination ChosenIn(Store remembered, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(remembered);
        ArgumentNullException.ThrowIfNull(record);

        if (remembered.Read(WhatItIsCalled) is not { } written)
        {
            return Familiar;
        }

        if (Combination.TryParse(written, out Combination? chosen))
        {
            return chosen;
        }

        record(CouldNotUse(written));

        return Familiar;
    }

    /// <summary>
    /// Reads the chosen combination and claims it, carrying on without one if
    /// something else already owns it.
    /// </summary>
    ///
    /// <remarks>
    /// Must be called on a thread with a running message loop, because that is
    /// what dispatches the message Windows posts. The registration belongs to
    /// that thread, so <see cref="Rebind"/> and <see cref="Dispose"/> have to
    /// come back to it.
    /// </remarks>
    /// <param name="remembered">What the last run left behind.</param>
    /// <param name="record">Where to note anything it had to carry on without.</param>
    /// <param name="pressed">What to do when the combination arrives.</param>
    /// <returns>The claim, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentNullException">
    /// Any argument is null.
    /// </exception>
    internal static ChosenHotkey From(Store remembered, Action<string> record, Action pressed)
    {
        ArgumentNullException.ThrowIfNull(pressed);

        Combination chosen = ChosenIn(remembered, record);

        // Unwrapping is what writes the reason down, so a claim nobody could
        // have cannot be lost silently here. Nobody is watching at startup —
        // that is the difference between this and Rebind, which hands the
        // reason back to whoever asked.
        return new ChosenHotkey(
            remembered,
            record,
            pressed,
            chosen,
            Hotkey.TryRegister(chosen, pressed).Or(record));
    }

    /// <summary>
    /// Changes the combination, and remembers it for next time.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Takes effect at once. A combination that only worked after a restart
    /// would not be a setting, it would be a startup argument spelled
    /// differently — and it is the first criterion on
    /// [#18](https://github.com/retiring-studios/directive-47/issues/18).
    /// </para>
    /// <para>
    /// The new one is claimed before the old one is given back, so a refusal
    /// costs nothing: being told no should not take away the key that was
    /// working a moment ago. The cost of that order is that the application can
    /// stand in the way of its own request, which is why a combination already
    /// in force is answered without going near Windows.
    /// </para>
    /// <para>
    /// On the thread that claimed the last one. A registration belongs to its
    /// thread, so unregistering from anywhere else quietly does nothing.
    /// </para>
    /// </remarks>
    /// <param name="wanted">The combination to claim.</param>
    /// <returns>
    /// The combination now in force, or an absence carrying what to say about
    /// it when another application already owns the one asked for.
    ///
    /// <para>
    /// Handed back rather than only filed. Startup writes a refusal to a log
    /// because there is nobody to tell; whoever calls this is somebody to tell,
    /// and telling them is the whole of what this story adds to the absence
    /// [#103](https://github.com/retiring-studios/directive-47/issues/103)
    /// already modelled. It is written down as well — a Commander diagnosing
    /// this tomorrow has only the log.
    /// </para>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="wanted"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The combination could not be claimed for any reason other than something
    /// else owning it.
    /// </exception>
    internal Perhaps<Combination> Rebind(Combination wanted)
    {
        ArgumentNullException.ThrowIfNull(wanted);

        // Not politeness. Disposing has already given the combination back and
        // will never run again, so a claim made after it is one nothing
        // releases until the process ends.
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (wanted == _inForce && _claimed is not null)
        {
            return Perhaps<Combination>.Of(wanted);
        }

        string? refused = null;

        Hotkey? claimed = Hotkey.TryRegister(wanted, _pressed)
            .Or(why =>
            {
                refused = why;
                _record(why);
            });

        if (claimed is null)
        {
            // Non-null by construction: Or records before it hands back
            // nothing, and recording is what sets this.
            return Perhaps<Combination>.Absent(refused!);
        }

        // Only now. A combination nobody could claim is not worth giving the
        // old one back for, and it is not worth remembering either — a store
        // that says the Commander chose something that has never worked is a
        // store that will hand them the same dead key on every restart.
        _claimed?.Dispose();
        _claimed = claimed;
        _inForce = wanted;

        _remembered.Write(WhatItIsCalled, wanted.ToString());

        return Perhaps<Combination>.Of(wanted);
    }

    /// <summary>
    /// Gives the combination back.
    /// </summary>
    ///
    /// <remarks>
    /// On the thread that claimed it, for the reason <see cref="Rebind"/> gives.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _claimed?.Dispose();
        _claimed = null;
    }

    /// <summary>
    /// What to say about a remembered choice that is not a combination. Quotes
    /// what was found, because the fix is a line in a file and nobody can
    /// correct a line they cannot identify.
    /// </summary>
    private static string CouldNotUse(string written) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"The overlay's hotkey was remembered as \"{written}\", which is not a combination "
            + $"of at least one modifier and a key. Directive 47 has started on {Familiar} "
            + $"instead, and will keep using that until \"{WhatItIsCalled}\" says something it "
            + $"can read.");
}
