using System.IO;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Data;

/// <summary>
/// The settings file, and the one thing that makes it a different kind of file
/// from the one beside it.
///
/// <para>
/// <c>remembered.json</c> shrugs at a key it does not know, because a leftover
/// from an older version is harmless. This one does not, because a key it does
/// not know means somebody edited the file and got it wrong, and applying the
/// half of it that parsed would leave them looking for the half that did not.
/// </para>
///
/// <para>
/// Against the application's real schema rather than one invented here. A test
/// that declares its own list of settings keeps passing after somebody adds a
/// setting and forgets to register it.
/// </para>
/// </summary>
public class SettingsStoreTests
{
    [Fact]
    public void Settings_WithAKeyTheSchemaDoesNotKnow_AreNotAppliedAtAll()
    {
        using var temporary = new TemporaryStore();

        // One good key and one misspelled one. The good one is what makes this
        // a test about refusing the file rather than about skipping a line.
        temporary.WriteSettings(
            """{ "panel zoom": "150", "overlay hoktey": "Ctrl+D" }""");

        temporary.Settings().Read("panel zoom").ShouldBeNull(
            "a settings file holding a key we do not know is not one we can act "
            + "on, including the parts of it we recognize");
    }

    [Fact]
    public void Settings_WithAKeyTheSchemaDoesNotKnow_SayWhichKey()
    {
        using var temporary = new TemporaryStore();

        temporary.WriteSettings("""{ "overlay hoktey": "Ctrl+D" }""");

        temporary.Settings();

        // Naming it is the whole of "loudly". A Commander who mistyped a key
        // needs to be told which one; a record saying only that the file was
        // rejected sends them reading the whole thing.
        temporary.Recorded.ShouldHaveSingleItem().ShouldContain("overlay hoktey");
    }

    [Fact]
    public void Settings_WithOnlyKnownKeys_AreApplied()
    {
        using var temporary = new TemporaryStore();

        temporary.WriteSettings("""{ "panel zoom": "150" }""");

        temporary.Settings().Read("panel zoom").ShouldBe("150");

        temporary.Recorded.ShouldBeEmpty("a file we understand is not an event");
    }

    [Fact]
    public void Settings_WhenNothingWasEverSet_AreEmptyAndSayNothing()
    {
        using var temporary = new TemporaryStore();

        temporary.Settings().Read("panel zoom").ShouldBeNull(
            "a Commander who has changed nothing has overridden nothing");

        temporary.Recorded.ShouldBeEmpty(
            "a first run is not an event, and recording one would make every "
            + "first run look like a fault");
    }

    [Fact]
    public void Setting_WhenChanged_IsThereOnTheNextRun()
    {
        using var temporary = new TemporaryStore();

        temporary.Settings().Write("panel zoom", "150");

        temporary.Settings().Read("panel zoom").ShouldBe("150");
    }

    [Fact]
    public void Setting_WhenWritten_GoesToSettingsAndNotToRunningMemory()
    {
        using var temporary = new TemporaryStore();

        temporary.Settings().Write("panel zoom", "150");

        // Two files is the point of the story. A settings store that wrote into
        // remembered.json would pass every other fact here.
        File.ReadAllText(temporary.SettingsFile).ShouldContain("panel zoom");
        temporary.Files().ShouldBe(["settings.json"]);
    }
}
