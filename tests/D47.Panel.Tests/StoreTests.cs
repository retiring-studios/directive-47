using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// Somewhere for the application to write things down and find them again next
/// time.
///
/// <para>
/// In process and in a temporary folder, so these run in CI without a desktop —
/// which is one of the story's own conditions. Nothing here launches anything.
/// </para>
///
/// <para>
/// The store is built before there is any way to change a setting, and before
/// anything is stored in it. That is deliberate: the overlay should stay where
/// it was put, and state can exist before a page exists to surface it. See
/// [#117](https://github.com/retiring-studios/directive-47/issues/117).
/// </para>
/// </summary>
public class StoreTests
{
    [Fact]
    public void Application_OnRestart_RemembersWhatItWasTold()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().Write("where", "top left");

        // A second open is the next run. Asking the same instance would prove
        // only that it can hold a value in memory, which is not the claim.
        temporary.Open().Read("where").ShouldBe("top left");
    }

    [Fact]
    public void Store_WhenToldSomethingTwice_KeepsTheSecondAnswer()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().Write("where", "top left");
        temporary.Open().Write("where", "bottom right");

        temporary.Open().Read("where").ShouldBe("bottom right");
    }

    [Fact]
    public void Store_WhenNothingWasEverWritten_StartsEmptyAndSaysNothing()
    {
        using var temporary = new TemporaryStore();

        Store store = temporary.Open();

        store.Read("where").ShouldBeNull("a machine that has never been asked knows nothing");

        temporary.Recorded.ShouldBeEmpty(
            "a first run is not an event, and recording one would make every first run "
            + "look like a fault");
    }

    [Fact]
    public void Store_WhenTheFileCannotBeRead_StartsEmptyAndLeavesARecord()
    {
        using var temporary = new TemporaryStore();

        temporary.Corrupt("this is not a store");

        Store store = temporary.Open();

        store.Read("where").ShouldBeNull("an unreadable store is no answer, not a failure");

        // Told apart from a missing one, deliberately. A file that is not there
        // is a machine that has never been asked; a file that is there and
        // cannot be read is our defect or somebody's editor, and only one of the
        // two is worth anybody's attention.
        temporary.Recorded.ShouldHaveSingleItem().ShouldContain(
            temporary.File,
            customMessage: "the record has to say which file, or it cannot be acted on");
    }

    [Fact]
    public void Store_WhenTheFileCannotBeRead_IsWritableAgainAfterwards()
    {
        using var temporary = new TemporaryStore();

        temporary.Corrupt("this is not a store");

        temporary.Open().Write("where", "top left");

        // Recovering rather than staying broken. A store that starts empty every
        // time but can never be written again is a store that has quietly
        // stopped working, and nothing after the first run would say so.
        temporary.Open().Read("where").ShouldBe("top left");
    }

    [Fact]
    public void Store_WhenOpenedWithoutBeingToldWhere_KeepsItselfBesideTheLog()
    {
        // Reading is all this does — a store nobody has written to creates no
        // file — so it is safe to ask on the machine running the tests.
        Store.Open(Nowhere).Location.ShouldBe(
            ApplicationData.File("remembered.json"),
            customMessage: "the store and the log were supposed to settle this once, together");
    }

    [Fact]
    public void Store_WhenItWrites_LeavesNothingBehindButTheStore()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().Write("where", "top left");

        // Writing safely means writing somewhere else first. That is the right
        // way to do it and it is invisible when it works — the visible failure
        // is a folder slowly filling with the debris of every write.
        temporary.Files().ShouldBe(["remembered.json"]);
    }

    /// <summary>
    /// Somewhere for a record to go that is not the maintainer's own log. The
    /// fact above only asks where the store would live, and a real logger passed
    /// there would be a test reaching into <c>%LOCALAPPDATA%</c>.
    /// </summary>
    private static void Nowhere(string ignored)
    {
        // Deliberately empty.
    }
}
