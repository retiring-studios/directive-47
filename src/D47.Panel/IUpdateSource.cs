namespace D47.Panel;

/// <summary>
/// Where a newer Directive 47 comes from, and what can be done about one.
/// </summary>
///
/// <remarks>
/// <para>
/// A seam in front of the updater, for the same reason <c>IGameOverlay</c> is
/// one: the decisions are testable and the adapter is not. A real updater cannot
/// be asked whether it would have applied something, and a test that let it find
/// out would install over the machine running it.
/// </para>
/// <para>
/// Three calls, in the order they happen: what is there, get it ready, hand it
/// over on the way out. Which of them is slow, and whether getting it ready
/// happens on a thread of its own, belongs to whatever implements this —
/// <see cref="Updates"/> only decides when each is right.
/// </para>
/// </remarks>
internal interface IUpdateSource
{
    /// <summary>
    /// The version waiting, or <see langword="null"/> if the running copy is
    /// current.
    /// </summary>
    /// <returns>The version, or <see langword="null"/>.</returns>
    string? Waiting();

    /// <summary>
    /// Gets whatever <see cref="Waiting"/> found ready to be applied.
    /// </summary>
    ///
    /// <remarks>
    /// Called while the application is still running, because the updater's
    /// minute starts when it is told to apply and a download inside that minute
    /// is a download that can miss it.
    /// </remarks>
    void Fetch();

    /// <summary>
    /// Tells the updater to apply what was fetched once this process has gone.
    /// </summary>
    ///
    /// <remarks>
    /// Returns rather than blocking. The point of the whole arrangement is that
    /// the application exits normally and the updater does its work afterwards,
    /// so anything that waited here would be holding up the exit it is waiting
    /// for.
    /// </remarks>
    void ApplyOnExit();
}
