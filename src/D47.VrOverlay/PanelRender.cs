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
/// one answer rather than one object — which is what makes parity hold by
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
    /// <param name="answer">What is being rendered.</param>
    /// <returns>The render's natural size, in whole pixels.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
    public static (int Width, int Height) Measure(Answer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        return SizeOf(LaidOut(answer));
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
    /// <param name="answer">What to render.</param>
    /// <returns>The pixels, and how wide and tall they are.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
    public static (byte[] Pixels, int Width, int Height) Take(Answer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        CapabilityView view = LaidOut(answer);
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
    /// The control, measured and arranged at its natural size.
    /// </summary>
    ///
    /// <remarks>
    /// Measured against infinity on purpose: the render decides how big it is,
    /// and every surface then presents that at whatever size it has. Handing it
    /// a constraint here would make the headset the thing that decides the
    /// layout, which is the direction
    /// [#96](https://github.com/retiring-studios/directive-47/issues/96)
    /// establishes runs the other way.
    /// </remarks>
    private static CapabilityView LaidOut(Answer answer)
    {
        var view = new CapabilityView { DataContext = answer };

        view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        view.Arrange(new Rect(view.DesiredSize));
        view.UpdateLayout();

        return view;
    }

    /// <summary>
    /// The laid-out size in whole pixels, rounded up.
    ///
    /// <para>
    /// Up rather than to nearest, because a texture a fraction narrower than
    /// the layout is one with a column of the render missing from it.
    /// </para>
    /// </summary>
    private static (int Width, int Height) SizeOf(CapabilityView view) =>
        ((int)Math.Ceiling(view.DesiredSize.Width), (int)Math.Ceiling(view.DesiredSize.Height));
}
