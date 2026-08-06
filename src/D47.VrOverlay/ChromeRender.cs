using System;
using System.Windows.Media;

using D47.Placement;
using D47.Render;

namespace D47.VrOverlay;

/// <summary>
/// The chrome, as pixels: the bar that moves the overlay, the four handles that
/// scale it, and nothing at all where the panel shows through.
///
/// <para>
/// A second texture on a second quad. <c>PanelRender</c>'s invariant keeps
/// anything VR-specific out of the shared render, and a grab affordance is
/// VR-specific by definition — the panel and the game overlay have no pointer.
/// See the Architecture section of <c>docs/decisions.md</c>.
/// </para>
///
/// <para>
/// Bytes rather than a visual tree, deliberately. Everything here is flat
/// rectangles, and building them in WPF would tie the chrome to a
/// single-threaded apartment — which the thread that watches the controllers is
/// not, and should not have to become.
/// </para>
/// </summary>
public static class ChromeRender
{
    /// <summary>
    /// How many pixels a metre of chrome is drawn at.
    ///
    /// <para>
    /// The chrome is solid rectangles, so this only has to be fine enough that
    /// an edge does not read as a staircase — the compositor filters what it is
    /// given. On the shipped quad it makes a texture of roughly 287 by 192,
    /// which is smaller than the panel's own and costs nothing to rebuild.
    /// </para>
    ///
    /// <para>
    /// A number to react to rather than a measured one.
    /// </para>
    /// </summary>
    private const float PixelsPerMetre = 512;

    /// <summary>
    /// How visible the chrome is when nothing is aimed at it.
    ///
    /// <para>
    /// Present rather than hidden: a Commander who has never been told the bar
    /// exists finds it by seeing it. Faint enough that it is furniture rather
    /// than something competing with the render for attention.
    /// </para>
    /// </summary>
    private const byte Resting = 90;

    /// <summary>
    /// How visible the part being pointed at is.
    /// </summary>
    private const byte Lit = 255;

    /// <summary>
    /// The chrome for a panel, with whatever is being pointed at picked out.
    /// </summary>
    ///
    /// <remarks>
    /// Every part is drawn every time rather than the lit one being painted over
    /// a cached texture. The whole thing is a few tens of kilobytes of flat fill,
    /// and a cache would be a second copy of the geometry to keep in step with
    /// <see cref="Chrome.Around"/>.
    /// </remarks>
    /// <param name="panel">The panel the chrome goes around.</param>
    /// <param name="lit">What the controller is aimed at, if anything.</param>
    /// <returns>The pixels, and how wide and tall they are.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of the panel's pose is not a finite number.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The panel has no width or no height.
    /// </exception>
    public static (byte[] Pixels, int Width, int Height) Take(Board panel, Grabbed lit)
    {
        Board around = Chrome.Around(panel);

        int width = (int)MathF.Ceiling(around.Width * PixelsPerMetre);
        int height = (int)MathF.Ceiling(around.Height * PixelsPerMetre);

        byte[] pixels = new byte[width * height * 4];

        // Where each piece is comes from D47.Placement, so the chrome is drawn
        // exactly where the hit test says it is. Working it out here from the
        // shares would be a second answer to the same question.
        foreach (Patch patch in Chrome.Parts(panel))
        {
            Fill(pixels, width, height, around, patch, patch.What == lit);
        }

        return (pixels, width, height);
    }

    /// <summary>
    /// Paints a rectangle given in the chrome's own metres.
    /// </summary>
    ///
    /// <remarks>
    /// The colour is Elite's own HUD yellow, the one the render writes every line
    /// of body text in, dimmed rather than replaced. <c>Palette</c> says the five
    /// undeclared colours from the sampling pass stay undeclared because nothing
    /// draws with them, and this draws with none of them either.
    /// </remarks>
    private static void Fill(
        byte[] pixels, int width, int height, Board around, Patch patch, bool lit)
    {
        Color colour = Palette.BodyText.Color;
        byte alpha = lit ? Lit : Resting;

        int fromX = ToPixelAcross(patch.Left, around, width);
        int toX = ToPixelAcross(patch.Right, around, width);

        // Top and bottom swap on the way in, because a texture counts rows
        // downwards and the cockpit counts metres upwards.
        int fromY = ToPixelDown(patch.Top, around, height);
        int toY = ToPixelDown(patch.Bottom, around, height);

        for (int y = Math.Max(fromY, 0); y < Math.Min(toY, height); y++)
        {
            for (int x = Math.Max(fromX, 0); x < Math.Min(toX, width); x++)
            {
                int at = ((y * width) + x) * 4;

                pixels[at] = colour.R;
                pixels[at + 1] = colour.G;
                pixels[at + 2] = colour.B;
                pixels[at + 3] = alpha;
            }
        }
    }

    private static int ToPixelAcross(float metres, Board around, int width) =>
        (int)MathF.Round((metres + (around.Width / 2)) / around.Width * width);

    private static int ToPixelDown(float metres, Board around, int height) =>
        (int)MathF.Round(((around.Height / 2) - metres) / around.Height * height);
}
