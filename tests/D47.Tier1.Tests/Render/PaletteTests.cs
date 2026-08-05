using System.Collections.Generic;
using System.Windows.Media;

using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Render;

/// <summary>
/// Directive 47 looks like Elite Dangerous by default. The colors are the game's
/// own, sampled from a stock install's cockpit HUD with the identity color
/// matrix on record proving nothing had transformed them — see
/// [#100](https://github.com/retiring-studios/directive-47/issues/100).
///
/// <para>
/// A color is a property of what is drawn rather than of any one surface, so it
/// is set once on the shared render and the panel and both overlays get it
/// together. That is the same reasoning the typeface is set here under, and it
/// is [#112](https://github.com/retiring-studios/directive-47/issues/112)'s
/// invariant.
/// </para>
///
/// <para>
/// Every fact here reads the color off the visual tree the render actually
/// built, rather than comparing a hex string back to itself. A brush that never
/// resolved is null and a null brush paints nothing without complaining, so a
/// test written the other way agrees with the XAML while the panel is showing an
/// unpainted surface.
/// </para>
///
/// <para>
/// Two facts because two colors are drawn with. The other five the issue records
/// have no consumer and are not in the render, so there is nothing here to ask
/// about them.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PaletteTests
{
    /// <summary>
    /// Elite's own menu background. Explicitly not black — black is what a dark
    /// surface defaults to when nobody chooses.
    /// </summary>
    private static readonly Color Background = Color.FromRgb(0x1A, 0x1A, 0x1A);

    /// <summary>
    /// The emphasis end of the HUD's orange ramp, at 13.19:1 on the background.
    /// The HUD's primary orange is the ramp's tightest value at 7.46:1 and this
    /// is the color that would carry the most text, so body text takes the
    /// headroom instead.
    /// </summary>
    private static readonly Color BodyText = Color.FromRgb(0xFF, 0xE0, 0x00);

    [Fact]
    public void Render_ForTheSurfaceEverythingSitsOn_FillsItWithElitesBackground()
    {
        Rendering.BackgroundOf(Fixtures.HelpsAnswer()).ShouldBe(
            Background,
            customMessage:
                "the render's background is the one the panel and both overlays show, and "
                + "null here is a brush that never resolved rather than a color");
    }

    [Fact]
    public void Render_ForEveryLineItPutsOnScreen_WritesItInElitesHudYellow()
    {
        IReadOnlyList<Color?> inks = Rendering.ForegroundsFor(Fixtures.HelpsAnswer());

        // Not empty first, because a render that put nothing on screen satisfies
        // every claim about what color it put there.
        inks.ShouldNotBeEmpty();
        inks.ShouldAllBe(
            ink => ink == BodyText,
            customMessage:
                "body text is #FFE000 throughout — a line that came back some other color "
                + "is a template that painted over the palette");
    }
}
