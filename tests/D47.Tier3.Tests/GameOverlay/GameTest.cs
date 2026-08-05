using System;
using System.Diagnostics.CodeAnalysis;

using D47.GameOverlay;

using Xunit;

namespace D47.Tier3.Tests.GameOverlay;

/// <summary>
/// Base for every test that needs Elite Dangerous actually running.
///
/// <para>
/// It fails rather than skips. That is the difference between Tier 3 and the
/// desktop tests in <c>D47.Tier1.Tests.Panel</c>, which skip on a machine
/// somebody is using: those can run anywhere nobody is typing, including CI, so
/// skipping costs nothing. These run in one place — the dev PC, with the game
/// up on a second monitor — and there is nowhere else for them to be run
/// later. A skip there is a green build that checked nothing.
/// </para>
///
/// <para>
/// The consequence, which is deliberate: <c>dotnet test</c> across the whole
/// solution goes red on a machine with no game running. <c>ci.slnf</c> does not
/// name this project, so CI is unaffected.
/// </para>
/// </summary>
[Collection(Collection)]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification =
        "The test classes that inherit this are public, as xUnit requires, and a public "
        + "class cannot derive from an internal one. The rule does not recognise this as a "
        + "test type because it holds no facts of its own.")]
public abstract class GameTest
{
    /// <summary>
    /// The collection name shared by every test that drives the real desktop
    /// with the game on it. There is one desktop and one running game, and
    /// xUnit runs test classes in parallel unless told otherwise.
    /// </summary>
    private const string Collection = "The desktop, with Elite running";

    /// <summary>
    /// Why a Tier 3 test failed, in the words somebody reading the output needs
    /// — including what to do about it, because "not running" is a fact about
    /// the machine rather than a defect in the code.
    /// </summary>
    internal const string NeedsTheGame =
        "Elite Dangerous is not running. These are Tier 3 tests: they prove the overlay draws "
        + "over the real game, so there is no stand-in for it and nowhere else to run them. "
        + "Start Elite in borderless windowed mode — nothing can draw over fullscreen "
        + "exclusive — and run them again.";

    /// <summary>
    /// Finds the game, or fails the test saying it is not there.
    /// </summary>
    /// <exception cref="InvalidOperationException">Elite is not running.</exception>
    protected GameTest()
    {
        Game = EliteWindow.Find() ?? throw new InvalidOperationException(NeedsTheGame);
    }

    /// <summary>
    /// The running game, as it was when the test started.
    /// </summary>
    protected EliteWindow Game { get; }
}
