using System;
using System.Diagnostics.CodeAnalysis;

namespace D47.Panel;

/// <summary>
/// A newer Directive 47, if there is one, and what the Commander said about it.
///
/// <para>
/// The decisions live here rather than in the adapter, the same way
/// <see cref="Overlay"/>'s do: what accepting means, what declining means, and
/// that neither of them touches the running session. All of which is logic, and
/// all of which is assertable in CI against a stand-in — where a real updater
/// asked the same questions would install over the machine running the test.
/// </para>
/// </summary>
internal sealed class Updates
{
    /// <summary>
    /// What to say when the check itself would not run.
    ///
    /// <para>
    /// Names what was being attempted and says the running copy is fine, because
    /// whoever reads this went looking for why they were never offered a version
    /// they know exists. The reason is appended by the caller — "the check
    /// failed" on its own is a line nobody can act on.
    /// </para>
    /// </summary>
    private const string CouldNotAsk =
        "Directive 47 could not find out whether there is a newer version of itself. It has "
        + "carried on with the one you have, and will look again.";

    private readonly IUpdateSource _source;
    private readonly Action<string> _record;

    private bool _accepted;

    private Updates(IUpdateSource source, Action<string> record)
    {
        _source = source;
        _record = record;
    }

    /// <summary>
    /// The version waiting, or <see langword="null"/> if this copy is current.
    ///
    /// <para>
    /// What [#143](https://github.com/retiring-studios/directive-47/issues/143)
    /// puts on the panel and the overlay. A notice that cannot name the version
    /// is one nobody can decide about.
    /// </para>
    /// </summary>
    internal string? Waiting { get; private set; }

    /// <summary>
    /// Wraps an updater. Asks it nothing.
    /// </summary>
    /// <param name="source">Where a newer version comes from.</param>
    /// <param name="record">Where to note a check that would not run.</param>
    /// <returns>Something to ask, accept and decline with.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    internal static Updates From(IUpdateSource source, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(record);

        return new Updates(source, record);
    }

    /// <summary>
    /// Goes and finds out whether there is a newer version, and carries on if it
    /// cannot.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Absent, not failed — the third place this application draws that line,
    /// after the two overlays. No network, no server, no answer: none of it is a
    /// reason to interrupt somebody flying, and every one of them is outside
    /// this application to fix. A Commander who cannot reach GitHub has a
    /// working copy and nothing to hear about.
    /// </para>
    /// <para>
    /// Nothing is offered when the check failed, which is what "does not
    /// interrupt" comes to here. There is no dialog to suppress — this class
    /// cannot draw one — so the way a failed check could still reach the
    /// Commander is by leaving a notice on the panel and the overlay, and
    /// <see cref="Waiting"/> staying null is what stops it.
    /// </para>
    /// <para>
    /// Written down every time rather than once. Looking again is ordinary, and
    /// a second failure suppressed because it matched the first is a second
    /// failure nobody can see happened.
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification =
            "Every reason a check fails is outside this application and they are "
            + "indistinguishable here: no network, a proxy, a rate limit, a certificate, a "
            + "server having a bad day. The updater makes no promise about which exception "
            + "each arrives as, and a narrow catch that guessed wrong would take the "
            + "application down on startup for a machine that is merely offline.")]
    internal void Look()
    {
        try
        {
            Waiting = _source.Waiting();
        }
        catch (Exception problem)
        {
            // The previous answer goes with it. Keeping it would offer a version
            // this run never confirmed is there, and the notice is the one thing
            // a failed check must not produce.
            Waiting = null;

            _record($"{CouldNotAsk} ({problem.Message})");
        }
    }

    /// <summary>
    /// Takes the update, to be installed once Directive 47 is closed.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Fetches now and applies later, and the split is the whole story. The
    /// updater waits a minute for this process to exit and then stops waiting,
    /// so the slow part has to be over before that minute starts — while
    /// nothing is being interrupted, because a download is not a thing anyone
    /// sees.
    /// </para>
    /// <para>
    /// Accepting what was never offered is nothing rather than an error. There
    /// is no notice to answer unless something is waiting, so this can only be
    /// reached by a defect above — and throwing would turn that into a crash on
    /// the way out.
    /// </para>
    /// </remarks>
    internal void Accept()
    {
        if (Waiting is null)
        {
            return;
        }

        _accepted = true;

        _source.Fetch();
    }

    /// <summary>
    /// Leaves the update alone.
    /// </summary>
    ///
    /// <remarks>
    /// Clears an earlier acceptance rather than only being the absence of one.
    /// Changing your mind is ordinary, the notice stays up until it is answered,
    /// and a fetch already begun is not a commitment to install what it fetched.
    /// </remarks>
    internal void Decline() => _accepted = false;

    /// <summary>
    /// Hands an accepted update to the updater, on the way out.
    /// </summary>
    ///
    /// <remarks>
    /// The only place anything is installed, which is what makes "no installer
    /// window opens while the overlay is shown" true by construction rather than
    /// by care. There is no other call to <c>ApplyOnExit</c> to get wrong.
    /// </remarks>
    internal void Exiting()
    {
        if (!_accepted)
        {
            return;
        }

        _source.ApplyOnExit();
    }
}
