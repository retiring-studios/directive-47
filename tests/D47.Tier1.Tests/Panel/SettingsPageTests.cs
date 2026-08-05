using System.Collections.Generic;
using System.Windows.Input;

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
    [Fact]
    public void SettingsPage_ForASettingNobodyChose_ShowsTheDefaultBehindAnEmptyBox()
    {
        using var folder = new TemporaryStore();

        (IReadOnlyList<string> boxes, IReadOnlyList<string> shown) =
            StaThread.Run(() =>
        {
            var page = SettingsPage.For(folder.Settings(), Ignored);

            return (VisualTree.ValuesIn(page), VisualTree.TextIn(page));
        });

        // Empty box, and the shipped answer visible behind it. A blank row is
        // true of the file and useless to a Commander, who cannot be told what
        // the hotkey is by being shown nothing.
        boxes.ShouldAllBe(box => box.Length == 0);

        shown.ShouldContain(
            Settings.DefaultFor(ChosenHotkey.WhatItIsCalled),
            "the page should say what the hotkey is when nobody has changed it");
    }

    [Fact]
    public void SettingsPage_WhenTheDefaultIsOnlyShown_DoesNotOfferItAsAChange()
    {
        using var folder = new TemporaryStore();

        List<(string Key, string Value)> changed = [];

        StaThread.Run(() =>
        {
            var page = SettingsPage.For(
                folder.Settings(), (key, value) => changed.Add((key, value)));

            // What tabbing past an untouched row does. If the default were in
            // the box rather than behind it, this would hand the shipped answer
            // back as a change and write it into the file as an override —
            // which is the two layers becoming one.
            page.Change(Zoom.HowBig, string.Empty);

            return true;
        });

        changed.ShouldHaveSingleItem().Value.ShouldBeEmpty(
            "an untouched row reports nothing chosen, not the default");
    }
    [Fact]
    public void HotkeyRow_WhenACombinationIsPressed_TakesItWithoutBeingSpelled()
    {
        using var folder = new TemporaryStore();

        List<(string Key, string Value)> changed = [];

        StaThread.Run(() =>
        {
            var page = SettingsPage.For(
                folder.Settings(), (key, value) => changed.Add((key, value)));

            page.Pressed(ModifierKeys.Control | ModifierKeys.Alt, Key.E);

            return true;
        });

        // Spelled by the same thing that writes the file, which is the whole
        // point: nobody has to know whether it wants "ctrl" or "Control".
        changed.ShouldHaveSingleItem().ShouldBe(
            (ChosenHotkey.WhatItIsCalled,
             new Combination(ModifierKeys.Control | ModifierKeys.Alt, Key.E).ToString()));
    }

    [Fact]
    public void HotkeyRow_WhileOnlyAModifierIsDown_HasNotBeenGivenACombination()
    {
        using var folder = new TemporaryStore();

        List<(string Key, string Value)> changed = [];

        StaThread.Run(() =>
        {
            var page = SettingsPage.For(
                folder.Settings(), (key, value) => changed.Add((key, value)));

            page.Pressed(ModifierKeys.Control, Key.LeftCtrl);

            return true;
        });

        // Holding Ctrl on the way to Ctrl+Alt+E must not claim Ctrl. Every
        // combination is reached through its own modifiers going down first.
        changed.ShouldBeEmpty();
    }
}
