using System.Collections.Generic;

using D47.Panel;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// The page a Commander changes a setting on.
///
/// <para>
/// In process and on an STA thread: the page is built, read and driven without
/// a window, which is what keeps these in CI. What a click does to the rest of
/// the application is the composition root's, and is asserted where that is.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class SettingsPageTests
{
    [Fact]
    public void SettingsPage_ShowsEverySettingDirective47Has()
    {
        using var folder = new TemporaryStore();

        IReadOnlyList<string> shown = StaThread.Run(() =>
            VisualTree.TextIn(SettingsPage.For(folder.Settings(), Ignored)));

        // The schema is the list, so a setting added without a row is a setting
        // no Commander can reach. Asserted against Settings.Known rather than
        // against three names typed here, which would go stale silently.
        foreach (string setting in Settings.Known)
        {
            shown.ShouldContain(
                line => line.Contains(SettingsPage.LabelFor(setting)),
                customMessage: $"{setting} has no row on the settings page");
        }
    }

    [Fact]
    public void SettingsPage_ShowsWhatIsInForceRatherThanTheDefault()
    {
        using var folder = new TemporaryStore();

        folder.Settings().Write(Zoom.HowBig, "150");

        IReadOnlyList<string> showing = StaThread.Run(() =>
            VisualTree.ValuesIn(SettingsPage.For(folder.Settings(), Ignored)));

        showing.ShouldContain(
            "150",
            customMessage:
                "a page showing the default while the panel is at 150% is lying");
    }

    [Fact]
    public void SettingsPage_WhenASettingIsChanged_SaysWhichAndWhatTo()
    {
        using var folder = new TemporaryStore();

        List<(string Key, string Value)> changed = [];

        StaThread.Run(() =>
        {
            var page = SettingsPage.For(
                folder.Settings(), (key, value) => changed.Add((key, value)));

            page.Change(Zoom.HowBig, "150");

            return true;
        });

        // The page reports rather than writes. Who cares about a changed
        // setting is the composition root's knowledge, and a page that wrote
        // the file itself would leave the running application unaware.
        changed.ShouldHaveSingleItem().ShouldBe((Zoom.HowBig, "150"));
    }

    private static void Ignored(string key, string value)
    {
        // Deliberately empty.
    }
}
