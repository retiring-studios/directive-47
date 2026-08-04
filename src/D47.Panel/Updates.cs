using System;

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
    private readonly IUpdateSource _source;

    private bool _accepted;

    private Updates(IUpdateSource source)
    {
        _source = source;
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
    /// <returns>Something to ask, accept and decline with.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    internal static Updates From(IUpdateSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new Updates(source);
    }

    /// <summary>
    /// Goes and finds out whether there is a newer version.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Separate from <see cref="From"/>, and deliberately not called at startup
    /// yet. This is the one call here that reaches the network, so it is the one
    /// that can fail on a machine that is merely offline — and what should happen
    /// then is
    /// [#145](https://github.com/retiring-studios/directive-47/issues/145)'s
    /// criterion rather than a guess made here. Constructing without asking is
    /// what keeps that story's absence from being a crash on startup in the
    /// meantime.
    /// </para>
    /// <para>
    /// [#143](https://github.com/retiring-studios/directive-47/issues/143) is
    /// what calls this, when it has somewhere to show the answer.
    /// </para>
    /// </remarks>
    internal void Look() => Waiting = _source.Waiting();

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
