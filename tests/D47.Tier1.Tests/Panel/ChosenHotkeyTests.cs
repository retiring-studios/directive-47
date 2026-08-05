using System.Collections.Generic;
using System.Windows.Input;

using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Which combination toggles the overlay: what the Commander chose, what
/// happens when nothing was chosen, and what happens when what is written down
/// is not a combination at all.
///
/// <para>
/// Nothing here claims a hotkey. Choosing is reading a line out of a file and
/// deciding whether it means anything, which needs no desktop and no message
/// loop — so these run everywhere rather than behind the desktop gate. Claiming
/// the combination, and changing it while the application is running, are
/// asserted in <see cref="HotkeyTests"/>, where a real registration already
/// lives.
/// </para>
/// </summary>
public class ChosenHotkeyTests
{
    /// <summary>
    /// What the store calls it. Spelled the way somebody would say it, because
    /// until there is a settings page, editing <c>remembered.json</c> is how
    /// this value changes — and a test naming the key is what stops the spelling
    /// drifting from the one already in the Commander's file.
    /// </summary>
    private const string WhatItIsCalled = "overlay hotkey";

    /// <summary>
    /// What the application has shipped with since
    /// [#103](https://github.com/retiring-studios/directive-47/issues/103), and
    /// what it falls back to.
    /// </summary>
    private const string TheDefault = "Ctrl+Alt+D";

    /// <summary>
    /// Where this test's absences are written down. One per test, because xUnit
    /// builds the class afresh for each fact.
    /// </summary>
    private readonly List<string> _recorded = [];

    [Fact]
    public void OverlayHotkey_WhenNothingWasEverRemembered_IsTheOneItAlwaysWas()
    {
        using var remembered = new TemporaryStore();

        Combination chosen = ChosenHotkey.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.ToString().ShouldBe(TheDefault);

        // And silently. A first run has chosen nothing, which is every machine
        // once and is not an event — the same line DataStore draws between a file
        // that is not there and one that will not open.
        _recorded.ShouldBeEmpty("having chosen nothing yet is not an absence to report");
    }

    [Fact]
    public void OverlayHotkey_OnRestart_IsTheCombinationThatWasChosen()
    {
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, "Ctrl+Shift+F9");

        // A second open is the next run. A chosen combination is only worth
        // having if it survives one.
        Combination chosen = ChosenHotkey.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.Modifiers.ShouldBe(ModifierKeys.Control | ModifierKeys.Shift);
        chosen.Key.ShouldBe(Key.F9);
        _recorded.ShouldBeEmpty("a combination it could use is nothing to write down");
    }

    [Fact]
    public void OverlayHotkey_WhenSpelledInAnotherOrder_IsStillTheSameCombination()
    {
        // Modifiers are a set, and somebody hand-editing a file will write them
        // in whatever order they think of them. Reading it back in one fixed
        // order is what makes the value round-trip rather than mutate itself
        // the first time the application writes it down again.
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, "shift+CTRL+f9");

        Combination chosen = ChosenHotkey.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.ToString().ShouldBe("Ctrl+Shift+F9");
        _recorded.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("banana")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Alt+")]
    [InlineData("Hyper+D")]
    [InlineData("Ctrl+Alt+Banana")]
    public void OverlayHotkey_WhenWhatWasRememberedIsNotACombination_StartsAtTheDefault(
        string nonsense)
    {
        // The file is indented JSON in a folder the Commander can open, so
        // somebody editing it by hand is the expected way this value changes
        // until there is a settings page. An unusable line is no answer rather
        // than a failure — refusing to start over a hand-edited setting whose
        // whole purpose is to be adjusted would be the worse trade, which is
        // the argument the overlay's opacity already settled.
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, nonsense);

        Combination chosen = ChosenHotkey.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.ToString().ShouldBe(TheDefault);

        _recorded.ShouldHaveSingleItem().ShouldContain(
            WhatItIsCalled,
            customMessage: "a setting nobody could use is exactly the kind of absence to record");
    }

    [Fact]
    public void OverlayHotkey_WhenWhatWasRememberedIsANumber_IsNotReadAsAKeyCode()
    {
        // Enum.TryParse says yes to "1" and hands back Key.Cancel, so a
        // Commander writing the digit on their keyboard would silently get a
        // key they have never heard of. Spelling is what round-trips here, not
        // the number behind it.
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, "Ctrl+Alt+1");

        Combination chosen = ChosenHotkey.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.ToString().ShouldBe(TheDefault);
        _recorded.ShouldHaveSingleItem().ShouldContain(WhatItIsCalled);
    }

    [Fact]
    public void OverlayHotkey_WithNoModifierAtAll_IsNotACombinationWorthClaiming()
    {
        // Windows would take it. A bare letter claimed system-wide swallows
        // that key everywhere on the machine — including in the game — and the
        // Commander's next clue is that typing has stopped working. That is the
        // same shape as an opacity outside 0 to 1: the API accepts it, and it
        // is not what the setting means.
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, "D");

        Combination chosen = ChosenHotkey.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.ToString().ShouldBe(TheDefault);
        _recorded.ShouldHaveSingleItem().ShouldContain(WhatItIsCalled);
    }

    [Fact]
    public void OverlayHotkey_WhenWhatWasRememberedIsNotACombination_QuotesWhatItFound()
    {
        // The fix is a line in a file, and nobody can correct a line they
        // cannot identify.
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, "Hyper+D");

        ChosenHotkey.ChosenIn(remembered.Settings(), _recorded.Add);

        string said = _recorded.ShouldHaveSingleItem();

        said.ShouldContain("\"Hyper+D\"");
        said.ShouldContain(TheDefault, customMessage: "it should say what it is using instead");
    }
}
