using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using D47.Render;

namespace D47.VrOverlay;

/// <summary>
/// The panel's render, as pixels.
///
/// <para>
/// One renderer, two sinks: the panel puts <see cref="CapabilityView"/> in a
/// window and the headset puts the same control's pixels on a quad. WPF cannot
/// put one element in two windows, so "the same render" is one definition and
/// one presented rather than one object — which is what makes parity hold by
/// construction instead of by discipline.
/// </para>
///
/// <para>
/// Nothing here draws anything the panel does not already draw, and that is an
/// invariant of the feature rather than a property of this file. The moment
/// something VR-specific is painted in, the two surfaces can disagree.
/// </para>
/// </summary>
public static class PanelRender
{
    /// <summary>
    /// Ninety-six, which is WPF's device-independent pixel. Rendering at the
    /// control's own scale keeps the texture the size the layout asked for; the
    /// headset makes it legible by how big the quad is in metres, not by how
    /// many pixels went onto it.
    /// </summary>
    private const double ItsOwnScale = 96;

    /// <summary>
    /// How big the render wants to be, before anything is drawn.
    /// </summary>
    ///
    /// <remarks>
    /// Must be called on a thread WPF will talk to. Building a visual tree
    /// anywhere but a single-threaded apartment throws.
    /// </remarks>
    /// <param name="presented">What is being rendered.</param>
    /// <returns>The render's natural size, in whole pixels.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="presented"/> is null.</exception>
    public static (int Width, int Height) Measure(Presentation presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        return SizeOf(LaidOutForATexture(presented));
    }

    /// <summary>
    /// Draws the render and hands back its pixels, ready for a texture.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Must be called on a thread WPF will talk to, for the reason
    /// <see cref="Measure"/> gives.
    /// </para>
    /// <para>
    /// RGBA, not the BGRA WPF works in. OpenVR reads a raw buffer in the order
    /// it is given and has no format to be told, so the swap happens here — and
    /// it is a swap rather than a conversion because both are four bytes with
    /// the alpha last.
    /// </para>
    /// </remarks>
    /// <param name="presented">What to render.</param>
    /// <returns>The pixels, and how wide and tall they are.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="presented"/> is null.</exception>
    public static (byte[] Pixels, int Width, int Height) Take(Presentation presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        CapabilityView view = LaidOutForATexture(presented);
        (int width, int height) = SizeOf(view);

        var drawn = new RenderTargetBitmap(
            width, height, ItsOwnScale, ItsOwnScale, PixelFormats.Pbgra32);

        drawn.Render(view);

        byte[] pixels = new byte[width * height * 4];
        drawn.CopyPixels(pixels, width * 4, 0);

        for (int at = 0; at < pixels.Length; at += 4)
        {
            (pixels[at], pixels[at + 2]) = (pixels[at + 2], pixels[at]);
        }

        return (pixels, width, height);
    }

    /// <summary>
    /// The control at its natural size, arranged at the whole pixels a texture
    /// is made of.
    /// </summary>
    ///
    /// <remarks>
    /// The natural size is <see cref="CapabilityView.LaidOutFor"/>'s, which
    /// every surface asks and which carries the reasons the sequence is what it
    /// is. What is left here is the part only a texture needs — and the reason
    /// this asks for the control rather than the size.
    /// </remarks>
    private static CapabilityView LaidOutForATexture(Presentation presented)
    {
        var view = CapabilityView.LaidOutFor(presented);

        // Then arranged again, at the whole-pixel size the texture will be. A
        // bitmap is whole pixels and rounds up, so a control left at 221.6 wide
        // leaves the last column of a 222-wide texture never painted on. This
        // has to come after UpdateLayout rather than before: UpdateLayout runs
        // the layout queue again and re-arranges at the control's own
        // fractional size, putting the uncovered column straight back.
        //
        // On a desktop that column is a hairline nobody sees. On a half-metre
        // quad in a headset it is a visible soft edge, and it is also what
        // would drag OpenVR's premultiplied-or-straight alpha question into a
        // render that otherwise never has to presented it.
        view.Arrange(new Rect(Whole(view.DesiredSize)));

        return view;
    }

    /// <summary>
    /// A size rounded up to whole pixels.
    ///
    /// <para>
    /// Up rather than to nearest, because a texture a fraction narrower than
    /// the layout is one with a column of the render missing from it.
    /// </para>
    /// </summary>
    private static Size Whole(Size wanted) =>
        new(Math.Ceiling(wanted.Width), Math.Ceiling(wanted.Height));

    /// <summary>
    /// The size the control was arranged at.
    /// </summary>
    ///
    /// <remarks>
    /// Taken from what <see cref="LaidOutForATexture"/> asked for rather than read back
    /// off the control. <c>RenderSize</c> comes back fractional even after an
    /// arrange at whole pixels, and casting that to <c>int</c> truncates — a
    /// 222-wide render became a 221-wide texture, which is the missing column
    /// this was supposed to prevent.
    /// </remarks>
    private static (int Width, int Height) SizeOf(CapabilityView view) =>
        ((int)Math.Ceiling(view.DesiredSize.Width), (int)Math.Ceiling(view.DesiredSize.Height));
}
