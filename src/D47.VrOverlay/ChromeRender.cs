using System;
using System.Collections.Generic;
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
    /// How thick a corner bracket's arm is, as a share of the handle's side.
    ///
    /// <para>
    /// A bracket rather than a filled square, which is what a handle was until
    /// the maintainer saw one in the headset. On the shipped quad a handle is six
    /// centimetres across, so a solid one is a fifth of the panel's height in
    /// ink at each corner — four blocks around something meant to be read.
    /// </para>
    ///
    /// <para>
    /// It changes the drawing and not the target. The handle is still the whole
    /// six-centimetre square as far as <c>Chrome.On</c> is concerned, so this
    /// makes the chrome quieter without making it harder to hit — which is the
    /// opposite trade from simply shrinking the handles.
    /// </para>
    ///
    /// <para>
    /// A number to react to rather than a measured one. A fifth leaves about a
    /// third of the ink a solid handle had.
    /// </para>
    /// </summary>
    private const float ArmShare = 0.2f;

    /// <summary>
    /// How much of the bar's width is actually inked, centred.
    ///
    /// <para>
    /// A short pill rather than a band the full width of the panel. The bar was
    /// the largest single piece of chrome by a wide margin — more ink than all
    /// four corners together — and it is the one the maintainer's screenshot
    /// showed as a slab across the bottom of the cockpit.
    /// </para>
    ///
    /// <para>
    /// This is Horizon OS's shape, which is the idiom
    /// <c>docs/decisions.md</c> already says the chrome follows.
    /// </para>
    /// </summary>
    private const float BarInkAcross = 0.3f;

    /// <summary>
    /// How much of the bar's height is inked, centred in it.
    /// </summary>
    private const float BarInkDown = 0.35f;

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
    /// <param name="shown">
    /// What the laser is near enough to be worth drawing. Nothing draws what is
    /// not in here, so <c>Near.Nothing</c> is an empty quad.
    /// </param>
    /// <returns>The pixels, and how wide and tall they are.</returns>
    /// <exception cref="ArgumentException">
    /// Some part of the panel's pose is not a finite number.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The panel has no width or no height.
    /// </exception>
    public static (byte[] Pixels, int Width, int Height) Take(
        Board panel, Grabbed lit, Near shown)
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
            if (!shown.Shows(patch.What))
            {
                continue;
            }

            foreach (Patch ink in Drawn(patch))
            {
                Fill(pixels, width, height, around, ink, patch.What == lit);
            }
        }

        return (pixels, width, height);
    }

    /// <summary>
    /// What actually gets painted for a piece of chrome: the bar as it is, and a
    /// corner handle as the two arms of a bracket.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Derived from the piece rather than worked out from the shares, so
    /// <c>Chrome.Parts</c> stays the one answer to where a grab target is. This
    /// only decides how much of that target is inked, which is a question about
    /// drawing and belongs in the thing that draws.
    /// </para>
    /// <para>
    /// The two outer edges, not the two inner ones. A handle straddles its
    /// corner, so the arms furthest from the panel trace the corner from outside
    /// it — the inner pair would draw a bracket lying across the render.
    /// </para>
    /// </remarks>
    /// <param name="piece">One piece of chrome, as <c>Chrome.Parts</c> gives it.</param>
    /// <returns>The rectangles to fill for it.</returns>
    private static IEnumerable<Patch> Drawn(Patch piece)
    {
        if (piece.What == Grabbed.Bar)
        {
            float across = (piece.Right - piece.Left) * BarInkAcross / 2;
            float down = (piece.Top - piece.Bottom) * BarInkDown / 2;

            float middleAcross = (piece.Left + piece.Right) / 2;
            float middleDown = (piece.Bottom + piece.Top) / 2;

            yield return piece with
            {
                Left = middleAcross - across,
                Right = middleAcross + across,
                Bottom = middleDown - down,
                Top = middleDown + down,
            };

            yield break;
        }

        float arm = (piece.Right - piece.Left) * ArmShare;

        bool alongTheTop =
            piece.What is Grabbed.TopLeftCorner or Grabbed.TopRightCorner;

        bool downTheLeft =
            piece.What is Grabbed.TopLeftCorner or Grabbed.BottomLeftCorner;

        yield return piece with
        {
            Bottom = alongTheTop ? piece.Top - arm : piece.Bottom,
            Top = alongTheTop ? piece.Top : piece.Bottom + arm,
        };

        yield return piece with
        {
            Left = downTheLeft ? piece.Left : piece.Right - arm,
            Right = downTheLeft ? piece.Left + arm : piece.Right,
        };
    }

    /// <summary>
    /// Paints a rectangle with radiused ends, given in the chrome's own metres.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The colour is Elite's own HUD yellow, the one the render writes every line
    /// of body text in, dimmed rather than replaced. <c>Palette</c> says the five
    /// undeclared colours from the sampling pass stay undeclared because nothing
    /// draws with them, and this draws with none of them either.
    /// </para>
    /// <para>
    /// Fully rounded: the radius is half the shorter side, so an arm is a stroke
    /// with round caps and the bar is a pill. Two arms meeting at a corner share
    /// that corner, so their union comes out radiused on the outside and square
    /// on the inside, which is what a bracket looks like.
    /// </para>
    /// <para>
    /// Antialiased by coverage rather than drawn hard and filtered by the
    /// compositor. A radius stepped in whole pixels reads as a staircase at the
    /// distance the overlay sits, and the compositor's filtering blurs a
    /// staircase rather than removing it.
    /// </para>
    /// <para>
    /// Written against alpha rather than blended into it. Nothing here overlaps
    /// anything else except an arm meeting its own elbow, where both want the
    /// same value — so the brighter of the two is the right answer and blending
    /// would darken the join.
    /// </para>
    /// </remarks>
    private static void Fill(
        byte[] pixels, int width, int height, Board around, Patch patch, bool lit)
    {
        Color colour = Palette.BodyText.Color;
        byte alpha = lit ? Lit : Resting;

        float left = ToPixelAcross(patch.Left, around, width);
        float right = ToPixelAcross(patch.Right, around, width);

        // Top and bottom swap on the way in, because a texture counts rows
        // downwards and the cockpit counts metres upwards.
        float top = ToPixelDown(patch.Top, around, height);
        float bottom = ToPixelDown(patch.Bottom, around, height);

        float middleX = (left + right) / 2;
        float middleY = (top + bottom) / 2;

        float halfWide = (right - left) / 2;
        float halfTall = (bottom - top) / 2;

        float radius = MathF.Min(halfWide, halfTall);

        // A pixel beyond the shape on every side, so the soft edge has somewhere
        // to land.
        int fromX = Math.Max((int)MathF.Floor(left) - 1, 0);
        int toX = Math.Min((int)MathF.Ceiling(right) + 1, width);
        int fromY = Math.Max((int)MathF.Floor(top) - 1, 0);
        int toY = Math.Min((int)MathF.Ceiling(bottom) + 1, height);

        for (int y = fromY; y < toY; y++)
        {
            for (int x = fromX; x < toX; x++)
            {
                float covered = Covered(
                    x + 0.5f, y + 0.5f, middleX, middleY, halfWide, halfTall, radius);

                if (covered <= 0)
                {
                    continue;
                }

                byte here = (byte)(alpha * covered);

                int at = ((y * width) + x) * 4;

                if (here <= pixels[at + 3])
                {
                    continue;
                }

                pixels[at] = colour.R;
                pixels[at + 1] = colour.G;
                pixels[at + 2] = colour.B;
                pixels[at + 3] = here;
            }
        }
    }

    /// <summary>
    /// How much of a pixel a rounded rectangle covers, from nought to one.
    /// </summary>
    ///
    /// <remarks>
    /// The distance from the pixel's middle to the shape, softened across one
    /// pixel. Exact area would mean integrating the shape over the pixel; this
    /// is the standard approximation and is indistinguishable at any size an
    /// overlay is read at.
    /// </remarks>
    private static float Covered(
        float x,
        float y,
        float middleX,
        float middleY,
        float halfWide,
        float halfTall,
        float radius)
    {
        float sideways = MathF.Max(MathF.Abs(x - middleX) - (halfWide - radius), 0);
        float updown = MathF.Max(MathF.Abs(y - middleY) - (halfTall - radius), 0);

        float outside = MathF.Sqrt((sideways * sideways) + (updown * updown)) - radius;

        return Math.Clamp(0.5f - outside, 0, 1);
    }

    private static float ToPixelAcross(float metres, Board around, int width) =>
        (metres + (around.Width / 2)) / around.Width * width;

    private static float ToPixelDown(float metres, Board around, int height) =>
        ((around.Height / 2) - metres) / around.Height * height;
}
