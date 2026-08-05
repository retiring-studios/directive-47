using System.Collections.Generic;

using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Which combination is held to talk: what the Commander chose, what happens
/// when nothing was chosen, and what happens when what is written down is not a
/// combination at all.
///
/// <para>
/// Nothing here claims a hotkey, for the reason
/// <see cref="ChosenHotkeyTests"/> gives — choosing is reading a line and
/// deciding whether it means anything, and needs no desktop. Claiming it and
/// holding it are asserted in <see cref="HotkeyTests"/>, where a real
/// registration already lives.
/// </para>
/// </summary>
public class PushToTalkTests
{
    /// <summary>
    /// What the store calls it. Named here as well as in the production constant,
    /// for the reason <see cref="ChosenHotkeyTests"/> names the overlay's: a key
    /// whose spelling drifts is a setting the Commander's file stops matching.
    /// </summary>
    private const string WhatItIsCalled = "push to talk";

    /// <summary>
    /// Deliberately not the overlay's combination. Two settings defaulting to one
    /// key would mean the second claim always loses, and the Commander would see
    /// a feature that silently does not work.
    /// </summary>
    private const string TheDefault = "Ctrl+Alt+V";

    private readonly List<string> _recorded = [];

    [Fact]
    public void PushToTalk_WhenNothingWasEverRemembered_IsTheOneItShipsWith()
    {
        using var remembered = new TemporaryStore();

        Combination chosen = PushToTalk.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.ToString().ShouldBe(TheDefault);
        _recorded.ShouldBeEmpty("having chosen nothing yet is not an absence to report");
    }

    [Fact]
    public void PushToTalk_OnRestart_IsTheCombinationThatWasChosen()
    {
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, "Ctrl+Shift+F9");

        Combination chosen = PushToTalk.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.ToString().ShouldBe("Ctrl+Shift+F9");
    }

    [Fact]
    public void PushToTalk_WhenWhatWasRememberedIsNotACombination_FallsBackAndSaysSo()
    {
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, "banana");

        Combination chosen = PushToTalk.ChosenIn(remembered.Settings(), _recorded.Add);

        chosen.ToString().ShouldBe(TheDefault);
        _recorded.ShouldHaveSingleItem().ShouldContain("banana");
    }

    [Fact]
    public void PushToTalk_IsNotTheSameCombinationAsTheOverlayHotkey()
    {
        // The two are claimed independently and system-wide, so equal defaults
        // would mean whichever is claimed second never registers at all.
        PushToTalk.ShippedWith.ShouldNotBe(ChosenHotkey.Familiar);
    }

    [Fact]
    public void PushToTalk_IsASettingTheSchemaKnowsAbout()
    {
        // settings.json fails loudly on a key it does not know, so a setting
        // written by the application and absent from the schema would make the
        // file the application itself wrote unloadable.
        Settings.Known.ShouldContain(WhatItIsCalled);
        Settings.DefaultFor(WhatItIsCalled).ShouldBe(TheDefault);
    }
}
