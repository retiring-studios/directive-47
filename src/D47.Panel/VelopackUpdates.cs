using Velopack;
using Velopack.Sources;

namespace D47.Panel;

/// <summary>
/// The real updater, as <see cref="Updates"/> deals with it.
///
/// <para>
/// An adapter and nothing else. It holds what a check found so that fetching and
/// applying refer to the same release, and it knows that a copy which was never
/// installed has nothing to update — every other decision belongs to
/// <see cref="Updates"/>, where it can be asserted.
/// </para>
/// </summary>
internal sealed class VelopackUpdates : IUpdateSource
{
    private readonly UpdateManager _updater;

    /// <summary>
    /// What the last check found, kept so that fetching and applying are about
    /// the same release rather than about whatever is newest at the moment each
    /// is called.
    /// </summary>
    private UpdateInfo? _found;

    /// <summary>
    /// Points the updater at this project's releases.
    /// </summary>
    /// <param name="feed">Where releases are published.</param>
    /// <param name="includePrereleases">
    /// Whether a release marked prerelease counts as an update.
    /// </param>
    internal VelopackUpdates(string feed, bool includePrereleases)
    {
        _updater = new UpdateManager(new GithubSource(feed, null, includePrereleases));
    }

    /// <inheritdoc/>
    ///
    /// <remarks>
    /// A copy that was not installed has nothing to update and must not be
    /// asked: the updater has no release folder to compare against and says so
    /// by throwing. That is every developer run and every test run, so it is
    /// checked rather than caught.
    /// </remarks>
    public string? Waiting()
    {
        if (!_updater.IsInstalled)
        {
            return null;
        }

        _found = _updater.CheckForUpdates();

        return _found?.TargetFullRelease.Version.ToString();
    }

    /// <inheritdoc/>
    public void Fetch()
    {
        if (_found is not null)
        {
            _updater.DownloadUpdates(_found);
        }
    }

    /// <inheritdoc/>
    ///
    /// <remarks>
    /// Does not restart. The Commander is closing Directive 47, and an
    /// application that comes back up because it updated itself is one that
    /// ignored what was asked of it.
    /// </remarks>
    public void ApplyOnExit()
    {
        if (_found is not null)
        {
            _updater.WaitExitThenApplyUpdates(_found, silent: true, restart: false);
        }
    }
}
