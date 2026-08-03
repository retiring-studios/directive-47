using System.Windows.Media;

namespace D47.Render;

/// <summary>
/// Directive 47's colors, in one place.
///
/// <para>
/// There were two places and nothing made them agree: <c>CapabilityView.xaml</c>
/// and <c>MainWindow.xaml</c> each named <c>#FF0B0D10</c> independently. The
/// window now names nothing and shows what the render draws.
/// </para>
///
/// <para>
/// These are Elite Dangerous' own, sampled from the in-flight cockpit HUD of a
/// stock install — flat fills at zero variance, with the game's identity color
/// matrix on record proving nothing had transformed them. The sampling, the
/// evidence behind each value and the contrast figures are on
/// [#100](https://github.com/retiring-studios/directive-47/issues/100).
/// </para>
///
/// <para>
/// A color is a property of what is drawn rather than of any one surface, so
/// these are set on the shared render and the panel and both overlays get them
/// together — the same reasoning the typeface is set under.
/// </para>
///
/// <para>
/// C# rather than a merged <c>ResourceDictionary</c>, for two reasons. A
/// misspelled member here does not compile, where a misspelled resource key
/// fails at the moment the XAML loads; and a second XAML part means a second
/// package stream opened inside <c>LoadComponent</c>, which surfaced a race in
/// WPF's own packaging when test classes built views in parallel.
/// </para>
///
/// <para>
/// The palette has seven values and two of them are here. The other five — the
/// primary orange, white, cyan, red and the dim fill — are recorded on the issue
/// because they came out of one sampling pass and re-sourcing them would mean
/// recapturing and resampling. Nothing draws with them, so nothing declares
/// them.
/// </para>
/// </summary>
public static class Palette
{
    /// <summary>
    /// <c>#1A1A1A</c>, what everything is drawn on.
    /// </summary>
    ///
    /// <remarks>
    /// Elite's own background, and explicitly not black. <c>#000000</c> is what
    /// a dark surface defaults to when nobody chooses; this is a choice, and it
    /// is what makes Directive 47 look like it belongs with the game rather than
    /// beside it.
    /// </remarks>
    public static SolidColorBrush Background { get; } = Frozen(Color.FromRgb(0x1A, 0x1A, 0x1A));

    /// <summary>
    /// <c>#FFE000</c>, what every line of body text is written in.
    /// </summary>
    ///
    /// <remarks>
    /// 13.19:1 on <see cref="Background"/>, and the best-evidenced value in the
    /// palette. The HUD's primary orange <c>#FF8C00</c> is what the game labels
    /// with, and at 7.46:1 it is the tightest value in the ramp — which would
    /// put the least headroom under the color carrying the most text. This is
    /// the emphasis end of the same ramp, so it is still unambiguously Elite's
    /// HUD.
    ///
    /// <para>
    /// A ratio is a monitor measurement. It says nothing about legibility at a
    /// headset's angular resolution, which stays unverified per
    /// [#112](https://github.com/retiring-studios/directive-47/issues/112).
    /// </para>
    /// </remarks>
    public static SolidColorBrush BodyText { get; } = Frozen(Color.FromRgb(0xFF, 0xE0, 0x00));

    /// <summary>
    /// A brush nothing can change, which is also what lets every surface share
    /// one. An unfrozen brush belongs to the thread that made it, and the panel
    /// and the overlays do not share a thread.
    /// </summary>
    /// <param name="color">The color to paint.</param>
    /// <returns>The frozen brush.</returns>
    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);

        brush.Freeze();

        return brush;
    }
}
