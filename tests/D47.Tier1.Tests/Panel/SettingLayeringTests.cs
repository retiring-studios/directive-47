using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// A setting has two layers and a Commander only ever sees one of them: what
/// Directive 47 ships with, and what they chose instead.
///
/// <para>
/// Asserted through <see cref="Zoom"/> rather than against the store, because
/// the claim is about the layering and only a real consumer has a default to
/// layer over. A store round trip would prove the file works and say nothing
/// about what happens when the file is silent.
/// </para>
///
/// <para>
/// Zoom is the consumer used because its default is a number anyone can check —
/// 100% is life size — and because it is the one setting a Commander is likely
/// to change and then want back.
/// </para>
/// </summary>
public class SettingLayeringTests
{
    [Fact]
    public void Setting_NobodyHasChanged_IsWhatDirective47ShipsWith()
    {
        using var folder = new TemporaryStore();

        new Zoom(folder.Settings(), Ignored).AsSaid.ShouldBe("100%");
    }

    [Fact]
    public void Setting_TheCommanderChanged_IsTheirsRatherThanTheDefault()
    {
        using var folder = new TemporaryStore();

        folder.Settings().Write(Zoom.HowBig, "150");

        new Zoom(folder.Settings(), Ignored).AsSaid.ShouldBe("150%");
    }

    [Fact]
    public void Setting_TheCommanderClears_IsTheDefaultAgain()
    {
        using var folder = new TemporaryStore();

        folder.Settings().Write(Zoom.HowBig, "150");
        folder.Settings().Forget(Zoom.HowBig);

        // Back to the shipped answer without anybody having to know what it
        // was. That is the whole point of the override being a separate layer
        // rather than the file holding every value.
        new Zoom(folder.Settings(), Ignored).AsSaid.ShouldBe("100%");
    }

    [Fact]
    public void Setting_TheCommanderClears_IsGoneFromTheFileRatherThanBlank()
    {
        using var folder = new TemporaryStore();

        folder.Settings().Write(Zoom.HowBig, "150");
        folder.Settings().Forget(Zoom.HowBig);

        // An empty value is a value, and the next reader has to guess whether
        // the Commander meant "the default" or "nothing". Gone says it once.
        System.IO.File.ReadAllText(folder.SettingsFile)
            .ShouldNotContain(Zoom.HowBig);
    }

    /// <summary>
    /// These facts are about what a setting reads as, and a complaint would
    /// mean one of them had stopped being about that.
    /// </summary>
    /// <param name="ignored">Whatever went wrong, which should be none.</param>
    private static void Ignored(string ignored)
    {
        // Deliberately empty.
    }
}
