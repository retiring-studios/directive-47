using D47.Data;
using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Data;

/// <summary>
/// What happens to an installed build the first time it runs a version that has
/// two files instead of one.
///
/// <para>
/// Deliberately temporary. There is no supported upgrade path from a prerelease
/// once 1.0.0 exists, so this and the code it covers come out then. It is here
/// because fifteen lines is cheaper than a Commander finding their hotkey gone.
/// </para>
/// </summary>
public class MigrationTests
{
    [Fact]
    public void Settings_LeftInRememberedByAnEarlierVersion_MoveToSettings()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().Write("panel zoom", "150");

        SettingsStore.MigrateFrom(temporary.Open(), temporary.Settings(), Settings.Known);

        temporary.Settings().Read("panel zoom").ShouldBe(
            "150", "a Commander who set the zoom keeps the zoom");
    }

    [Fact]
    public void Settings_OnceMoved_AreNoLongerInRemembered()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().Write("panel zoom", "150");

        SettingsStore.MigrateFrom(temporary.Open(), temporary.Settings(), Settings.Known);

        // Left behind, the old copy is a second answer to the same question,
        // and the next person to read remembered.json finds it.
        temporary.Open().Read("panel zoom").ShouldBeNull();
    }

    [Fact]
    public void RunningMemory_IsLeftWhereItIs()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().Write("game overlay left", "120");

        SettingsStore.MigrateFrom(temporary.Open(), temporary.Settings(), Settings.Known);

        temporary.Open().Read("game overlay left").ShouldBe(
            "120", "where the overlay was left is not a setting and does not move");
    }

    [Fact]
    public void Settings_AlreadySet_AreNotOverwrittenByTheOldCopy()
    {
        using var temporary = new TemporaryStore();

        temporary.Open().Write("panel zoom", "150");
        temporary.Settings().Write("panel zoom", "200");

        SettingsStore.MigrateFrom(temporary.Open(), temporary.Settings(), Settings.Known);

        // The newer file wins. Every run calls the migration, so it can run
        // after the Commander has already used the settings page, and undoing
        // them would be worse than never having moved anything.
        temporary.Settings().Read("panel zoom").ShouldBe("200");
    }

    [Fact]
    public void Migration_WhenThereIsNothingToMove_WritesNothing()
    {
        using var temporary = new TemporaryStore();

        SettingsStore.MigrateFrom(temporary.Open(), temporary.Settings(), Settings.Known);

        // Every run after the first, and every fresh install. Creating a
        // settings.json for a Commander who has chosen nothing is litter.
        temporary.Files().ShouldBeEmpty();
    }
}
