using System;
using System.Net.Http;

using D47.Panel;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Updaters that answer, refuse, or remember what they were asked to do.
///
/// <para>
/// Shared because two test classes need them and the shapes were identical:
/// <c>UpdateTests</c> asks what accepting and declining do, and
/// <c>FailedCheckTests</c> asks what happens when the check will not run. One
/// stand-in that both use is also one place for the pretence to be wrong,
/// rather than two that can drift into disagreeing about what an updater does.
/// </para>
///
/// <para>
/// All of this exists because a real updater cannot be asked whether it
/// <em>would</em> have applied something, and a test that let it find out would
/// install over the machine running it.
/// </para>
/// </summary>
internal static class UpdateSources
{
    /// <summary>
    /// An updater with a newer version to offer, which remembers what it was
    /// asked to do about it.
    /// </summary>
    internal sealed class Offering : IUpdateSource
    {
        private readonly string? _version;

        /// <summary>
        /// Offers a version.
        /// </summary>
        /// <param name="version">What is waiting.</param>
        internal Offering(string version)
        {
            _version = version;
        }

        /// <summary>
        /// Offers nothing, which is what a current copy is told.
        /// </summary>
        internal Offering()
        {
            _version = null;
        }

        /// <summary>
        /// Whether the update was ever downloaded.
        /// </summary>
        internal bool Fetched { get; private set; }

        /// <summary>
        /// Whether the updater was told to apply it once the process has gone.
        /// </summary>
        internal bool Applied { get; private set; }

        /// <inheritdoc/>
        public string? Waiting() => _version;

        /// <inheritdoc/>
        public void Fetch() => Fetched = true;

        /// <inheritdoc/>
        public void ApplyOnExit() => Applied = true;
    }

    /// <summary>
    /// An updater that answers once and then cannot, for the case that only
    /// exists because looking twice does.
    /// </summary>
    internal sealed class OfferingThenFailing : IUpdateSource
    {
        private readonly string _version;

        private bool _asked;

        /// <summary>
        /// Offers a version to the first caller and nothing but trouble after.
        /// </summary>
        /// <param name="version">What the first check finds.</param>
        internal OfferingThenFailing(string version)
        {
            _version = version;
        }

        /// <inheritdoc/>
        public string? Waiting()
        {
            if (_asked)
            {
                throw new HttpRequestException(ThatCannotAnswer.Because);
            }

            _asked = true;

            return _version;
        }

        /// <inheritdoc/>
        public void Fetch()
        {
            // nothing to do: this stand-in exists to answer checks
        }

        /// <inheritdoc/>
        public void ApplyOnExit()
        {
            // nothing to do: this stand-in exists to answer checks
        }
    }

    /// <summary>
    /// An updater on a machine that cannot reach the releases.
    /// </summary>
    ///
    /// <remarks>
    /// Throws what the real one throws. <c>HttpRequestException</c> is what a
    /// machine with no network produces, and a stand-in throwing
    /// <c>InvalidOperationException</c> instead would pass against a narrow
    /// <c>catch</c> that the real failure walks straight past.
    /// </remarks>
    internal sealed class ThatCannotAnswer : IUpdateSource
    {
        /// <summary>
        /// What the machine said, which the record has to carry — "the check
        /// failed" on its own is a line nobody can act on.
        /// </summary>
        internal const string Because = "No such host is known";

        /// <summary>
        /// How many times it was asked, so that asking again can be told from
        /// an answer being remembered.
        /// </summary>
        internal int TimesAsked { get; private set; }

        /// <inheritdoc/>
        public string? Waiting()
        {
            TimesAsked++;

            throw new HttpRequestException(Because);
        }

        /// <inheritdoc/>
        public void Fetch() =>
            throw new InvalidOperationException("nothing was found to fetch");

        /// <inheritdoc/>
        public void ApplyOnExit() =>
            throw new InvalidOperationException("nothing was found to apply");
    }
}
